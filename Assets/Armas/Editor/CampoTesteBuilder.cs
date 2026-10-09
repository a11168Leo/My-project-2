using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LeoVR.Weapons.EditorTools
{
    /// <summary>
    /// Ferramentas > Teste > Criar Campo Plano com Luvas
    /// Cria a cena "Campo_Teste" (cópia da "inicio" sem a cidade), com chão plano em grelha de 1 m,
    /// luz de dia, o arsenal, o militar como alvo de referência, e troca o modelo dos comandos pelas luvas.
    /// A cena "inicio" (cidade) NÃO é alterada.
    /// </summary>
    public static class CampoTesteBuilder
    {
        const string SrcScene = "Assets/Scenes/inicio.unity";
        const string DstScene = "Assets/Scenes/Campo_Teste.unity";
        const string CharDir = "Assets/Personagens/Militar";

        [MenuItem("Ferramentas/Teste/Criar Campo Plano com Luvas")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Teste", "Sai do Play mode primeiro.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BakeAxis(CharDir + "/Militar.fbx");
            BakeAxis(CharDir + "/Luvas.fbx");

            var scene = EditorSceneManager.OpenScene(SrcScene, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, DstScene, true);
            scene = EditorSceneManager.OpenScene(DstScene, OpenSceneMode.Single);

            // 1) Limpar o mundo antigo (fica só o XR Origin, a luz e os gestores)
            GameObject origin = null;
            Light sun = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                string n = root.name;
                if (n.Contains("XR Origin")) { origin = root; continue; }
                var l = root.GetComponent<Light>();
                if (l != null && l.type == LightType.Directional) { sun = l; continue; }
                if (n.Contains("Interaction") || n.Contains("EventSystem") || n.Contains("Manager")) continue;
                Debug.Log("[Campo Teste] Removido: " + n);
                Object.DestroyImmediate(root);
            }
            if (origin == null)
            {
                Debug.LogError("[Campo Teste] Não encontrei o XR Origin na cena.");
                return;
            }
            float floorY = origin.transform.position.y;

            // 2) Chão plano 300 x 300 m com grelha de 1 m
            var gridTex = MakeGridTexture();
            var groundMat = LitMat(CharDir + "/../Chao_Grelha.mat", Color.white, 0f, 0.2f);
            groundMat.SetTexture("_BaseMap", gridTex);
            groundMat.SetTextureScale("_BaseMap", new Vector2(300, 300));
            EditorUtility.SetDirty(groundMat);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Chao_Plano";
            ground.transform.position = new Vector3(origin.transform.position.x, floorY, origin.transform.position.z);
            ground.transform.localScale = new Vector3(30, 1, 30);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            ground.isStatic = true;

            // 3) Luz de dia + céu
            if (sun == null)
            {
                var go = new GameObject("Directional Light");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            var cam = origin.GetComponentInChildren<Camera>(true);
            if (cam) cam.clearFlags = CameraClearFlags.Skybox;

            Physics.SyncTransforms();

            // 4) Arsenal (mesa, armas, alvos, equipamento no corpo)
            ArsenalBuilder.BuildAll();

            // 5) Material do militar (texturas 2K)
            var mat = MilitarMaterial();

            // 6) Militar de referência / alvo a 3 m
            var militarAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharDir + "/Militar.fbx");
            if (militarAsset)
            {
                var m = (GameObject)PrefabUtility.InstantiatePrefab(militarAsset);
                m.name = "Militar_Referencia";
                var fwd = Vector3.ProjectOnPlane(origin.transform.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                var right = Vector3.Cross(Vector3.up, fwd);
                m.transform.position = new Vector3(origin.transform.position.x, floorY, origin.transform.position.z) + fwd * 3f + right * 1.2f;
                m.transform.rotation = Quaternion.LookRotation(fwd); // o modelo olha para -Z, assim fica virado para o jogador
                foreach (var r in m.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                var h = m.AddComponent<Health>();
                h.maxHealth = 100f;
                var body = new GameObject("Hitbox_Corpo");
                body.transform.SetParent(m.transform, false);
                var cap = body.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0, 0.75f, 0);
                cap.height = 1.5f;
                cap.radius = 0.22f;
                var zb = body.AddComponent<DamageZone>(); zb.owner = h; zb.multiplier = 1f;
                var head = new GameObject("Hitbox_Cabeca");
                head.transform.SetParent(m.transform, false);
                head.transform.localPosition = new Vector3(0, 1.63f, 0);
                var sph = head.AddComponent<SphereCollider>();
                sph.radius = 0.13f;
                var zh = head.AddComponent<DamageZone>(); zh.owner = h; zh.multiplier = 4f;
            }
            else Debug.LogWarning("[Campo Teste] Não encontrei " + CharDir + "/Militar.fbx");

            // 7) Luvas no lugar dos comandos (luvas SRG se existirem; senão as do militar)
            const string srgPath = "Assets/Equipamento/Luvas/SRG/Luvas_SRG.fbx";
            BakeAxis(srgPath);
            var srg = AssetDatabase.LoadAssetAtPath<GameObject>(srgPath);
            var luvas = AssetDatabase.LoadAssetAtPath<GameObject>(CharDir + "/Luvas.fbx");
            if (srg)
            {
                var gmat = GloveMaterial("Assets/Equipamento/Luvas/SRG");
                AttachGlove(origin.transform, "Right Controller", "XR Controller Right", srg, "Luva_D", gmat, true);
                AttachGlove(origin.transform, "Left Controller", "XR Controller Left", srg, "Luva_E", gmat, true);
            }
            else if (luvas)
            {
                AttachGlove(origin.transform, "Right Controller", "XR Controller Right", luvas, "Luva_D", mat, false);
                AttachGlove(origin.transform, "Left Controller", "XR Controller Left", luvas, "Luva_E", mat, false);
            }
            else Debug.LogWarning("[Campo Teste] Não encontrei luvas.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Campo Teste] Cena Campo_Teste criada. A cena 'inicio' (cidade) ficou igual.");
        }

        /// <summary>FBX do Blender: converte os eixos na importação (o modelo fica de pé, sem rotação -90).</summary>
        static void BakeAxis(string path)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi != null && !mi.bakeAxisConversion)
            {
                mi.bakeAxisConversion = true;
                mi.SaveAndReimport();
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r) return r;
            }
            return null;
        }

        static void AttachGlove(Transform origin, string controllerName, string controllerModelName, GameObject luvasAsset, string meshName, Material mat, bool srgGlove)
        {
            var ctrl = FindDeep(origin, controllerName);
            if (!ctrl) { Debug.LogWarning("[Campo Teste] Não encontrei " + controllerName); return; }

            // esconder o modelo do comando
            string side = controllerName.StartsWith("Right") ? "Right" : "Left";
            foreach (var n in new[] { controllerModelName, side + " Controller Visual" })
            {
                var model = FindDeep(ctrl, n);
                if (model) { model.gameObject.SetActive(false); Debug.Log("[Campo Teste] Modelo do comando escondido: " + n); }
            }

            var old = FindDeep(ctrl, "Luva_" + (meshName.EndsWith("D") ? "Direita" : "Esquerda"));
            if (old) Object.DestroyImmediate(old.gameObject);

            var src = FindDeep(luvasAsset.transform, meshName);
            if (!src) { Debug.LogWarning("[Campo Teste] O FBX das luvas não tem " + meshName); return; }

            // Luva_X  (posição da palma em relação ao comando — afina aqui se precisares)
            //  └ Rotacao (dedos para a frente, polegar para cima)
            //      └ malha
            var rootT = new GameObject("Luva_" + (meshName.EndsWith("D") ? "Direita" : "Esquerda")).transform;
            rootT.SetParent(ctrl, false);
            rootT.localPosition = srgGlove ? new Vector3(0f, -0.02f, -0.05f) : new Vector3(0f, -0.01f, -0.03f);
            rootT.localRotation = Quaternion.identity;
            var rot = new GameObject("Rotacao").transform;
            rot.SetParent(rootT, false);
            // luvas SRG já vêm com os dedos para a frente; as do militar estavam penduradas (dedos para baixo)
            rot.localRotation = srgGlove ? Quaternion.identity : Quaternion.Euler(-90f, 0f, 0f);
            var mesh = Object.Instantiate(src.gameObject);
            mesh.name = meshName;
            mesh.transform.SetParent(rot, false);
            mesh.transform.localPosition = src.localPosition;
            // FBX do Blender entra virado para -Z: rodar 180° em Y antes de pôr os dedos para a frente
            mesh.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * src.localRotation;
            mesh.transform.localScale = src.localScale;
            foreach (var r in mesh.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            foreach (var c in mesh.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }

        static Material LitMat(string path, Color c, float metal, float smooth)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = sh;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GloveMaterial(string dir)
        {
            string nrmPath = dir + "/Texturas/Luva_SRG_Normal.png";
            var imp = AssetImporter.GetAtPath(nrmPath) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
            var m = LitMat(dir + "/Luva_SRG.mat", Color.white, 0f, 0.35f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Texturas/Luva_SRG_Base.png"));
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(nrmPath);
            if (nrm) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MilitarMaterial()
        {
            string texDir = CharDir + "/Texturas";
            string normalPath = texDir + "/Militar_normal.png";
            var imp = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
            var m = LitMat(CharDir + "/Militar_URP.mat", Color.white, 0f, 0.25f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/Militar.png"));
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (nrm)
            {
                m.SetTexture("_BumpMap", nrm);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static Texture2D MakeGridTexture()
        {
            string path = "Assets/Personagens/Grelha_1m.png";
            const int size = 256;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool line = x < 3 || y < 3;
                    bool mid = x == size / 2 || y == size / 2;
                    var c = line ? new Color(0.25f, 0.27f, 0.3f) : mid ? new Color(0.5f, 0.52f, 0.55f) : new Color(0.6f, 0.62f, 0.64f);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.anisoLevel = 8;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
