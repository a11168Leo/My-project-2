using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LeoVR.Weapons.EditorTools
{
    /// <summary>
    /// Ferramentas > Mapas > Criar Carreira de Tiro
    /// Carreira de tiro realista: linha de tiro coberta, 10 pistas com divisórias, mesas,
    /// alvos de papel (7/15/25 m), placas de aço que balançam (10 m), bonecos (7/12/20 m),
    /// taludes de terra, parapeito de sacos de areia, contentores, placas de distância.
    /// Cada material tem o seu impacto (papel, aço, madeira, betão, terra).
    /// </summary>
    public static class RangeBuilder
    {
        const string SrcScene = "Assets/Scenes/inicio.unity";
        const string Dir = "Assets/Mapas/CarreiraTiro";
        const string DstScene = Dir + "/CarreiraTiro.unity";
        const string TexDir = Dir + "/Texturas";
        const string MatDir = Dir + "/Materiais";
        const string PrefDir = Dir + "/Prefabs";

        static Material mGravel, mDirt, mConcrete, mWood, mPaper, mSteel, mSteelPaint, mYellow, mSandbag, mContainer, mSign;
        static GameObject holePaper, holeSteel, holeConcrete, holeWood, fxSparks, fxSplinters, fxDust, holeDefault;

        [MenuItem("Ferramentas/Mapas/Criar Carreira de Tiro")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Mapas", "Sai do Play mode primeiro.", "OK"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var d in new[] { "Assets/Mapas", Dir, TexDir, MatDir, PrefDir })
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(SrcScene, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, DstScene, true);
            scene = EditorSceneManager.OpenScene(DstScene, OpenSceneMode.Single);

            // limpar a cidade
            GameObject origin = null; Light sun = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                string n = root.name;
                if (n.Contains("XR Origin")) { origin = root; continue; }
                var l = root.GetComponent<Light>();
                if (l != null && l.type == LightType.Directional) { sun = l; continue; }
                if (n.Contains("Interaction") || n.Contains("EventSystem") || n.Contains("Manager")) continue;
                Object.DestroyImmediate(root);
            }
            if (origin == null) { Debug.LogError("[Carreira] Sem XR Origin na cena."); return; }
            origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // luz de dia
            if (sun == null) { sun = new GameObject("Directional Light").AddComponent<Light>(); sun.type = LightType.Directional; }
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.75f, 0.8f, 0.85f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 250f;
            var cam = origin.GetComponentInChildren<Camera>(true);
            if (cam) cam.clearFlags = CameraClearFlags.Skybox;

            CreateMaterials();
            CreateImpactPrefabs();

            var R = new GameObject("Carreira_de_Tiro").transform;
            BuildRange(R);
            Physics.SyncTransforms();

            // armas, mesa, bonecos, equipamento no corpo
            ArsenalBuilder.BuildAll();
            // materiais de impacto também nos bonecos e mesa do arsenal
            var ars = GameObject.Find("Arsenal (Teste)");
            if (ars)
            {
                foreach (var h in ars.GetComponentsInChildren<Health>()) Surface(h.gameObject, SurfaceImpact.Kind.Madeira);
                var mesa = ars.transform.Find("Mesa");
                if (mesa) Surface(mesa.gameObject, SurfaceImpact.Kind.Madeira);
            }

            // luvas com dedos animados
            CampoTesteBuilder.SetupGloves(origin.transform, CampoTesteBuilder.MilitarMaterial());

            AddToBuildSettings(DstScene);
            AddToBuildSettings("Assets/Scenes/Campo_Teste.unity");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Carreira] Carreira de tiro criada em " + DstScene);
        }

        // ------------------------------------------------------------------ teste de disparo no editor
        [MenuItem("Ferramentas/Teste/Disparar arma selecionada %#f")]
        public static void TestFireSelected()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("Entra em Play primeiro."); return; }
            Firearm fw = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<Firearm>() : null;
            if (fw == null) fw = Object.FindFirstObjectByType<Firearm>();
            if (fw == null) { Debug.LogWarning("Não há armas na cena."); return; }
            fw.TestFire();
            Debug.Log("[Teste] Disparo de teste: " + fw.weaponName);
        }

        static void AddToBuildSettings(string path)
        {
            if (!File.Exists(path)) return;
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list) if (s.path == path) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ geometria
        static GameObject P(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material m, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }
        static GameObject Cube(string n, Transform p, Vector3 pos, Vector3 size, Material m, Vector3 euler = default, bool col = true)
            => P(PrimitiveType.Cube, n, p, pos, size, euler, m, col);

        static void Surface(GameObject go, SurfaceImpact.Kind kind)
        {
            var s = go.GetComponent<SurfaceImpact>();
            if (s == null) s = go.AddComponent<SurfaceImpact>();
            s.kind = kind;
            switch (kind)
            {
                case SurfaceImpact.Kind.Papel: s.holePrefab = holePaper; s.fxPrefab = fxSplinters; s.holeScale = 0.8f; break;
                case SurfaceImpact.Kind.Metal: s.holePrefab = holeSteel; s.fxPrefab = fxSparks; s.holeScale = 2.2f; break;
                case SurfaceImpact.Kind.Madeira: s.holePrefab = holeWood; s.fxPrefab = fxSplinters; s.holeScale = 1f; break;
                case SurfaceImpact.Kind.Betao: s.holePrefab = holeConcrete; s.fxPrefab = fxDust; s.holeScale = 1.6f; break;
                default: s.holePrefab = holeDefault; s.fxPrefab = fxDust; s.holeScale = 1.5f; break;
            }
        }

        static void BuildRange(Transform R)
        {
            const float line = 1.45f; // linha de tiro (z)

            // chão de gravilha 140 x 100 m
            var ground = P(PrimitiveType.Plane, "Chao_Gravilha", R, new Vector3(0, 0, 15), new Vector3(14, 1, 10), Vector3.zero, mGravel);
            Surface(ground, SurfaceImpact.Kind.Terra);

            // posto de tiro coberto: laje de betão, linha amarela, telhado
            var slab = Cube("Laje_Betao", R, new Vector3(0, 0.005f, -1.5f), new Vector3(20.5f, 0.01f, 6f), mConcrete);
            Surface(slab, SurfaceImpact.Kind.Betao);
            Cube("Linha_de_Tiro", R, new Vector3(0, 0.012f, line), new Vector3(20.5f, 0.012f, 0.08f), mYellow, default, false);
            foreach (var x in new[] { -10f, 10f })
                foreach (var z in new[] { -4.3f, 1.3f })
                    Surface(Cube("Pilar", R, new Vector3(x, 1.55f, z), new Vector3(0.15f, 3.1f, 0.15f), mSteelPaint), SurfaceImpact.Kind.Metal);
            Surface(Cube("Telhado", R, new Vector3(0, 3.16f, -1.5f), new Vector3(20.8f, 0.12f, 6.4f), mSteelPaint), SurfaceImpact.Kind.Metal);

            // divisórias entre pistas (pistas de 2 m; a tua é a do meio, x = 0)
            for (int i = -5; i <= 4; i++)
            {
                float x = i * 2f + 1f;
                var div = Cube("Divisoria", R, new Vector3(x, 0.9f, 0.6f), new Vector3(0.06f, 1.8f, 1.5f), mWood);
                Surface(div, SurfaceImpact.Kind.Madeira);
            }
            // mesas nas outras pistas (a do meio vem com as armas)
            for (int i = -4; i <= 4; i++)
            {
                if (i == 0) continue;
                float x = i * 2f;
                var t = new GameObject("Mesa_Pista").transform; t.SetParent(R, false); t.localPosition = new Vector3(x, 0, 0.75f);
                Surface(Cube("Tampo", t, new Vector3(0, 0.78f, 0), new Vector3(1.2f, 0.04f, 0.6f), mWood), SurfaceImpact.Kind.Madeira);
                foreach (var lx in new[] { -0.55f, 0.55f })
                    foreach (var lz in new[] { -0.25f, 0.25f })
                        Cube("Perna", t, new Vector3(lx, 0.38f, lz), new Vector3(0.05f, 0.76f, 0.05f), mWood);
            }

            // taludes laterais e talude de fundo (para-balas)
            foreach (var sx in new[] { -1f, 1f })
            {
                var b = Cube("Talude_Lateral", R, new Vector3(sx * 12.2f, 0.9f, 17f), new Vector3(4f, 3f, 34f), mDirt, new Vector3(0, 0, sx * 18f));
                Surface(b, SurfaceImpact.Kind.Terra);
            }
            var back = Cube("Talude_Fundo", R, new Vector3(0, 2.4f, 35f), new Vector3(30f, 8f, 5f), mDirt, new Vector3(-28f, 0, 0));
            Surface(back, SurfaceImpact.Kind.Terra);

            // parapeito de sacos de areia na base do talude
            var bags = new GameObject("Sacos_de_Areia_Fundo").transform; bags.SetParent(R, false);
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 44; k++)
                {
                    float x = -11f + k * 0.5f + (row % 2) * 0.25f;
                    if (x > 11f) continue;
                    Surface(Cube("Saco", bags, new Vector3(x, 0.08f + row * 0.15f, 32f), new Vector3(0.5f, 0.15f, 0.32f), mSandbag, new Vector3(0, Random.Range(-4f, 4f), 0)), SurfaceImpact.Kind.Terra);
                }
            // abrigos de sacos de areia a 12 m (pistas vizinhas)
            foreach (var x in new[] { -4f, 4f })
            {
                var w = new GameObject("Abrigo_Sacos").transform; w.SetParent(R, false); w.localPosition = new Vector3(x, 0, line + 12f);
                for (int row = 0; row < 6; row++)
                    for (int k = 0; k < 4; k++)
                        Surface(Cube("Saco", w, new Vector3(-0.75f + k * 0.5f + (row % 2) * 0.25f, 0.08f + row * 0.15f, 0), new Vector3(0.5f, 0.15f, 0.32f), mSandbag), SurfaceImpact.Kind.Terra);
            }

            // placas de distância
            for (int d = 5; d <= 25; d += 5)
            {
                var post = Cube("Placa_" + d + "m", R, new Vector3(-9.6f, 0.6f, line + d), new Vector3(0.08f, 1.2f, 0.08f), mWood);
                Surface(post, SurfaceImpact.Kind.Madeira);
                var board = Cube("Painel", post.transform.parent, new Vector3(-9.6f, 1.3f, line + d - 0.05f), new Vector3(0.6f, 0.3f, 0.03f), mSign);
                Surface(board, SurfaceImpact.Kind.Madeira);
                var tgo = new GameObject("Texto"); tgo.transform.SetParent(R, false);
                tgo.transform.localPosition = new Vector3(-9.6f, 1.3f, line + d - 0.07f);
                var tmp = tgo.AddComponent<TextMeshPro>();
                tmp.text = d + " m";
                tmp.fontSize = 2.2f;
                tmp.color = Color.black;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.rectTransform.sizeDelta = new Vector2(0.6f, 0.3f);
            }

            // alvos de papel (IPSC) na pista do meio a 7, 15 e 25 m, e nas pistas vizinhas a 10 m
            foreach (var d in new[] { 7f, 15f, 25f }) PaperTarget(R, new Vector3(0.6f, 0, line + d));
            foreach (var x in new[] { -6f, -4f, -2f, 2f, 4f, 6f }) PaperTarget(R, new Vector3(x, 0, line + 10f));

            // placas de aço que balançam a 10 m
            SteelRack(R, new Vector3(-0.2f, 0, line + 10f));

            // contentores
            Surface(Cube("Contentor", R, new Vector3(-17f, 1.3f, -5f), new Vector3(2.44f, 2.6f, 6.06f), mContainer), SurfaceImpact.Kind.Metal);
            Surface(Cube("Contentor", R, new Vector3(16f, 1.3f, -2f), new Vector3(6.06f, 2.6f, 2.44f), mContainer, new Vector3(0, 12f, 0)), SurfaceImpact.Kind.Metal);
        }

        static void PaperTarget(Transform R, Vector3 pos)
        {
            var t = new GameObject("Alvo_Papel").transform; t.SetParent(R, false); t.localPosition = pos;
            foreach (var x in new[] { -0.28f, 0.28f })
                Surface(Cube("Estaca", t, new Vector3(x, 0.8f, 0.02f), new Vector3(0.04f, 1.6f, 0.04f), mWood), SurfaceImpact.Kind.Madeira);
            var card = Cube("Cartao_IPSC", t, new Vector3(0, 1.25f, 0), new Vector3(0.46f, 0.76f, 0.004f), mPaper);
            card.isStatic = false;
            Surface(card, SurfaceImpact.Kind.Papel);
        }

        static void SteelRack(Transform R, Vector3 pos)
        {
            var t = new GameObject("Placas_de_Aco").transform; t.SetParent(R, false); t.localPosition = pos;
            Surface(Cube("Barra", t, new Vector3(0, 1.25f, 0), new Vector3(1.8f, 0.05f, 0.05f), mSteelPaint), SurfaceImpact.Kind.Metal);
            foreach (var x in new[] { -0.85f, 0.85f })
                Surface(Cube("Perna", t, new Vector3(x, 0.62f, 0), new Vector3(0.05f, 1.25f, 0.05f), mSteelPaint), SurfaceImpact.Kind.Metal);
            float[] xs = { -0.6f, -0.2f, 0.2f, 0.6f };
            for (int i = 0; i < xs.Length; i++)
            {
                var plate = new GameObject("Placa_Aco_" + (i + 1));
                plate.transform.SetParent(t, false);
                plate.transform.localPosition = new Vector3(xs[i], 1.22f, 0.04f);
                var rb = plate.AddComponent<Rigidbody>();
                rb.mass = 2.5f;
                rb.angularDamping = 0.6f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                var hj = plate.AddComponent<HingeJoint>();
                hj.anchor = Vector3.zero;
                hj.axis = Vector3.right;
                Cube("Corrente", plate.transform, new Vector3(0, -0.06f, 0), new Vector3(0.02f, 0.12f, 0.01f), mSteelPaint, default, false).isStatic = false;
                var disk = P(PrimitiveType.Cylinder, "Disco", plate.transform, new Vector3(0, -0.22f, 0), new Vector3(0.2f, 0.006f, 0.2f), new Vector3(90, 0, 0), mSteel, false);
                disk.isStatic = false;
                var col = plate.AddComponent<BoxCollider>();
                col.center = new Vector3(0, -0.22f, 0);
                col.size = new Vector3(0.2f, 0.2f, 0.012f);
                Surface(plate, SurfaceImpact.Kind.Metal);
            }
        }

        // ------------------------------------------------------------------ materiais e texturas
        static Texture2D Tex(string name, int size, System.Func<int, int, Color> f, bool alpha = false, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            string path = $"{TexDir}/{name}.png";
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) t.SetPixel(x, y, f(x, y));
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = wrap;
            imp.alphaIsTransparency = alpha;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Noise(float x, float y, float s, int oct = 4)
        {
            float v = 0, a = 0.5f, f = s;
            for (int i = 0; i < oct; i++) { v += a * Mathf.PerlinNoise(x * f + 31.7f * i, y * f + 17.3f * i); f *= 2f; a *= 0.5f; }
            return v;
        }

        static Material Mat(string name, string shader, Color c, Texture2D tex = null, Vector2? tiling = null, float metal = 0f, float smooth = 0.2f)
        {
            string path = $"{MatDir}/{name}.mat";
            var sh = Shader.Find(shader);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetColor("_BaseColor", c);
            if (tex) { m.SetTexture("_BaseMap", tex); if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value); }
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }
        const string Lit = "Universal Render Pipeline/Lit";
        const string Unlit = "Universal Render Pipeline/Unlit";

        static Material Cutout(string name, Texture2D tex)
        {
            var m = Mat(name, Unlit, Color.white, tex);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void CreateMaterials()
        {
            var gravel = Tex("Gravilha", 256, (x, y) =>
            {
                float n = Noise(x, y, 0.05f); float sp = Mathf.PerlinNoise(x * 0.9f, y * 0.9f);
                float v = 0.42f + 0.25f * n + (sp > 0.72f ? 0.12f : sp < 0.25f ? -0.1f : 0f);
                return new Color(v * 1.02f, v * 0.97f, v * 0.88f);
            });
            mGravel = Mat("Gravilha", Lit, Color.white, gravel, new Vector2(70, 50), 0, 0.1f);
            var dirt = Tex("Terra", 256, (x, y) =>
            {
                float n = Noise(x, y, 0.03f); float v = 0.3f + 0.25f * n;
                return new Color(v * 1.15f, v * 0.9f, v * 0.62f);
            });
            mDirt = Mat("Terra", Lit, Color.white, dirt, new Vector2(8, 8), 0, 0.05f);
            var conc = Tex("Betao", 256, (x, y) =>
            {
                float n = Noise(x, y, 0.08f); float v = 0.62f + 0.12f * n - (Mathf.PerlinNoise(x * 0.7f, y * 0.7f) > 0.8f ? 0.08f : 0);
                return new Color(v, v, v * 0.98f);
            });
            mConcrete = Mat("Betao", Lit, Color.white, conc, new Vector2(10, 3), 0, 0.15f);
            var wood = Tex("Madeira", 256, (x, y) =>
            {
                float plank = (y % 64) < 2 ? 0.55f : 1f;
                float grain = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.02f, y * 0.35f);
                return new Color(0.55f * grain * plank, 0.38f * grain * plank, 0.22f * grain * plank);
            });
            mWood = Mat("Madeira", Lit, Color.white, wood, new Vector2(1, 1), 0, 0.25f);
            var paper = Tex("Alvo_IPSC", 256, IpscPixel, false, TextureWrapMode.Clamp);
            mPaper = Mat("Alvo_IPSC", Lit, Color.white, paper, null, 0, 0.05f);
            mSteel = Mat("Aco_Placa", Lit, new Color(0.85f, 0.85f, 0.82f), null, null, 0.2f, 0.35f);
            mSteelPaint = Mat("Aco_Pintado", Lit, new Color(0.18f, 0.2f, 0.2f), null, null, 0.6f, 0.35f);
            mYellow = Mat("Tinta_Amarela", Lit, new Color(1f, 0.82f, 0.1f), null, null, 0, 0.3f);
            var bag = Tex("Saco_Areia", 128, (x, y) =>
            {
                float v = 0.62f + 0.12f * Noise(x, y, 0.2f, 3);
                float st = (x % 32 < 1 || y % 16 < 1) ? 0.8f : 1f;
                return new Color(0.78f * v * st, 0.7f * v * st, 0.5f * v * st);
            });
            mSandbag = Mat("Saco_Areia", Lit, Color.white, bag, new Vector2(1, 1), 0, 0.1f);
            mContainer = Mat("Contentor", Lit, new Color(0.22f, 0.32f, 0.22f), null, null, 0.5f, 0.3f);
            mSign = Mat("Placa_Branca", Lit, new Color(0.95f, 0.95f, 0.92f), null, null, 0, 0.2f);
        }

        /// <summary>Alvo IPSC simplificado: cartão castanho com zonas A, C e D marcadas.</summary>
        static Color IpscPixel(int x, int y)
        {
            float u = (x + 0.5f) / 256f * 0.46f - 0.23f;   // metros, centro = 0
            float v = (y + 0.5f) / 256f * 0.76f - 0.38f;
            var card = new Color(0.78f, 0.64f, 0.45f);
            bool line = false;
            // cabeça (topo)
            bool head = v > 0.2f && Mathf.Abs(u) < 0.075f;
            bool headA = v > 0.255f && v < 0.31f && Mathf.Abs(u) < 0.04f;
            // corpo
            bool body = v <= 0.2f && v > -0.38f && Mathf.Abs(u) < 0.23f - Mathf.Max(0, (v - 0.12f)) * 1.2f;
            bool zoneA = v > -0.05f && v < 0.15f && Mathf.Abs(u) < 0.075f;
            bool zoneC = v > -0.17f && v < 0.17f && Mathf.Abs(u) < 0.15f;
            float e = 0.004f;
            if (head && (Mathf.Abs(Mathf.Abs(u) - 0.075f) < e || Mathf.Abs(v - 0.38f) < e)) line = true;
            if (headA && (Mathf.Abs(Mathf.Abs(u) - 0.04f) < e || Mathf.Abs(v - 0.255f) < e || Mathf.Abs(v - 0.31f) < e)) line = true;
            if (zoneA && (Mathf.Abs(Mathf.Abs(u) - 0.075f) < e || Mathf.Abs(v + 0.05f) < e || Mathf.Abs(v - 0.15f) < e)) line = true;
            if (zoneC && (Mathf.Abs(Mathf.Abs(u) - 0.15f) < e || Mathf.Abs(v + 0.17f) < e || Mathf.Abs(v - 0.17f) < e)) line = true;
            if (!head && !body) return new Color(0.7f, 0.57f, 0.4f);
            if (line) return new Color(0.25f, 0.18f, 0.1f);
            if (zoneA || headA) return new Color(0.82f, 0.68f, 0.48f);
            return card;
        }

        static GameObject SavePrefab(GameObject go)
        {
            var path = $"{PrefDir}/{go.name}.prefab";
            var p = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return p;
        }

        static GameObject HolePrefab(string name, System.Func<float, float, float, Color> f)
        {
            var tex = Tex(name, 64, (x, y) =>
            {
                float u = (x + 0.5f) / 64f * 2 - 1, v = (y + 0.5f) / 64f * 2 - 1;
                float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                return f(r, a, Mathf.PerlinNoise(Mathf.Cos(a) * 2 + 3, Mathf.Sin(a) * 2 + 3));
            }, true, TextureWrapMode.Clamp);
            var m = Cutout(name, tex);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            var r = q.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
            return SavePrefab(q);
        }

        static GameObject FxPrefab(string name, Color c, float speedMin, float speedMax, float sizeMax, float life, int count, float gravity)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f; main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.4f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMax * 0.3f, sizeMax);
            main.startColor = c;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count * 2;
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0, count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35; sh.radius = 0.004f;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
            var mat = Mat(name + "_Mat", "Universal Render Pipeline/Particles/Unlit", c);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            return SavePrefab(go);
        }

        static void CreateImpactPrefabs()
        {
            holePaper = HolePrefab("Buraco_Papel", (r, a, n) => r < 0.45f + 0.08f * n ? new Color(0.05f, 0.04f, 0.03f, 1) : new Color(0, 0, 0, 0));
            holeSteel = HolePrefab("Marca_Aco", (r, a, n) =>
            {
                float star = 0.35f + 0.55f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 4f + n)), 4f) * n;
                if (r > star) return new Color(0, 0, 0, 0);
                return r < 0.18f ? new Color(0.35f, 0.35f, 0.36f, 1) : new Color(0.72f, 0.72f, 0.74f, 1);
            });
            holeConcrete = HolePrefab("Buraco_Betao", (r, a, n) =>
            {
                float edge = 0.5f + 0.3f * n;
                if (r > edge) return new Color(0, 0, 0, 0);
                return r < 0.2f ? new Color(0.18f, 0.18f, 0.18f, 1) : new Color(0.82f, 0.82f, 0.8f, 1);
            });
            holeWood = HolePrefab("Buraco_Madeira", (r, a, n) =>
            {
                float edge = 0.42f + 0.2f * n * Mathf.Abs(Mathf.Sin(a * 3f));
                if (r > edge) return new Color(0, 0, 0, 0);
                return r < 0.25f ? new Color(0.06f, 0.04f, 0.02f, 1) : new Color(0.75f, 0.6f, 0.4f, 1);
            });
            fxSparks = FxPrefab("FX_Faiscas", new Color(1f, 0.75f, 0.3f), 2f, 7f, 0.012f, 0.35f, 18, 1.2f);
            fxSplinters = FxPrefab("FX_Lascas", new Color(0.55f, 0.42f, 0.28f), 1f, 3f, 0.015f, 0.6f, 12, 1f);
            fxDust = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Armas/Prefabs/Impacto_Poeira.prefab");
            holeDefault = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Armas/Prefabs/BuracoBala.prefab");
        }
    }
}
