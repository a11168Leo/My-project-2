#if UNITY_EDITOR
// =====================================================================
//  CriarCidade.cs  —  Gera uma cidade delimitada para o teu jogo VR
//  Menu: Ferramentas > Cidade > Criar Cidade (Noite) / (Dia)
//  Pode ficar em qualquer pasta dentro de Assets.
//  Podes correr quantas vezes quiseres: apaga a cidade antiga e cria de novo.
//  Ctrl+Z desfaz.
// =====================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CriarCidade
{
    // ---------- Configuração (podes mexer aqui) ----------
    const string PASTA = "Assets/Cidade";
    const string PASTA_MAT = "Assets/Cidade/Materiais";
    const float METADE = 40f;                           // a cidade vai de -40 a 40 (80 x 80 m)
    static readonly float[] ESTRADAS = { -20f, 0f, 20f }; // estradas em X e em Z
    const float LARG_ESTRADA = 8f;
    const int SEMENTE = 2026;                           // muda para gerar outra cidade

    static readonly Color[] CORES = {
        new Color(0.78f, 0.74f, 0.68f), new Color(0.56f, 0.60f, 0.66f), new Color(0.72f, 0.60f, 0.50f),
        new Color(0.46f, 0.48f, 0.52f), new Color(0.84f, 0.80f, 0.74f), new Color(0.62f, 0.50f, 0.46f) };
    static readonly Color[] NEON = {
        new Color(0f, 0.9f, 1f), new Color(1f, 0.1f, 0.8f), new Color(1f, 0.55f, 0f), new Color(0.4f, 1f, 0.3f) };
    static readonly Color[] CORES_CARRO = {
        new Color(0.7f, 0.1f, 0.1f), new Color(0.1f, 0.25f, 0.6f), new Color(0.9f, 0.9f, 0.9f),
        new Color(0.08f, 0.08f, 0.08f), new Color(0.9f, 0.7f, 0.1f) };

    // ---------- Estado interno ----------
    static Transform raiz;
    static bool noite;
    static int contadorPredio;
    static Texture2D texBase, texEmissao;
    static Material matAsfalto, matPasseio, matMuro, matLinha, matTronco, matFolhas, matRelva,
                    matPoste, matLampada, matBanco, matLuzVermelha, matVidroCarro;

    [MenuItem("Ferramentas/Cidade/Criar Cidade (Noite)")]
    static void CriarNoite() { Criar(true); }

    [MenuItem("Ferramentas/Cidade/Criar Cidade (Dia)")]
    static void CriarDia() { Criar(false); }

    // Se existir o ficheiro "GerarCidade.txt" na pasta do projeto, gera a cidade sozinho
    // (escreve "dia" ou "noite" dentro dele). O ficheiro é apagado a seguir.
    [InitializeOnLoadMethod]
    static void VerificarPedidoAutomatico()
    {
        EditorApplication.delayCall += () =>
        {
            string f = Path.Combine(Application.dataPath, "..", "GerarCidade.txt");
            if (!File.Exists(f)) return;
            string modo = File.ReadAllText(f).Trim().ToLowerInvariant();
            File.Delete(f);
            Criar(modo != "dia");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Cidade gerada automaticamente e cena guardada.");
        };
    }

    static void Criar(bool modoNoite)
    {
        noite = modoNoite;
        contadorPredio = 0;
        UnityEngine.Random.InitState(SEMENTE);

        GarantirPastas();
        CriarTexturasJanelas();
        CriarMateriais();

        var antiga = GameObject.Find("Cidade");
        if (antiga != null) Undo.DestroyObjectImmediate(antiga);
        EsconderMundoAntigo();

        var go = new GameObject("Cidade");
        Undo.RegisterCreatedObjectUndo(go, "Criar Cidade");
        raiz = go.transform;

        CriarChaoEMuros();
        CriarQuarteiroes();
        CriarMarcasEstrada();
        CriarCandeeiros();
        CriarCarros();
        ConfigurarIluminacao();
        CriarPosProcessamento();
        MarcarEstatico(raiz);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Cidade criada (" + (noite ? "noite" : "dia") + ")! Carrega Ctrl+S para guardar a cena.");
    }

    // =================================================================
    //  Pastas, texturas e materiais
    // =================================================================
    static void GarantirPastas()
    {
        if (!AssetDatabase.IsValidFolder(PASTA)) AssetDatabase.CreateFolder("Assets", "Cidade");
        if (!AssetDatabase.IsValidFolder(PASTA_MAT)) AssetDatabase.CreateFolder(PASTA, "Materiais");
    }

    // Textura com uma grelha de 8x8 janelas (algumas acesas) que se repete nos prédios
    static void CriarTexturasJanelas()
    {
        const int celulas = 8, px = 8, tam = celulas * px;
        var b = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        var e = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        var rnd = new System.Random(7);
        var parede = Color.white;
        var vidro = new Color(0.18f, 0.21f, 0.26f);
        var luz = new Color(1f, 0.85f, 0.55f);

        for (int cy = 0; cy < celulas; cy++)
            for (int cx = 0; cx < celulas; cx++)
            {
                bool acesa = rnd.NextDouble() < 0.45;
                for (int y = 0; y < px; y++)
                    for (int x = 0; x < px; x++)
                    {
                        bool janela = x >= 2 && x <= 5 && y >= 2 && y <= 6;
                        int X = cx * px + x, Y = cy * px + y;
                        b.SetPixel(X, Y, janela ? vidro : parede);
                        e.SetPixel(X, Y, janela && acesa ? luz : Color.black);
                    }
            }
        texBase = GuardarTextura(b, "Janelas_Base.png");
        texEmissao = GuardarTextura(e, "Janelas_Emissao.png");
    }

    static Texture2D GuardarTextura(Texture2D t, string nome)
    {
        string caminho = PASTA + "/" + nome;
        File.WriteAllBytes(Path.Combine(Application.dataPath, "Cidade", nome), t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
        if (imp != null)
        {
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
    }

    static Shader ShaderLit()
    {
        var s = Shader.Find("Universal Render Pipeline/Lit");
        return s != null ? s : Shader.Find("Standard");
    }

    static Material Mat(string nome, Color cor, float suave, Color? emissao = null)
    {
        string caminho = PASTA_MAT + "/" + nome + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (m == null)
        {
            m = new Material(ShaderLit());
            AssetDatabase.CreateAsset(m, caminho);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", cor);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", suave);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", suave);
        if (emissao.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emissao.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SetTex(Material m, string prop, Texture t)
    {
        if (m.HasProperty(prop)) m.SetTexture(prop, t);
    }

    static void CriarMateriais()
    {
        matAsfalto     = Mat("Asfalto",        new Color(0.13f, 0.13f, 0.14f), 0.35f);
        matPasseio     = Mat("Passeio",        new Color(0.55f, 0.55f, 0.53f), 0.10f);
        matMuro        = Mat("Muro",           new Color(0.42f, 0.40f, 0.38f), 0.05f);
        matLinha       = Mat("Linha Estrada",  new Color(0.95f, 0.92f, 0.80f), 0.10f);
        matTronco      = Mat("Tronco Cidade",  new Color(0.36f, 0.24f, 0.14f), 0.05f);
        matFolhas      = Mat("Folhas Cidade",  new Color(0.18f, 0.42f, 0.20f), 0.05f);
        matRelva       = Mat("Relva Cidade",   new Color(0.24f, 0.45f, 0.20f), 0.02f);
        matPoste       = Mat("Poste",          new Color(0.20f, 0.21f, 0.23f), 0.50f);
        matBanco       = Mat("Banco",          new Color(0.45f, 0.30f, 0.18f), 0.20f);
        matVidroCarro  = Mat("Vidro Carro",    new Color(0.10f, 0.12f, 0.15f), 0.90f);
        matLampada     = Mat("Lampada",        new Color(1f, 0.9f, 0.7f), 0.5f,
                             new Color(1f, 0.8f, 0.5f) * (noite ? 4f : 0.3f));
        matLuzVermelha = Mat("Luz Vermelha",   Color.red, 0.5f,
                             new Color(1f, 0.1f, 0.05f) * (noite ? 5f : 1f));
    }

    // Cada prédio tem o seu material para as janelas ficarem do tamanho certo
    static Material MatPredio(float w, float h)
    {
        contadorPredio++;
        var cor = CORES[UnityEngine.Random.Range(0, CORES.Length)];
        Color? emissao = noite ? (Color?)(Color.white * 1.4f) : null;
        var m = Mat("Predio_" + contadorPredio.ToString("00"), cor, 0.25f, emissao);
        SetTex(m, "_BaseMap", texBase);
        SetTex(m, "_MainTex", texBase);
        SetTex(m, "_EmissionMap", texEmissao);

        // 1 janela a cada ~2,5 m na horizontal e ~3 m na vertical (a textura tem 8x8)
        var escala = new Vector2(Mathf.Max(1f, Mathf.Round(w / 2.5f)) / 8f,
                                 Mathf.Max(1f, Mathf.Round(h / 3f)) / 8f);
        var desloc = new Vector2(UnityEngine.Random.Range(0, 8) / 8f, UnityEngine.Random.Range(0, 8) / 8f);
        foreach (var prop in new[] { "_BaseMap", "_MainTex" })
            if (m.HasProperty(prop))
            {
                m.SetTextureScale(prop, escala);
                m.SetTextureOffset(prop, desloc);
            }
        return m;
    }

    // =================================================================
    //  Ajudantes
    // =================================================================
    static GameObject Bloco(string nome, PrimitiveType tipo, Vector3 pos, Vector3 escala,
                            Material mat, Transform pai, bool colisor = true)
    {
        var g = GameObject.CreatePrimitive(tipo);
        g.name = nome;
        g.transform.SetParent(pai, false);
        g.transform.localPosition = pos;
        g.transform.localScale = escala;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!colisor) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static Transform Grupo(string nome, Transform pai)
    {
        var g = new GameObject(nome);
        g.transform.SetParent(pai, false);
        return g.transform;
    }

    static bool PertoDeCruzamento(float t, float raio)
    {
        foreach (var r in ESTRADAS) if (Mathf.Abs(t - r) < raio) return true;
        return false;
    }

    static float EstradaMaisProxima(float v)
    {
        float melhor = ESTRADAS[0];
        foreach (var r in ESTRADAS) if (Mathf.Abs(v - r) < Mathf.Abs(v - melhor)) melhor = r;
        return melhor;
    }

    // Intervalos entre estradas (onde ficam os quarteirões)
    static List<Vector2> Intervalos()
    {
        var lista = new List<Vector2>();
        float a = -METADE;
        foreach (var r in ESTRADAS)
        {
            lista.Add(new Vector2(a, r - LARG_ESTRADA / 2f));
            a = r + LARG_ESTRADA / 2f;
        }
        lista.Add(new Vector2(a, METADE));
        return lista;
    }

    // Esconde o chão e as árvores que fizemos antes (não apaga nada)
    static void EsconderMundoAntigo()
    {
        foreach (var g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == "Chao" || g.name.StartsWith("Arvore"))
            {
                Undo.RecordObject(g, "Esconder mundo antigo");
                g.SetActive(false);
            }
    }

    // Permite teleportar para esta superfície (XR Interaction Toolkit)
    static void TentarAdicionarTeleporte(GameObject g)
    {
        try
        {
            Type tipo = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea, Unity.XR.Interaction.Toolkit")
                     ?? Type.GetType("UnityEngine.XR.Interaction.Toolkit.TeleportationArea, Unity.XR.Interaction.Toolkit");
            if (tipo == null) return;
            var comp = g.AddComponent(tipo);

            var tipoMascara = Type.GetType("UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask, Unity.XR.Interaction.Toolkit");
            var getMask = tipoMascara != null ? tipoMascara.GetMethod("GetMask", new[] { typeof(string[]) }) : null;
            var prop = tipo.GetProperty("interactionLayers");
            if (getMask == null || prop == null) return;

            object res = getMask.Invoke(null, new object[] { new[] { "Teleport" } });
            if (res is int)
            {
                int valor = (int)res;
                if (valor == 0) return; // camada "Teleport" não existe: fica na Default
                var conv = tipoMascara.GetMethod("op_Implicit", new[] { typeof(int) });
                if (conv != null) prop.SetValue(comp, conv.Invoke(null, new object[] { valor }));
            }
            else prop.SetValue(comp, res);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Teleporte não adicionado em " + g.name + ": " + e.Message);
        }
    }

    static void MarcarEstatico(Transform t)
    {
        foreach (var tr in t.GetComponentsInChildren<Transform>(true))
            if (tr.GetComponent<Light>() == null && tr.GetComponent<Volume>() == null)
                GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.BatchingStatic);
    }

    // =================================================================
    //  Construção
    // =================================================================
    static void CriarChaoEMuros()
    {
        var chao = Bloco("Chao (Asfalto)", PrimitiveType.Cube, new Vector3(0f, -0.1f, 0f),
                         new Vector3(METADE * 2f, 0.2f, METADE * 2f), matAsfalto, raiz);
        TentarAdicionarTeleporte(chao);

        var muros = Grupo("Muros", raiz);
        float h = 8f, t = 1f, L = METADE * 2f + t * 2f;
        Bloco("Muro Norte", PrimitiveType.Cube, new Vector3(0f, h / 2f, METADE + t / 2f), new Vector3(L, h, t), matMuro, muros);
        Bloco("Muro Sul",   PrimitiveType.Cube, new Vector3(0f, h / 2f, -METADE - t / 2f), new Vector3(L, h, t), matMuro, muros);
        Bloco("Muro Este",  PrimitiveType.Cube, new Vector3(METADE + t / 2f, h / 2f, 0f), new Vector3(t, h, L), matMuro, muros);
        Bloco("Muro Oeste", PrimitiveType.Cube, new Vector3(-METADE - t / 2f, h / 2f, 0f), new Vector3(t, h, L), matMuro, muros);
    }

    static void CriarQuarteiroes()
    {
        var pai = Grupo("Quarteiroes", raiz);
        var iv = Intervalos();
        int n = 0;
        foreach (var ix in iv)
            foreach (var iz in iv)
            {
                n++;
                var q = Grupo("Quarteirao " + n, pai);
                float cx = (ix.x + ix.y) / 2f, cz = (iz.x + iz.y) / 2f;
                float w = ix.y - ix.x, d = iz.y - iz.x;

                var passeio = Bloco("Passeio", PrimitiveType.Cube, new Vector3(cx, 0.075f, cz),
                                    new Vector3(w, 0.15f, d), matPasseio, q);
                TentarAdicionarTeleporte(passeio);

                // quarteirões do meio têm prédios mais baixos, os de fora mais altos
                bool centro = Mathf.Abs(cx) < 15f && Mathf.Abs(cz) < 15f;
                const float margem = 1.4f, folga = 1.2f;
                const int nx = 2, nz = 2;
                float lw = (w - margem * 2f - folga * (nx - 1)) / nx;
                float ld = (d - margem * 2f - folga * (nz - 1)) / nz;

                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                    {
                        float px = ix.x + margem + lw / 2f + i * (lw + folga);
                        float pz = iz.x + margem + ld / 2f + j * (ld + folga);
                        var pos = new Vector3(px, 0.15f, pz);
                        if (UnityEngine.Random.value < 0.15f)
                            CriarJardim(q, pos, lw, ld);
                        else
                        {
                            float altura = centro ? UnityEngine.Random.Range(6f, 15f)
                                                  : UnityEngine.Random.Range(10f, 34f);
                            CriarPredio(q, pos, lw, ld, altura);
                        }
                    }
            }
    }

    static void CriarPredio(Transform pai, Vector3 pBase, float w, float d, float h)
    {
        var p = Grupo("Predio", pai);
        p.localPosition = pBase;
        Bloco("Corpo", PrimitiveType.Cube, new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d), MatPredio(w, h), p);
        Bloco("Telhado", PrimitiveType.Cube, new Vector3(0f, h + 0.15f, 0f), new Vector3(w + 0.3f, 0.3f, d + 0.3f), matMuro, p);

        if (UnityEngine.Random.value < 0.6f)
            Bloco("Ar Condicionado", PrimitiveType.Cube,
                  new Vector3(UnityEngine.Random.Range(-w / 4f, w / 4f), h + 0.8f, UnityEngine.Random.Range(-d / 4f, d / 4f)),
                  new Vector3(1.4f, 1f, 1.2f), matPoste, p, false);

        if (h > 22f)
        {
            Bloco("Antena", PrimitiveType.Cylinder, new Vector3(0f, h + 2.3f, 0f), new Vector3(0.12f, 2f, 0.12f), matPoste, p, false);
            Bloco("Luz Antena", PrimitiveType.Sphere, new Vector3(0f, h + 4.4f, 0f), Vector3.one * 0.35f, matLuzVermelha, p, false);
        }

        if (UnityEngine.Random.value < 0.35f) CriarNeon(p, pBase, w, d);
    }

    // Placa luminosa virada para a estrada mais próxima
    static void CriarNeon(Transform p, Vector3 centro, float w, float d)
    {
        float rx = EstradaMaisProxima(centro.x), rz = EstradaMaisProxima(centro.z);
        float dx = Mathf.Abs(centro.x - rx), dz = Mathf.Abs(centro.z - rz);
        var cor = NEON[UnityEngine.Random.Range(0, NEON.Length)];
        var m = Mat("Neon_" + ColorUtility.ToHtmlStringRGB(cor), cor, 0.8f, cor * (noite ? 6f : 1.5f));
        float y = UnityEngine.Random.Range(3.5f, 6f);
        Vector3 pos, esc;
        if (dx < dz)
        {
            float s = Mathf.Sign(rx - centro.x);
            pos = new Vector3(s * (w / 2f + 0.06f), y, 0f);
            esc = new Vector3(0.1f, 1.2f, Mathf.Min(d * 0.7f, 4f));
        }
        else
        {
            float s = Mathf.Sign(rz - centro.z);
            pos = new Vector3(0f, y, s * (d / 2f + 0.06f));
            esc = new Vector3(Mathf.Min(w * 0.7f, 4f), 1.2f, 0.1f);
        }
        Bloco("Placa Neon", PrimitiveType.Cube, pos, esc, m, p, false);
    }

    static void CriarJardim(Transform pai, Vector3 c, float w, float d)
    {
        var j = Grupo("Jardim", pai);
        j.localPosition = c;
        Bloco("Relva", PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(w, 0.1f, d), matRelva, j);
        int n = UnityEngine.Random.Range(1, 3);
        for (int i = 0; i < n; i++)
            CriarArvore(j, new Vector3(UnityEngine.Random.Range(-w / 3f, w / 3f), 0.1f, UnityEngine.Random.Range(-d / 3f, d / 3f)));
        Bloco("Banco", PrimitiveType.Cube, new Vector3(0f, 0.35f, -d / 2f + 0.8f), new Vector3(1.8f, 0.15f, 0.5f), matBanco, j);
    }

    static void CriarArvore(Transform pai, Vector3 pos)
    {
        var a = Grupo("Arvore", pai);
        a.localPosition = pos;
        float s = UnityEngine.Random.Range(0.8f, 1.2f);
        Bloco("Tronco", PrimitiveType.Cylinder, new Vector3(0f, 1.2f * s, 0f), new Vector3(0.3f, 1.2f, 0.3f) * s, matTronco, a);
        Bloco("Copa", PrimitiveType.Sphere, new Vector3(0f, 3f * s, 0f), Vector3.one * 2.4f * s, matFolhas, a, false);
    }

    static void CriarMarcasEstrada()
    {
        var pai = Grupo("Marcas Estrada", raiz);
        foreach (var r in ESTRADAS)
            for (float t = -METADE + 2f; t <= METADE - 2f; t += 4f)
            {
                if (PertoDeCruzamento(t, 5f)) continue;
                Bloco("Traco", PrimitiveType.Cube, new Vector3(t, 0.01f, r), new Vector3(2f, 0.02f, 0.2f), matLinha, pai, false);
                Bloco("Traco", PrimitiveType.Cube, new Vector3(r, 0.01f, t), new Vector3(0.2f, 0.02f, 2f), matLinha, pai, false);
            }
    }

    static void CriarCandeeiros()
    {
        var pai = Grupo("Candeeiros", raiz);
        float lado = LARG_ESTRADA / 2f + 0.6f;
        int i = 0;
        foreach (var r in ESTRADAS)
            for (float t = -METADE + 4f; t <= METADE - 4f; t += 8f)
            {
                if (PertoDeCruzamento(t, 6f)) continue;
                float s = (i++ % 2 == 0) ? 1f : -1f;   // alterna os lados da estrada
                CriarCandeeiro(pai, new Vector3(t, 0.15f, r + s * lado), new Vector3(0f, 0f, -s));
                CriarCandeeiro(pai, new Vector3(r + s * lado, 0.15f, t), new Vector3(-s, 0f, 0f));
            }
    }

    static void CriarCandeeiro(Transform pai, Vector3 pos, Vector3 paraEstrada)
    {
        var c = Grupo("Candeeiro", pai);
        c.localPosition = pos;
        c.localRotation = Quaternion.LookRotation(paraEstrada, Vector3.up);
        Bloco("Poste", PrimitiveType.Cylinder, new Vector3(0f, 2.5f, 0f), new Vector3(0.15f, 2.5f, 0.15f), matPoste, c);
        Bloco("Braco", PrimitiveType.Cube, new Vector3(0f, 4.95f, 0.6f), new Vector3(0.1f, 0.1f, 1.3f), matPoste, c, false);
        Bloco("Lampada", PrimitiveType.Cube, new Vector3(0f, 4.85f, 1.15f), new Vector3(0.35f, 0.12f, 0.6f), matLampada, c, false);

        var luzGo = new GameObject("Luz");
        luzGo.transform.SetParent(c, false);
        luzGo.transform.localPosition = new Vector3(0f, 4.7f, 1.15f);
        luzGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // aponta para baixo
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Spot;
        luz.range = 14f;
        luz.spotAngle = 110f;
        luz.innerSpotAngle = 60f;
        luz.intensity = 15f;
        luz.color = new Color(1f, 0.8f, 0.55f);
        luz.shadows = LightShadows.None;
        luz.lightmapBakeType = LightmapBakeType.Realtime;
        luz.enabled = noite;
    }

    static void CriarCarros()
    {
        var pai = Grupo("Carros", raiz);
        foreach (var r in ESTRADAS)
            for (float t = -METADE + 8f; t <= METADE - 8f; t += 8f)
            {
                if (PertoDeCruzamento(t, 7f)) continue;
                if (UnityEngine.Random.value < 0.35f) CriarCarro(pai, new Vector3(t, 0f, r + 2.6f), 0f);
                if (UnityEngine.Random.value < 0.35f) CriarCarro(pai, new Vector3(r - 2.6f, 0f, t), 90f);
            }
    }

    static void CriarCarro(Transform pai, Vector3 pos, float rotY)
    {
        var c = Grupo("Carro", pai);
        c.localPosition = pos;
        c.localRotation = Quaternion.Euler(0f, rotY, 0f);
        var cor = CORES_CARRO[UnityEngine.Random.Range(0, CORES_CARRO.Length)];
        var m = Mat("Carro_" + ColorUtility.ToHtmlStringRGB(cor), cor, 0.7f);
        Bloco("Carrocaria", PrimitiveType.Cube, new Vector3(0f, 0.65f, 0f), new Vector3(4f, 0.8f, 1.8f), m, c);
        Bloco("Cabine", PrimitiveType.Cube, new Vector3(-0.2f, 1.35f, 0f), new Vector3(2.2f, 0.6f, 1.6f), matVidroCarro, c, false);
        foreach (var x in new[] { -1.3f, 1.3f })
            foreach (var z in new[] { -0.9f, 0.9f })
            {
                var roda = Bloco("Roda", PrimitiveType.Cylinder, new Vector3(x, 0.35f, z), new Vector3(0.7f, 0.1f, 0.7f), matPoste, c, false);
                roda.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
    }

    // =================================================================
    //  Iluminação, céu, nevoeiro e pós-processamento
    // =================================================================
    static void ConfigurarIluminacao()
    {
        Light sol = null;
        foreach (var l in UnityEngine.Object.FindObjectsByType<Light>())
            if (l.type == LightType.Directional) { sol = l; break; }
        if (sol == null)
        {
            var g = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(g, "Criar Sol");
            sol = g.AddComponent<Light>();
            sol.type = LightType.Directional;
        }
        Undo.RecordObject(sol, "Luz");
        Undo.RecordObject(sol.transform, "Luz");
        if (noite)
        {
            sol.color = new Color(0.55f, 0.65f, 1f);   // luar
            sol.intensity = 0.25f;
            sol.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
        }
        else
        {
            sol.color = new Color(1f, 0.96f, 0.88f);
            sol.intensity = 1.3f;
            sol.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
        sol.shadows = LightShadows.Soft;

        string caminhoCeu = PASTA_MAT + "/Ceu.mat";
        var ceu = AssetDatabase.LoadAssetAtPath<Material>(caminhoCeu);
        if (ceu == null)
        {
            ceu = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(ceu, caminhoCeu);
        }
        ceu.SetFloat("_SunSize", noite ? 0.03f : 0.04f);
        ceu.SetFloat("_AtmosphereThickness", noite ? 0.35f : 1f);
        ceu.SetColor("_SkyTint", noite ? new Color(0.15f, 0.18f, 0.35f) : new Color(0.5f, 0.5f, 0.5f));
        ceu.SetColor("_GroundColor", noite ? new Color(0.05f, 0.05f, 0.07f) : new Color(0.37f, 0.35f, 0.34f));
        ceu.SetFloat("_Exposure", noite ? 0.25f : 1.3f);
        EditorUtility.SetDirty(ceu);

        RenderSettings.skybox = ceu;
        RenderSettings.sun = sol;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = noite ? new Color(0.10f, 0.12f, 0.20f) : new Color(0.55f, 0.58f, 0.62f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = noite ? new Color(0.06f, 0.07f, 0.12f) : new Color(0.72f, 0.80f, 0.88f);
        RenderSettings.fogDensity = noite ? 0.018f : 0.006f;
        DynamicGI.UpdateEnvironment();
    }

    // Bloom faz as luzes "brilharem"
    static void CriarPosProcessamento()
    {
        string caminho = PASTA + "/PosProcessamento.asset";
        AssetDatabase.DeleteAsset(caminho);
        var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(perfil, caminho);

        var bloom = perfil.Add<Bloom>(true);
        bloom.threshold.Override(noite ? 0.8f : 1.1f);
        bloom.intensity.Override(noite ? 1.2f : 0.3f);
        bloom.scatter.Override(0.65f);
        AssetDatabase.AddObjectToAsset(bloom, perfil);

        var tone = perfil.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);
        AssetDatabase.AddObjectToAsset(tone, perfil);
        EditorUtility.SetDirty(perfil);

        var volGo = new GameObject("Pos-Processamento");
        volGo.transform.SetParent(raiz, false);
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = perfil;

        var cam = Camera.main;
        if (cam != null)
        {
            var dados = cam.GetComponent<UniversalAdditionalCameraData>();
            if (dados != null)
            {
                Undo.RecordObject(dados, "Ativar pos-processamento");
                dados.renderPostProcessing = true;
                EditorUtility.SetDirty(dados);
            }
        }
    }
}
#endif
