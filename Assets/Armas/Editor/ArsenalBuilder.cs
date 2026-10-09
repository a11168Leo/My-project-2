using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LeoVR.Weapons.EditorTools
{
    /// <summary>
    /// Menu Ferramentas > Armas > Construir Arsenal e Colocar na Cena
    /// Gera Glock 17, M4A1, cassetete, carregadores, cápsulas, alvos e o equipamento do corpo
    /// com medidas reais (em metros), e coloca tudo à frente do XR Origin.
    /// </summary>
    public static class ArsenalBuilder
    {
        const string RootDir = "Assets/Armas";
        const string PrefabDir = RootDir + "/Prefabs";
        const string MatDir = RootDir + "/Materiais";
        const string TexDir = RootDir + "/Texturas";

        static Material mPolymer, mSlide, mAnod, mMetal, mBrass, mCopper, mRubber, mDark, mPmag,
            mDummy, mWood, mCrate, mCan, mGear, mRedDot, mHole, mFlash, mParticle;

        [MenuItem("Ferramentas/Armas/Construir Arsenal e Colocar na Cena")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Armas", "Sai do Play mode primeiro.", "OK");
                return;
            }

            EnsureFolders();
            CreateMaterials();
            CreateModelMaterials();
            CreateAKMaterials();

            var casing9 = Save(BuildCasing("Capsula_9mm", 0.0099f, 0.019f, false));
            var round9 = Save(BuildCasing("Bala_9mm", 0.0099f, 0.019f, true));
            var casing556 = Save(BuildCasing("Capsula_556", 0.0096f, 0.045f, false));
            var round556 = Save(BuildCasing("Bala_556", 0.0096f, 0.045f, true));
            var hole = Save(BuildBulletHole());
            var fx = Save(BuildImpactFx());
            var magGlock = Save(BuildGlockMag());
            var glock = Save(BuildGlock(casing9, round9, hole, fx));
            GameObject magM4, m4;
            if (HasAKModel)
            {
                var casing762 = Save(BuildCasingModel762("Capsula_762", false));
                var round762 = Save(BuildCasingModel762("Bala_762", true));
                magM4 = Save(BuildAKMag());
                m4 = Save(BuildAK(casing762, round762, hole, fx));
            }
            else
            {
                magM4 = Save(BuildStanagMag());
                m4 = Save(BuildM4(casing556, round556, hole, fx));
            }
            var baton = Save(BuildBaton());
            var dummy = Save(BuildDummy());

            PlaceInScene(glock, m4, baton, magGlock, magM4, dummy);
            AssetDatabase.SaveAssets();
            Debug.Log("[Armas] Arsenal construído: Glock 17, " + (HasAKModel ? "AK-47" : "M4A1") + ", cassetete, carregadores, alvos e equipamento do corpo.");
        }

        // ------------------------------------------------------------------ pastas / materiais
        static void EnsureFolders()
        {
            foreach (var d in new[] { RootDir, PrefabDir, MatDir, TexDir })
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
        }

        static Shader Find(params string[] names)
        {
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            return Shader.Find("Standard");
        }

        static Material GetOrCreate(string name, Shader shader)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            return m;
        }

        static Material Lit(string name, Color c, float metal, float smooth)
        {
            var m = GetOrCreate(name, Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Unlit(string name, Color c)
        {
            var m = GetOrCreate(name, Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Cutout(string name, Texture2D tex, Color c)
        {
            var m = Unlit(name, c);
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D MakeTexture(string name, int size, System.Func<float, float, Color> f)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    t.SetPixel(x, y, f(u, v));
                }
            t.Apply();
            string path = $"{TexDir}/{name}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void CreateMaterials()
        {
            mPolymer = Lit("Polimero_Preto", new Color(0.06f, 0.06f, 0.065f), 0f, 0.35f);
            mSlide = Lit("Aco_Slide", new Color(0.12f, 0.12f, 0.13f), 0.85f, 0.55f);
            mAnod = Lit("Aluminio_Anodizado", new Color(0.09f, 0.09f, 0.09f), 0.6f, 0.4f);
            mMetal = Lit("Aco_Polido", new Color(0.55f, 0.55f, 0.57f), 1f, 0.7f);
            mBrass = Lit("Latao", new Color(0.78f, 0.6f, 0.25f), 1f, 0.75f);
            mCopper = Lit("Cobre", new Color(0.72f, 0.42f, 0.25f), 1f, 0.7f);
            mRubber = Lit("Borracha", new Color(0.04f, 0.04f, 0.04f), 0f, 0.1f);
            mDark = Lit("Preto_Fosco", new Color(0.01f, 0.01f, 0.01f), 0f, 0.2f);
            mPmag = Lit("PMAG_Areia", new Color(0.55f, 0.47f, 0.33f), 0f, 0.3f);
            mDummy = Lit("Alvo", new Color(0.62f, 0.55f, 0.4f), 0f, 0.2f);
            mWood = Lit("Madeira", new Color(0.42f, 0.28f, 0.16f), 0f, 0.3f);
            mCrate = Lit("Caixa", new Color(0.35f, 0.33f, 0.2f), 0f, 0.25f);
            mCan = Lit("Lata", new Color(0.75f, 0.1f, 0.1f), 0.8f, 0.6f);
            mGear = Lit("Equipamento_Oliva", new Color(0.2f, 0.23f, 0.15f), 0f, 0.2f);
            mRedDot = Unlit("Ponto_Vermelho", new Color(1f, 0.05f, 0.05f));

            var holeTex = MakeTexture("BuracoBala", 64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u);
                float edge = 0.55f + 0.18f * Mathf.PerlinNoise(Mathf.Cos(a) * 3f + 5f, Mathf.Sin(a) * 3f + 5f);
                if (r > edge) return new Color(0, 0, 0, 0);
                return r < 0.28f ? new Color(0.01f, 0.01f, 0.01f, 1f) : new Color(0.13f, 0.12f, 0.11f, 1f);
            });
            mHole = Cutout("BuracoBala", holeTex, Color.white);

            var flashTex = MakeTexture("Clarao", 64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u);
                float spikes = 0.3f + 0.65f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 6f);
                if (r > spikes) return new Color(0, 0, 0, 0);
                return Color.Lerp(new Color(1f, 0.95f, 0.75f, 1f), new Color(1f, 0.55f, 0.15f, 1f), r / spikes);
            });
            mFlash = Cutout("Clarao", flashTex, new Color(1f, 0.85f, 0.6f));

            mParticle = GetOrCreate("Poeira_Impacto", Find("Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit"));
            mParticle.SetColor("_BaseColor", new Color(0.5f, 0.47f, 0.42f, 1f));
            EditorUtility.SetDirty(mParticle);
        }

        // ------------------------------------------------------------------ modelos reais (Armas/Modelos)
        const string GlockModel = RootDir + "/Modelos/Glock17/Glock17_Modelo.fbx";
        const string GlockTex = RootDir + "/Modelos/Glock17/Texturas/";
        static Material mGlock, mGlockSights, mAmmo, mAK, mCase762, mBullet762;
        const string AKModel = RootDir + "/Modelos/AK47/AK47_Modelo.fbx";
        const string AKTex = RootDir + "/Modelos/AK47/Texturas/";
        const string Ammo762 = RootDir + "/Modelos/Municao762/Municao_762.fbx";
        const string Ammo762Tex = RootDir + "/Modelos/Municao762/Texturas/";
        static bool HasAKModel => AssetDatabase.LoadAssetAtPath<GameObject>(AKModel) != null;
        static bool Has762 => AssetDatabase.LoadAssetAtPath<GameObject>(Ammo762) != null;

        static void BakeAxisOf(string path)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi != null && !mi.bakeAxisConversion) { mi.bakeAxisConversion = true; mi.SaveAndReimport(); }
        }
        static void NormalMap(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        }
        static void CreateAKMaterials()
        {
            if (HasAKModel)
            {
                BakeAxisOf(AKModel);
                NormalMap(AKTex + "AK47_Normal.png");
                mAK = Lit("AK47_Modelo", Color.white, 1f, 1f);
                mAK.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AKTex + "AK47_Base.png"));
                var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(AKTex + "AK47_MetalSmooth.png");
                if (ms) { mAK.SetTexture("_MetallicGlossMap", ms); mAK.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                var nm = AssetDatabase.LoadAssetAtPath<Texture2D>(AKTex + "AK47_Normal.png");
                if (nm) { mAK.SetTexture("_BumpMap", nm); mAK.EnableKeyword("_NORMALMAP"); }
                var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(AKTex + "AK47_AO.png");
                if (ao) { mAK.SetTexture("_OcclusionMap", ao); mAK.EnableKeyword("_OCCLUSIONMAP"); }
                EditorUtility.SetDirty(mAK);
            }
            if (Has762)
            {
                BakeAxisOf(Ammo762);
                mCase762 = Lit("Capsula_762", Color.white, 0.7f, 0.45f);
                mCase762.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Ammo762Tex + "Capsula762_Base.png"));
                mBullet762 = Lit("Projetil_762", Color.white, 0.8f, 0.5f);
                mBullet762.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Ammo762Tex + "Projetil762_Base.png"));
                EditorUtility.SetDirty(mCase762); EditorUtility.SetDirty(mBullet762);
            }
        }
        const string AmmoModel = RootDir + "/Modelos/Municao9mm/Municao_9mm.fbx";
        const string AmmoTex = RootDir + "/Modelos/Municao9mm/Texturas/";
        static bool HasAmmoModel => AssetDatabase.LoadAssetAtPath<GameObject>(AmmoModel) != null;

        static bool HasGlockModel => AssetDatabase.LoadAssetAtPath<GameObject>(GlockModel) != null;

        static void CreateModelMaterials()
        {
            if (HasAmmoModel)
            {
                var ai = AssetImporter.GetAtPath(AmmoModel) as ModelImporter;
                if (ai != null && !ai.bakeAxisConversion) { ai.bakeAxisConversion = true; ai.SaveAndReimport(); }
                var an = AssetImporter.GetAtPath(AmmoTex + "Municao9mm_Normal.png") as TextureImporter;
                if (an != null && an.textureType != TextureImporterType.NormalMap) { an.textureType = TextureImporterType.NormalMap; an.SaveAndReimport(); }
                mAmmo = Lit("Municao9mm", Color.white, 1f, 1f);
                mAmmo.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AmmoTex + "Municao9mm_Base.png"));
                var ams = AssetDatabase.LoadAssetAtPath<Texture2D>(AmmoTex + "Municao9mm_MetalSmooth.png");
                if (ams) { mAmmo.SetTexture("_MetallicGlossMap", ams); mAmmo.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                var anm = AssetDatabase.LoadAssetAtPath<Texture2D>(AmmoTex + "Municao9mm_Normal.png");
                if (anm) { mAmmo.SetTexture("_BumpMap", anm); mAmmo.EnableKeyword("_NORMALMAP"); }
                EditorUtility.SetDirty(mAmmo);
            }
            if (!HasGlockModel) return;
            var mi = AssetImporter.GetAtPath(GlockModel) as ModelImporter;
            if (mi != null && !mi.bakeAxisConversion) { mi.bakeAxisConversion = true; mi.SaveAndReimport(); }
            string nrmPath = GlockTex + "GLOCK_glock_Normal.png";
            var ti = AssetImporter.GetAtPath(nrmPath) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.maxTextureSize = 2048; ti.SaveAndReimport(); }
            mGlock = Lit("Glock17_Modelo", Color.white, 1f, 1f);
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(GlockTex + "GLOCK_glock_AlbedoTransparency.png");
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(GlockTex + "GLOCK_glock_MetallicSmoothness.png");
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(nrmPath);
            if (alb) mGlock.SetTexture("_BaseMap", alb);
            if (ms) { mGlock.SetTexture("_MetallicGlossMap", ms); mGlock.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            if (nrm) { mGlock.SetTexture("_BumpMap", nrm); mGlock.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(mGlock);
            mGlockSights = Unlit("Glock17_Miras", new Color(0.75f, 1f, 0.75f));
        }

        /// <summary>Copia uma peça (objeto filho) de um FBX para dentro de 'parent', com os materiais certos.</summary>
        static GameObject ModelPart(string fbxPath, string partName, Transform parent, Material overrideMat = null, Material altMat = null, string altKey = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (asset == null) return null;
            Transform src = null;
            foreach (var t in asset.GetComponentsInChildren<Transform>(true))
                if (t.name == partName) { src = t; break; }
            if (src == null) { Debug.LogWarning("[Armas] Peça não encontrada no FBX: " + partName); return null; }
            var go = Object.Instantiate(src.gameObject);
            go.name = partName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            // os FBX exportados do Blender entram no Unity virados para -Z: rodar 180° para a frente ficar em +Z
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = src.lossyScale;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = (altKey != null && mats[i] != null && mats[i].name.ToLowerInvariant().Contains(altKey)) ? altMat
                            : overrideMat != null ? overrideMat
                            : (mats[i] != null && mats[i].name.ToLowerInvariant().Contains("lambert4")) ? mGlockSights : mGlock;
                r.sharedMaterials = mats;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            return go;
        }

        // ------------------------------------------------------------------ ajudantes
        static GameObject Save(GameObject go)
        {
            string path = $"{PrefabDir}/{go.name}.prefab";
            var asset = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return asset;
        }

        static Transform Empty(string name, Transform parent, Vector3 pos, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            return go.transform;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (!keepCollider && col) Object.DestroyImmediate(col);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Box(string n, Transform p, Vector3 pos, Vector3 size, Material m, Vector3 euler = default)
            => Prim(PrimitiveType.Cube, n, p, pos, size, euler, m);

        /// <summary>Cilindro deitado ao longo do eixo Z (cano, tubo...).</summary>
        static GameObject CylZ(string n, Transform p, Vector3 pos, float dia, float len, Material m)
            => Prim(PrimitiveType.Cylinder, n, p, pos, new Vector3(dia, len * 0.5f, dia), new Vector3(90, 0, 0), m);

        static GameObject Ball(string n, Transform p, Vector3 pos, float dia, Material m)
            => Prim(PrimitiveType.Sphere, n, p, pos, Vector3.one * dia, Vector3.zero, m);

        static BoxCollider BoxCol(string n, Transform p, Vector3 pos, Vector3 size, Vector3 euler = default)
        {
            var t = Empty(n, p, pos, euler);
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.size = size;
            return c;
        }

        static Rigidbody AddRb(GameObject go, float mass, CollisionDetectionMode mode = CollisionDetectionMode.ContinuousSpeculative)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = mode;
            return rb;
        }

        static void SetupGrab(XRGrabInteractable g, Transform attach, IEnumerable<Collider> cols, XRBaseInteractable.MovementType movement)
        {
            g.movementType = movement;
            g.throwOnDetach = true;
            g.attachTransform = attach;
            g.useDynamicAttach = false;
            g.colliders.Clear();
            g.colliders.AddRange(cols);
        }

        static void MuzzleFlash(Transform muzzle, float size, out GameObject flash, out Light light)
        {
            var f = Empty("Clarao", muzzle, Vector3.zero);
            var q1 = Prim(PrimitiveType.Quad, "Frente", f, new Vector3(0, 0, 0.004f), new Vector3(size, size, 1), Vector3.zero, mFlash);
            var q2 = Prim(PrimitiveType.Quad, "Lado1", f, new Vector3(0, 0, size * 0.6f), new Vector3(size * 1.3f, size * 0.5f, 1), new Vector3(0, 90, 0), mFlash);
            var q3 = Prim(PrimitiveType.Quad, "Lado2", f, new Vector3(0, 0, size * 0.6f), new Vector3(size * 1.3f, size * 0.5f, 1), new Vector3(0, 90, 90), mFlash);
            foreach (var q in new[] { q1, q2, q3 }) q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            flash = f.gameObject;
            var lt = Empty("Luz_Disparo", muzzle, new Vector3(0, 0, 0.05f));
            light = lt.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.7f, 0.35f);
            light.range = 6f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            flash.SetActive(false);
        }

        // ------------------------------------------------------------------ munição / efeitos
        static GameObject BuildCasingModel(string name, bool live)
        {
            var root = new GameObject(name);
            ModelPart(AmmoModel, live ? "Bala_9mm" : "Capsula_9mm", root.transform, mAmmo);
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.radius = 0.0049f;
            col.height = live ? 0.0297f : 0.0192f;
            col.contactOffset = 0.001f;
            AddRb(root, live ? 0.012f : 0.007f, CollisionDetectionMode.ContinuousDynamic);
            root.AddComponent<Casing>();
            return root;
        }

        static GameObject BuildCasing(string name, float dia, float len, bool live)
        {
            if (Mathf.Abs(dia - 0.0099f) < 0.0001f && HasAmmoModel) return BuildCasingModel(name, live);
            var root = Prim(PrimitiveType.Cylinder, name, null, Vector3.zero, new Vector3(dia, len * 0.5f, dia), Vector3.zero, mBrass, true);
            if (live)
            {
                float half = len * 0.5f;
                Prim(PrimitiveType.Sphere, "Ponta", root.transform, new Vector3(0, (half + dia * 0.35f) / half, 0),
                    new Vector3(0.92f, dia * 1.5f / half, 0.92f), Vector3.zero, mCopper);
            }
            var col = root.GetComponent<Collider>();
            col.contactOffset = 0.001f;
            AddRb(root, live ? 0.012f : 0.007f, CollisionDetectionMode.ContinuousDynamic);
            root.AddComponent<Casing>();
            return root;
        }

        static GameObject BuildBulletHole()
        {
            var q = Prim(PrimitiveType.Quad, "BuracoBala", null, Vector3.zero, Vector3.one, Vector3.zero, mHole);
            q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return q;
        }

        static GameObject BuildImpactFx()
        {
            var go = new GameObject("Impacto_Poeira");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.03f);
            main.startColor = new Color(0.55f, 0.5f, 0.45f, 1f);
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.maxParticles = 30;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 25f;
            sh.radius = 0.005f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mParticle;
            return go;
        }

        // ------------------------------------------------------------------ carregadores
        static GameObject BuildGlockMag()
        {
            bool real = HasGlockModel;
            var root = new GameObject("Carregador_Glock17");
            var R = root.transform;
            if (real) ModelPart(GlockModel, "Glock_Carregador", R);
            else
            {
                Box("Corpo", R, new Vector3(0, -0.054f, 0), new Vector3(0.022f, 0.108f, 0.031f), mPolymer);
                Box("Base", R, new Vector3(0, -0.106f, 0), new Vector3(0.026f, 0.008f, 0.035f), mPolymer);
            }
            var top = Empty("BalaTopo", R, Vector3.zero);
            if (HasAmmoModel)
            {
                var r9 = ModelPart(AmmoModel, "Bala_9mm", top, mAmmo);
                if (r9) r9.transform.localPosition = new Vector3(0, -0.006f, 0.001f);
            }
            else
            {
                CylZ("Casquilho", top, new Vector3(0, -0.004f, -0.003f), 0.0099f, 0.019f, mBrass);
                Ball("Ponta", top, new Vector3(0, -0.004f, 0.008f), 0.0092f, mCopper);
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = real ? new Vector3(0, -0.055f, 0.0015f) : new Vector3(0, -0.056f, 0);
            col.size = real ? new Vector3(0.03f, 0.115f, 0.047f) : new Vector3(0.024f, 0.112f, 0.033f);
            AddRb(root, 0.3f);
            var mag = root.AddComponent<Magazine>();
            mag.magazineType = "Glock17";
            mag.capacity = 17;
            mag.rounds = 17;
            mag.topRoundVisual = top.gameObject;
            // a mão agarra a base do carregador (o topo fica para cima, pronto a entrar no punho)
            SetupGrab(mag, Empty("Attach", R, new Vector3(0, -0.1f, 0)), new Collider[] { col }, XRBaseInteractable.MovementType.Instantaneous);
            root.AddComponent<HolsterItem>().slot = "Mag_Glock17";
            root.AddComponent<IgnorePlayerCollision>();
            return root;
        }

        static GameObject BuildStanagMag()
        {
            var root = new GameObject("Carregador_STANAG");
            var R = root.transform;
            Box("Corpo_Cima", R, new Vector3(0, -0.05f, 0), new Vector3(0.022f, 0.1f, 0.064f), mPmag);
            Box("Corpo_Curva", R, new Vector3(0, -0.14f, 0.01f), new Vector3(0.022f, 0.095f, 0.062f), mPmag, new Vector3(-12, 0, 0));
            Box("Base", R, new Vector3(0, -0.19f, 0.021f), new Vector3(0.025f, 0.01f, 0.068f), mPmag, new Vector3(-12, 0, 0));
            var top = Empty("BalaTopo", R, Vector3.zero);
            CylZ("Casquilho", top, new Vector3(0, -0.004f, -0.008f), 0.0096f, 0.045f, mBrass);
            Ball("Ponta", top, new Vector3(0, -0.004f, 0.019f), 0.0085f, mCopper);
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, -0.1f, 0.005f);
            col.size = new Vector3(0.024f, 0.2f, 0.066f);
            AddRb(root, 0.45f);
            var mag = root.AddComponent<Magazine>();
            mag.magazineType = "STANAG";
            mag.capacity = 30;
            mag.rounds = 30;
            mag.topRoundVisual = top.gameObject;
            SetupGrab(mag, Empty("Attach", R, new Vector3(0, -0.1f, 0)), new Collider[] { col }, XRBaseInteractable.MovementType.Instantaneous);
            root.AddComponent<HolsterItem>().slot = "Mag_STANAG";
            root.AddComponent<IgnorePlayerCollision>();
            return root;
        }

        // ------------------------------------------------------------------ GLOCK 17 (186 mm, 905 g carregada)
        // Se existir Assets/Armas/Modelos/Glock17/Glock17_Modelo.fbx usa o modelo real (Sketchfab),
        // senão usa a versão feita de cubos.
        static GameObject BuildGlock(GameObject casing, GameObject live, GameObject hole, GameObject fx)
        {
            bool real = HasGlockModel;
            var root = new GameObject("Glock17");
            var R = root.transform;

            // Medidas tiradas do modelo real (ângulo do punho 18°) ou da versão de cubos (22°)
            var gripEuler = real ? new Vector3(18.1f, 0, 0) : new Vector3(22, 0, 0);
            var gripPos = real ? new Vector3(0, -0.0666f, 0.0352f) : new Vector3(0, -0.07f, 0.032f);
            var wellPos = real ? new Vector3(0, -0.1142f, 0.0196f) : new Vector3(0, -0.121f, 0.0114f);
            var trigPos = real ? new Vector3(0, -0.0134f, 0.0845f) : new Vector3(0, -0.033f, 0.088f);
            float muzzleZ = real ? 0.198f : 0.19f;

            // a luva tem a palma 2 cm abaixo e 5 cm atrás do ponto do comando: o punho fica na palma
            var attach = Empty("Attach_Mao", R, real ? new Vector3(0, -0.047f, 0.085f) : new Vector3(0, -0.045f, 0.08f));
            var cols = real
                ? new List<Collider>
                {
                    BoxCol("Col_Punho", R, gripPos, new Vector3(0.034f, 0.105f, 0.058f), gripEuler),
                    BoxCol("Col_Armacao", R, new Vector3(0, -0.033f, 0.147f), new Vector3(0.034f, 0.054f, 0.092f)),
                    BoxCol("Col_Frente", R, new Vector3(0, 0, 0.15f), new Vector3(0.028f, 0.04f, 0.09f)),
                }
                : new List<Collider>
                {
                    BoxCol("Col_Punho", R, gripPos, new Vector3(0.032f, 0.11f, 0.052f), gripEuler),
                    BoxCol("Col_Armacao", R, new Vector3(0, -0.023f, 0.1f), new Vector3(0.03f, 0.022f, 0.15f)),
                    BoxCol("Col_Frente", R, new Vector3(0, 0, 0.14f), new Vector3(0.03f, 0.03f, 0.09f)),
                };

            var pivot = Empty("RecoilPivot", R, gripPos);
            var M = Empty("Modelo", pivot, -gripPos);
            var trig = Empty("Gatilho", M, trigPos);
            var slideT = Empty("Slide", M, Vector3.zero);

            if (real)
            {
                ModelPart(GlockModel, "Glock_Armacao", M);
                ModelPart(GlockModel, "Glock_Gatilho", trig);
                ModelPart(GlockModel, "Glock_Slide", slideT);
            }
            else
            {
                Box("Armacao", M, new Vector3(0, -0.023f, 0.098f), new Vector3(0.0285f, 0.02f, 0.15f), mPolymer);
                Box("Punho", M, gripPos, new Vector3(0.029f, 0.105f, 0.05f), mPolymer, gripEuler);
                Box("Guarda_Baixo", M, new Vector3(0, -0.056f, 0.085f), new Vector3(0.009f, 0.004f, 0.05f), mPolymer);
                Box("Guarda_Frente", M, new Vector3(0, -0.042f, 0.108f), new Vector3(0.009f, 0.03f, 0.004f), mPolymer);
                Box("Trilho", M, new Vector3(0, -0.035f, 0.15f), new Vector3(0.022f, 0.006f, 0.04f), mPolymer);
                Box("Lamina", trig, new Vector3(0, -0.01f, 0), new Vector3(0.006f, 0.02f, 0.005f), mPolymer);
                CylZ("Boca_Cano", M, new Vector3(0, 0, 0.1866f), 0.011f, 0.001f, mDark);
                Box("Slide_Corpo", slideT, new Vector3(0, 0, 0.093f), new Vector3(0.0255f, 0.029f, 0.186f), mSlide);
                Box("Mira_Tras", slideT, new Vector3(0, 0.0175f, 0.012f), new Vector3(0.02f, 0.006f, 0.006f), mSlide);
                Box("Mira_Frente", slideT, new Vector3(0, 0.0175f, 0.178f), new Vector3(0.004f, 0.006f, 0.004f), mSlide);
                Box("Janela_Ejecao", slideT, new Vector3(0.0125f, 0.009f, 0.115f), new Vector3(0.002f, 0.008f, 0.03f), mDark);
                for (int i = 0; i < 6; i++)
                {
                    Box("Estria_D" + i, slideT, new Vector3(0.0129f, 0, 0.012f + i * 0.005f), new Vector3(0.0008f, 0.022f, 0.002f), mDark);
                    Box("Estria_E" + i, slideT, new Vector3(-0.0129f, 0, 0.012f + i * 0.005f), new Vector3(0.0008f, 0.022f, 0.002f), mDark);
                }
            }

            // Slide (agarra a parte de trás com a outra mão e puxa)
            var slideCol = slideT.gameObject.AddComponent<BoxCollider>();
            slideCol.center = real ? new Vector3(0, 0f, 0.05f) : new Vector3(0, 0.002f, 0.045f);
            slideCol.size = real ? new Vector3(0.036f, 0.04f, 0.1f) : new Vector3(0.036f, 0.036f, 0.09f);
            var slideInter = slideT.gameObject.AddComponent<XRSimpleInteractable>();
            slideInter.colliders.Clear();
            slideInter.colliders.Add(slideCol);
            var sh = slideT.gameObject.AddComponent<SlideHandle>();
            sh.movingPart = slideT;
            sh.pullDirection = Vector3.back;
            sh.travel = 0.05f;
            sh.reciprocatesOnFire = true;
            sh.cycleDuration = 0.045f;

            var muzzle = Empty("Boca", M, new Vector3(0, 0, muzzleZ));
            MuzzleFlash(muzzle, 0.06f, out var flash, out var light);
            var eject = Empty("Ejecao", M, new Vector3(0.014f, 0.01f, 0.115f));

            // Encaixe do carregador (posição do ponto de agarrar do carregador quando está metido)
            var wellT = Empty("Encaixe_Carregador", M, wellPos, gripEuler);
            var sc = wellT.gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.045f;
            sc.center = new Vector3(0, 0.02f, 0);
            var well = wellT.gameObject.AddComponent<MagazineWell>();
            well.magazineType = "Glock17";
            well.attachTransform = wellT;
            well.showInteractableHoverMeshes = false;

            AddRb(root, 0.9f);
            var grab = root.AddComponent<XRGrabInteractable>();
            SetupGrab(grab, attach, cols, XRBaseInteractable.MovementType.Instantaneous);

            var fw = root.AddComponent<Firearm>();
            fw.weaponName = "Glock 17";
            fw.damage = 25f;
            fw.muzzleVelocity = 375f;
            fw.roundsPerMinute = 1100f;
            fw.spreadDegrees = 0.12f;
            fw.bulletHoleSize = 0.012f;
            fw.fireModes = new[] { Firearm.FireMode.Semi }; // a Glock não tem patilha de segurança manual
            fw.fireModeIndex = 0;
            fw.triggerVisual = trig;
            fw.muzzle = muzzle;
            fw.ejectionPort = eject;
            fw.recoilPivot = pivot;
            fw.magazineWell = well;
            fw.slide = sh;
            fw.recoilBack = 0.03f;
            fw.recoilRise = 9f;
            fw.recoilYaw = 2f;
            fw.recoilRoll = 2.5f;
            fw.twoHandRecoilMultiplier = 0.45f;
            fw.recoverTime = 0.1f;
            fw.muzzleFlash = flash;
            fw.muzzleLight = light;
            fw.casingPrefab = casing;
            fw.liveRoundPrefab = live;
            fw.bulletHolePrefab = hole;
            fw.impactFxPrefab = fx;
            fw.shotPitch = 1.1f;
            fw.shotBody = 0.7f;
            fw.shotVolume = 0.9f;
            sh.firearm = fw;
            well.firearm = fw;

            root.AddComponent<HolsterItem>().slot = "Pistol";
            root.AddComponent<IgnorePlayerCollision>();
            return root;
        }

        // ------------------------------------------------------------------ M4A1 (cano 14.5", ~3 kg)
        static GameObject BuildM4(GameObject casing, GameObject live, GameObject hole, GameObject fx)
        {
            var root = new GameObject("M4A1");
            var R = root.transform;
            var gripPos = new Vector3(0, -0.08f, -0.055f);
            var gripEuler = new Vector3(25, 0, 0);

            var attach = Empty("Attach_Mao", R, new Vector3(0, -0.075f, -0.053f));
            var attach2 = Empty("Attach_GuardaMao", R, new Vector3(0, -0.035f, 0.2f));
            var cols = new List<Collider>
            {
                BoxCol("Col_Punho", R, gripPos, new Vector3(0.032f, 0.1f, 0.04f), gripEuler),
                BoxCol("Col_Receptor", R, new Vector3(0, -0.005f, 0.0125f), new Vector3(0.035f, 0.07f, 0.175f)),
                BoxCol("Col_Coronha", R, new Vector3(0, -0.02f, -0.225f), new Vector3(0.04f, 0.1f, 0.19f)),
                BoxCol("Col_GuardaMao", R, new Vector3(0, 0, 0.19f), new Vector3(0.056f, 0.056f, 0.18f)),
                BoxCol("Col_Cano", R, new Vector3(0, 0, 0.39f), new Vector3(0.025f, 0.025f, 0.23f)),
                BoxCol("Col_PocoCarregador", R, new Vector3(0, -0.06f, 0.045f), new Vector3(0.034f, 0.05f, 0.075f)),
            };

            var pivot = Empty("RecoilPivot", R, gripPos);
            var M = Empty("Modelo", pivot, -gripPos);

            Box("Receptor_Superior", M, new Vector3(0, 0.012f, 0), new Vector3(0.03f, 0.04f, 0.2f), mAnod);
            Box("Trilho_Topo", M, new Vector3(0, 0.036f, 0), new Vector3(0.021f, 0.008f, 0.2f), mAnod);
            Box("Receptor_Inferior", M, new Vector3(0, -0.025f, -0.005f), new Vector3(0.026f, 0.035f, 0.17f), mAnod);
            Box("Poco_Carregador", M, new Vector3(0, -0.06f, 0.045f), new Vector3(0.032f, 0.05f, 0.075f), mAnod);
            Box("Punho", M, gripPos, new Vector3(0.028f, 0.095f, 0.033f), mPolymer, gripEuler);
            Box("Guarda_Gatilho", M, new Vector3(0, -0.065f, -0.005f), new Vector3(0.008f, 0.004f, 0.06f), mAnod);
            var trig = Empty("Gatilho", M, new Vector3(0, -0.043f, -0.012f));
            Box("Lamina", trig, new Vector3(0, -0.01f, 0), new Vector3(0.005f, 0.02f, 0.005f), mMetal);
            CylZ("Tubo_Amortecedor", M, new Vector3(0, -0.003f, -0.18f), 0.03f, 0.16f, mAnod);
            Box("Coronha", M, new Vector3(0, -0.022f, -0.25f), new Vector3(0.038f, 0.075f, 0.14f), mPolymer);
            Box("Coronha_Apoio", M, new Vector3(0, -0.03f, -0.318f), new Vector3(0.04f, 0.11f, 0.015f), mRubber);
            CylZ("Guarda_Mao", M, new Vector3(0, 0, 0.19f), 0.052f, 0.18f, mPolymer);
            CylZ("Anel_Delta", M, new Vector3(0, 0, 0.104f), 0.058f, 0.012f, mAnod);
            CylZ("Cano", M, new Vector3(0, 0, 0.38f), 0.016f, 0.2f, mAnod);
            CylZ("Quebra_Chamas", M, new Vector3(0, 0, 0.4825f), 0.022f, 0.045f, mAnod);
            CylZ("Boca_Cano", M, new Vector3(0, 0, 0.5052f), 0.006f, 0.001f, mDark);
            Box("Bloco_MiraFrente", M, new Vector3(0, 0.027f, 0.33f), new Vector3(0.02f, 0.056f, 0.016f), mAnod);
            Box("Poste_MiraFrente", M, new Vector3(0, 0.058f, 0.33f), new Vector3(0.003f, 0.012f, 0.003f), mAnod);
            Box("Mira_Tras", M, new Vector3(0, 0.051f, -0.08f), new Vector3(0.02f, 0.022f, 0.012f), mAnod);
            Box("Janela_Ejecao", M, new Vector3(0.0152f, 0.01f, 0.02f), new Vector3(0.002f, 0.012f, 0.035f), mDark);

            // Mira red dot (tubo oco para conseguires ver através)
            var c = new Vector3(0, 0.068f, 0f);
            Box("RedDot_Base", M, new Vector3(0, 0.045f, 0), new Vector3(0.02f, 0.014f, 0.04f), mAnod);
            Box("RedDot_Cima", M, c + new Vector3(0, 0.0165f, 0), new Vector3(0.036f, 0.003f, 0.045f), mAnod);
            Box("RedDot_Baixo", M, c - new Vector3(0, 0.0165f, 0), new Vector3(0.036f, 0.003f, 0.045f), mAnod);
            Box("RedDot_Esq", M, c - new Vector3(0.0165f, 0, 0), new Vector3(0.003f, 0.03f, 0.045f), mAnod);
            Box("RedDot_Dir", M, c + new Vector3(0.0165f, 0, 0), new Vector3(0.003f, 0.03f, 0.045f), mAnod);
            var dot = Ball("Ponto", M, c + new Vector3(0, 0, 0.02f), 0.0022f, mRedDot);
            dot.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Patilha de seleção (Seguro / Semi / Auto)
            var sel = Empty("Seletor", M, new Vector3(-0.0145f, -0.022f, -0.03f));
            Box("Patilha", sel, new Vector3(0, 0, 0.006f), new Vector3(0.003f, 0.004f, 0.016f), mAnod);

            // Ferrolho visível pela janela (vai para trás quando trava aberto)
            var bolt = Box("Ferrolho", M, new Vector3(0.0156f, 0.01f, 0.02f), new Vector3(0.001f, 0.01f, 0.03f), mMetal);

            // Alavanca de carregar (charging handle)
            var ch = Empty("Alavanca_Carregar", M, new Vector3(0, 0.03f, -0.1f));
            Box("Haste", ch, Vector3.zero, new Vector3(0.012f, 0.008f, 0.03f), mAnod);
            Box("Pega_T", ch, new Vector3(0, 0, -0.012f), new Vector3(0.05f, 0.008f, 0.01f), mAnod);
            var chCol = ch.gameObject.AddComponent<BoxCollider>();
            chCol.center = new Vector3(0, 0, -0.008f);
            chCol.size = new Vector3(0.06f, 0.025f, 0.04f);
            var chInter = ch.gameObject.AddComponent<XRSimpleInteractable>();
            chInter.colliders.Clear();
            chInter.colliders.Add(chCol);
            var sh = ch.gameObject.AddComponent<SlideHandle>();
            sh.movingPart = ch;
            sh.pullDirection = Vector3.back;
            sh.travel = 0.075f;
            sh.reciprocatesOnFire = false;
            sh.boltVisual = bolt.transform;
            sh.boltTravel = 0.045f;
            sh.cycleDuration = 0.06f;

            var muzzle = Empty("Boca", M, new Vector3(0, 0, 0.51f));
            MuzzleFlash(muzzle, 0.09f, out var flash, out var light);
            var eject = Empty("Ejecao", M, new Vector3(0.017f, 0.01f, 0.02f));

            var wellT = Empty("Encaixe_Carregador", M, new Vector3(0, -0.14f, 0.045f));
            var sc = wellT.gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.05f;
            sc.center = new Vector3(0, 0.05f, 0);
            var well = wellT.gameObject.AddComponent<MagazineWell>();
            well.magazineType = "STANAG";
            well.attachTransform = wellT;
            well.showInteractableHoverMeshes = false;

            AddRb(root, 3.0f);
            var grab = root.AddComponent<XRGrabInteractable>();
            SetupGrab(grab, attach, cols, XRBaseInteractable.MovementType.Instantaneous);
            grab.selectMode = InteractableSelectMode.Multiple; // duas mãos
            grab.secondaryAttachTransform = attach2;

            var fw = root.AddComponent<Firearm>();
            fw.weaponName = "M4A1";
            fw.damage = 40f;
            fw.muzzleVelocity = 880f;
            fw.roundsPerMinute = 800f;
            fw.spreadDegrees = 0.05f;
            fw.bulletHoleSize = 0.009f;
            fw.fireModes = new[] { Firearm.FireMode.Safe, Firearm.FireMode.Semi, Firearm.FireMode.Auto };
            fw.fireModeIndex = 1;
            fw.selectorVisual = sel;
            fw.selectorAngles = new[] { 0f, 90f, 180f };
            fw.triggerVisual = trig;
            fw.muzzle = muzzle;
            fw.ejectionPort = eject;
            fw.recoilPivot = pivot;
            fw.magazineWell = well;
            fw.slide = sh;
            fw.recoilBack = 0.018f;
            fw.recoilRise = 3f;
            fw.recoilYaw = 1f;
            fw.recoilRoll = 1f;
            fw.twoHandRecoilMultiplier = 0.35f;
            fw.recoverTime = 0.08f;
            fw.muzzleFlash = flash;
            fw.muzzleLight = light;
            fw.casingPrefab = casing;
            fw.liveRoundPrefab = live;
            fw.bulletHolePrefab = hole;
            fw.impactFxPrefab = fx;
            fw.shotPitch = 0.85f;
            fw.shotBody = 1f;
            fw.shotVolume = 1f;
            sh.firearm = fw;
            well.firearm = fw;

            root.AddComponent<HolsterItem>().slot = "Rifle";
            root.AddComponent<IgnorePlayerCollision>();
            return root;
        }

        static GameObject BuildCasingModel762(string name, bool live)
        {
            var root = new GameObject(name);
            if (Has762) ModelPart(Ammo762, live ? "Bala_762" : "Capsula_762", root.transform, mCase762, mBullet762, "lambert3");
            else CylZ("Casquilho", root.transform, Vector3.zero, 0.0112f, live ? 0.056f : 0.039f, mBrass);
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 2; col.radius = 0.0056f; col.height = live ? 0.056f : 0.039f; col.contactOffset = 0.001f;
            AddRb(root, live ? 0.016f : 0.007f, CollisionDetectionMode.ContinuousDynamic);
            root.AddComponent<Casing>();
            return root;
        }

        // ------------------------------------------------------------------ CARREGADOR AK (30 x 7.62x39, curvo)
        static GameObject BuildAKMag()
        {
            var root = new GameObject("Carregador_AK");
            var R = root.transform;
            ModelPart(AKModel, "AK_Carregador", R, mAK);
            var top = Empty("BalaTopo", R, Vector3.zero);
            if (Has762)
            {
                var r = ModelPart(Ammo762, "Bala_762", top, mCase762, mBullet762, "lambert3");
                if (r) r.transform.localPosition = new Vector3(0, -0.007f, 0.004f);
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, -0.095f, 0.03f);
            col.size = new Vector3(0.032f, 0.2f, 0.085f);
            AddRb(root, 0.33f);
            var mag = root.AddComponent<Magazine>();
            mag.magazineType = "AK762";
            mag.capacity = 30;
            mag.rounds = 30;
            mag.topRoundVisual = top.gameObject;
            SetupGrab(mag, Empty("Attach", R, new Vector3(0, -0.1f, 0.02f)), new Collider[] { col }, XRBaseInteractable.MovementType.Instantaneous);
            root.AddComponent<HolsterItem>().slot = "Mag_AK";
            root.AddComponent<IgnorePlayerCollision>();
            return root;
        }

        // ------------------------------------------------------------------ AK-47 (880 mm, ~3.6 kg carregada)
        static GameObject BuildAK(GameObject casing, GameObject live, GameObject hole, GameObject fx)
        {
            var root = new GameObject("AK47");
            var R = root.transform;
            var gripEuler = new Vector3(17.1f, 0, 0);
            var gripPos = new Vector3(0, -0.0953f, -0.1641f);
            // palma da luva: 2 cm abaixo e 5 cm atrás do ponto do comando
            var attach = Empty("Attach_Mao", R, gripPos + new Vector3(0, 0.02f, 0.05f));
            var attach2 = Empty("Attach_GuardaMao", R, new Vector3(0, -0.03f, 0.17f));
            var cols = new List<Collider>
            {
                BoxCol("Col_Punho", R, gripPos, new Vector3(0.035f, 0.11f, 0.05f), gripEuler),
                BoxCol("Col_Receptor", R, new Vector3(0, 0.008f, -0.07f), new Vector3(0.044f, 0.075f, 0.26f)),
                BoxCol("Col_GuardaMao_Cano", R, new Vector3(0, 0.0f, 0.27f), new Vector3(0.05f, 0.06f, 0.4f)),
                BoxCol("Col_Coronha", R, new Vector3(0, -0.03f, -0.31f), new Vector3(0.045f, 0.12f, 0.2f)),
            };
            var pivot = Empty("RecoilPivot", R, gripPos);
            var M = Empty("Modelo", pivot, -gripPos);
            ModelPart(AKModel, "AK_Armacao", M, mAK);
            var trig = Empty("Gatilho", M, new Vector3(-0.0006f, -0.0322f, -0.1006f));
            ModelPart(AKModel, "AK_Gatilho", trig, mAK);
            var sel = Empty("Seletor", M, new Vector3(0.0212f, -0.009f, -0.1416f));
            ModelPart(AKModel, "AK_Seletor", sel, mAK);

            // alavanca de carregar (lado direito) — na AK recua a cada tiro
            var ch = Empty("Alavanca_Carregar", M, Vector3.zero);
            ModelPart(AKModel, "AK_Alavanca", ch, mAK);
            var chCol = ch.gameObject.AddComponent<BoxCollider>();
            chCol.center = new Vector3(0.04f, 0.018f, 0.045f);
            chCol.size = new Vector3(0.045f, 0.045f, 0.07f);
            var chInter = ch.gameObject.AddComponent<XRSimpleInteractable>();
            chInter.colliders.Clear();
            chInter.colliders.Add(chCol);
            var sh = ch.gameObject.AddComponent<SlideHandle>();
            sh.movingPart = ch;
            sh.pullDirection = Vector3.back;
            sh.travel = 0.11f;
            sh.reciprocatesOnFire = true;
            sh.cycleDuration = 0.07f;

            var muzzle = Empty("Boca", M, new Vector3(0, 0, 0.472f));
            MuzzleFlash(muzzle, 0.1f, out var flash, out var light);
            var eject = Empty("Ejecao", M, new Vector3(0.026f, 0.015f, 0.0f));

            var wellT = Empty("Encaixe_Carregador", M, new Vector3(-0.0006f, -0.1183f, 0.0057f));
            var sc = wellT.gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = 0.06f; sc.center = new Vector3(0, 0.05f, 0);
            var well = wellT.gameObject.AddComponent<MagazineWell>();
            well.magazineType = "AK762";
            well.attachTransform = wellT;
            well.showInteractableHoverMeshes = false;

            AddRb(root, 3.6f);
            var grab = root.AddComponent<XRGrabInteractable>();
            SetupGrab(grab, attach, cols, XRBaseInteractable.MovementType.Instantaneous);
            grab.selectMode = InteractableSelectMode.Multiple;
            grab.secondaryAttachTransform = attach2;

            var fw = root.AddComponent<Firearm>();
            fw.weaponName = "AK-47";
            fw.damage = 45f;
            fw.muzzleVelocity = 715f;
            fw.roundsPerMinute = 600f;
            fw.spreadDegrees = 0.08f;
            fw.bulletHoleSize = 0.009f;
            fw.fireModes = new[] { Firearm.FireMode.Safe, Firearm.FireMode.Auto, Firearm.FireMode.Semi }; // ordem da AK
            fw.fireModeIndex = 2;
            fw.selectorVisual = sel;
            fw.selectorAngles = new[] { 0f, -16f, -32f };
            fw.triggerVisual = trig;
            fw.muzzle = muzzle;
            fw.ejectionPort = eject;
            fw.recoilPivot = pivot;
            fw.magazineWell = well;
            fw.slide = sh;
            fw.lockOpenOnEmpty = false; // a AK não trava aberta no fim do carregador
            fw.recoilBack = 0.025f;
            fw.recoilRise = 4.5f;
            fw.recoilYaw = 1.5f;
            fw.recoilRoll = 1.2f;
            fw.twoHandRecoilMultiplier = 0.35f;
            fw.recoverTime = 0.09f;
            fw.muzzleFlash = flash;
            fw.muzzleLight = light;
            fw.casingPrefab = casing;
            fw.liveRoundPrefab = live;
            fw.bulletHolePrefab = hole;
            fw.impactFxPrefab = fx;
            fw.shotPitch = 0.8f;
            fw.shotBody = 1.15f;
            fw.shotVolume = 1f;
            sh.firearm = fw;
            well.firearm = fw;
            root.AddComponent<HolsterItem>().slot = "Rifle";
            root.AddComponent<IgnorePlayerCollision>();
            return root;
        }

        // ------------------------------------------------------------------ CASSETETE (53 cm)
        static GameObject BuildBaton()
        {
            var root = new GameObject("Cassetete");
            var R = root.transform;
            CylZ("Pega", R, new Vector3(0, 0, 0f), 0.032f, 0.17f, mRubber);
            CylZ("Tampa", R, new Vector3(0, 0, -0.09f), 0.036f, 0.012f, mAnod);
            CylZ("Haste", R, new Vector3(0, 0, 0.265f), 0.022f, 0.36f, mAnod);
            Ball("Ponta", R, new Vector3(0, 0, 0.445f), 0.03f, mAnod);
            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.center = new Vector3(0, 0, 0.18f);
            col.radius = 0.018f;
            col.height = 0.56f;
            AddRb(root, 0.55f, CollisionDetectionMode.ContinuousDynamic);
            var grab = root.AddComponent<XRGrabInteractable>();
            SetupGrab(grab, Empty("Attach_Mao", R, new Vector3(0, 0, 0.02f)), new Collider[] { col }, XRBaseInteractable.MovementType.VelocityTracking);
            var melee = root.AddComponent<MeleeWeapon>();
            melee.tip = Empty("Ponta_Golpe", R, new Vector3(0, 0, 0.45f));
            root.AddComponent<HolsterItem>().slot = "Melee";
            root.AddComponent<IgnorePlayerCollision>();
            return root;
        }

        // ------------------------------------------------------------------ ALVO HUMANO (1.75 m)
        static GameObject BuildDummy()
        {
            var root = new GameObject("Alvo_Humano");
            var R = root.transform;
            var h = root.AddComponent<Health>();
            h.maxHealth = 100f;

            void Zone(GameObject g, float mult)
            {
                var z = g.AddComponent<DamageZone>();
                z.owner = h;
                z.multiplier = mult;
            }

            Zone(Prim(PrimitiveType.Cube, "Perna_E", R, new Vector3(-0.1f, 0.43f, 0), new Vector3(0.15f, 0.86f, 0.17f), Vector3.zero, mDummy, true), 0.6f);
            Zone(Prim(PrimitiveType.Cube, "Perna_D", R, new Vector3(0.1f, 0.43f, 0), new Vector3(0.15f, 0.86f, 0.17f), Vector3.zero, mDummy, true), 0.6f);
            Zone(Prim(PrimitiveType.Cube, "Bacia", R, new Vector3(0, 0.92f, 0), new Vector3(0.38f, 0.14f, 0.22f), Vector3.zero, mDummy, true), 0.9f);
            Zone(Prim(PrimitiveType.Cube, "Tronco", R, new Vector3(0, 1.28f, 0), new Vector3(0.42f, 0.58f, 0.24f), Vector3.zero, mDummy, true), 1f);
            Zone(Prim(PrimitiveType.Cube, "Braco_E", R, new Vector3(-0.28f, 1.26f, 0), new Vector3(0.1f, 0.6f, 0.11f), Vector3.zero, mDummy, true), 0.6f);
            Zone(Prim(PrimitiveType.Cube, "Braco_D", R, new Vector3(0.28f, 1.26f, 0), new Vector3(0.1f, 0.6f, 0.11f), Vector3.zero, mDummy, true), 0.6f);
            Zone(Prim(PrimitiveType.Cylinder, "Pescoco", R, new Vector3(0, 1.6f, 0), new Vector3(0.1f, 0.04f, 0.1f), Vector3.zero, mDummy, true), 2f);
            Zone(Prim(PrimitiveType.Sphere, "Cabeca", R, new Vector3(0, 1.74f, 0), new Vector3(0.2f, 0.24f, 0.22f), Vector3.zero, mDummy, true), 4f);
            return root;
        }

        // ------------------------------------------------------------------ CENA
        static GameObject Inst(GameObject prefab, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            return go;
        }

        static Vector3 Ground(Vector3 world, float fallbackY)
        {
            var from = new Vector3(world.x, fallbackY + 1.0f, world.z);
            if (Physics.Raycast(from, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(world.x, fallbackY, world.z);
        }

        static FilteredSocket Socket(string name, Transform parent, Vector3 pos, Vector3 euler, string slot, float radius, Vector3 visualSize)
        {
            var t = Empty(name, parent, pos, euler);
            var sc = t.gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = radius;
            var s = t.gameObject.AddComponent<FilteredSocket>();
            s.acceptSlot = slot;
            s.attachTransform = t;
            s.showInteractableHoverMeshes = false;
            if (visualSize != Vector3.zero)
            {
                var v = Box(name + "_Visual", parent, pos, visualSize, mGear, euler);
                v.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            return s;
        }

        static void PlaceInScene(GameObject glock, GameObject m4, GameObject baton, GameObject magGlock, GameObject magM4, GameObject dummy)
        {
            var oldArsenal = GameObject.Find("Arsenal (Teste)");
            if (oldArsenal) Undo.DestroyObjectImmediate(oldArsenal);
            var oldRig = GameObject.Find("Equipamento_Corpo");
            if (oldRig) Undo.DestroyObjectImmediate(oldRig);

            var cam = Camera.main;
            Transform origin = cam ? cam.transform.root : null;
            Vector3 basePos = origin ? origin.position : Vector3.zero;
            Vector3 fwd = origin ? Vector3.ProjectOnPlane(origin.forward, Vector3.up) : Vector3.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            var rot = Quaternion.LookRotation(fwd.normalized);

            var group = new GameObject("Arsenal (Teste)");
            group.transform.SetPositionAndRotation(basePos, rot);
            Undo.RegisterCreatedObjectUndo(group, "Arsenal");
            var G = group.transform;

            // Mesa (80 cm de altura)
            var table = Empty("Mesa", G, new Vector3(0, 0, 0.75f));
            Prim(PrimitiveType.Cube, "Tampo", table, new Vector3(0, 0.78f, 0), new Vector3(1.2f, 0.04f, 0.6f), Vector3.zero, mWood, true);
            foreach (var x in new[] { -0.55f, 0.55f })
                foreach (var z in new[] { -0.25f, 0.25f })
                    Prim(PrimitiveType.Cube, "Perna", table, new Vector3(x, 0.38f, z), new Vector3(0.05f, 0.76f, 0.05f), Vector3.zero, mWood, true);

            // M4 em cima da mesa, com carregador metido mas SEM bala na câmara (tens de puxar a alavanca)
            var m4i = Inst(m4, G, new Vector3(-0.05f, 0.84f, 0.8f), Quaternion.Euler(0, 90, 90));
            var m4Mag = Inst(magM4, G, new Vector3(-0.3f, 0.3f, 0.75f), Quaternion.identity);
            var m4Well = m4i.GetComponentInChildren<MagazineWell>();
            m4Well.startingSelectedInteractable = m4Mag.GetComponent<Magazine>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(m4Well);

            // Carregadores extra em cima da mesa
            Inst(magGlock, G, new Vector3(0.25f, 0.93f, 0.98f), Quaternion.identity);
            Inst(magM4, G, new Vector3(0.4f, 1.0f, 0.98f), Quaternion.identity);

            // Equipamento do corpo
            var rig = new GameObject("Equipamento_Corpo");
            Undo.RegisterCreatedObjectUndo(rig, "Equipamento");
            if (origin) rig.transform.SetParent(origin, false);
            rig.AddComponent<PlayerBodyRig>();
            var rrb = rig.AddComponent<Rigidbody>();
            rrb.isKinematic = true;
            rrb.useGravity = false;
            var RR = rig.transform;
            var belt = Prim(PrimitiveType.Cylinder, "Cinto", RR, Vector3.zero, new Vector3(0.34f, 0.025f, 0.26f), Vector3.zero, mGear);
            belt.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var vest = Box("Colete", RR, new Vector3(0, 0.33f, 0.07f), new Vector3(0.32f, 0.32f, 0.06f), mGear);
            vest.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var pistolH = Socket("Coldre_Pistola", RR, new Vector3(0.2f, 0f, 0.02f), new Vector3(90, 0, 0), "Pistol", 0.12f, Vector3.zero);
            Box("Coldre_Pistola_Visual", RR, new Vector3(0.21f, -0.07f, 0.04f), new Vector3(0.045f, 0.15f, 0.05f), mGear);
            var meleeH = Socket("Coldre_Cassetete", RR, new Vector3(-0.21f, 0f, -0.02f), new Vector3(90, 0, 0), "Melee", 0.1f, Vector3.zero);
            Socket("Costas_Espingarda", RR, new Vector3(0.12f, 0.5f, -0.18f), new Vector3(100, 0, 0), "Rifle", 0.15f, Vector3.zero);

            var p1 = Socket("Bolsa_Carregador_Pistola", RR, new Vector3(-0.12f, 0.02f, 0.12f), Vector3.zero, "Mag_Glock17", 0.07f, new Vector3(0.035f, 0.07f, 0.045f));
            var pouchA = p1.gameObject.AddComponent<AmmoPouch>();
            pouchA.magazinePrefab = magGlock.GetComponent<Magazine>();
            pouchA.spareMagazines = 4;
            foreach (var x in new[] { -0.1f, -0.025f })
            {
                var p = Socket("Bolsa_Carregador_Espingarda", RR, new Vector3(x, 0.25f, 0.15f), Vector3.zero, magM4.GetComponent<HolsterItem>().slot, 0.08f, new Vector3(0.035f, 0.1f, 0.08f));
                var pouch = p.gameObject.AddComponent<AmmoPouch>();
                pouch.magazinePrefab = magM4.GetComponent<Magazine>();
                pouch.spareMagazines = 3;
            }

            // Glock no coldre: carregada, com bala na câmara (como se anda com uma Glock)
            var glockI = Inst(glock, G, new Vector3(0.32f, 0.84f, 0.6f), Quaternion.Euler(0, 90, 90));
            var glockMag = Inst(magGlock, G, new Vector3(0.3f, 0.3f, 0.75f), Quaternion.identity);
            var gMag = glockMag.GetComponent<Magazine>();
            gMag.rounds = 16;
            PrefabUtility.RecordPrefabInstancePropertyModifications(gMag);
            var gWell = glockI.GetComponentInChildren<MagazineWell>();
            gWell.startingSelectedInteractable = gMag;
            PrefabUtility.RecordPrefabInstancePropertyModifications(gWell);
            var gFw = glockI.GetComponent<Firearm>();
            gFw.roundChambered = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(gFw);
            // A Glock começa na mesa (à vista). Para começar no coldre, descomenta a linha abaixo.
            // pistolH.startingSelectedInteractable = glockI.GetComponent<XRGrabInteractable>();

            // Cassetete no coldre da esquerda
            var batonI = Inst(baton, G, new Vector3(-0.2f, 0.85f, 0.5f), Quaternion.Euler(0, 90, 90));
            meleeH.startingSelectedInteractable = batonI.GetComponent<XRGrabInteractable>();

            // Alvos humanos a 7, 12 e 20 metros
            var dummies = new[] { new Vector3(-1.5f, 0, 7f), new Vector3(1.0f, 0, 12f), new Vector3(0, 0, 20f) };
            foreach (var d in dummies)
            {
                var world = G.TransformPoint(d);
                var p = Ground(world, basePos.y);
                var di = (GameObject)PrefabUtility.InstantiatePrefab(dummy, G);
                di.transform.SetPositionAndRotation(p, rot * Quaternion.Euler(0, 180, 0));
            }

            // Caixas com latas para acertar
            foreach (var cx in new[] { -2.2f, 2.2f })
            {
                var world = G.TransformPoint(new Vector3(cx, 0, 5f));
                var p = Ground(world, basePos.y);
                var crate = Prim(PrimitiveType.Cube, "Caixa", G, Vector3.zero, new Vector3(0.7f, 0.7f, 0.7f), Vector3.zero, mCrate, true);
                crate.transform.SetPositionAndRotation(p + Vector3.up * 0.35f, rot);
                for (int i = 0; i < 4; i++)
                {
                    var can = Prim(PrimitiveType.Cylinder, "Lata", G, Vector3.zero, new Vector3(0.066f, 0.061f, 0.066f), Vector3.zero, mCan, true);
                    can.transform.SetPositionAndRotation(p + Vector3.up * 0.762f + rot * new Vector3(-0.24f + 0.16f * i, 0, 0), rot);
                    can.GetComponent<Collider>().contactOffset = 0.002f;
                    AddRb(can, 0.05f);
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = group;
        }
    }
}
