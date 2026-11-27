#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuBuilder
{
    const string Dir = "Assets/UI/MainMenu";
    const string MatDir = "Assets/Materials/MainMenu";
    static readonly Color Cyan = new Color(0f, 0.9f, 1f, 1f);
    static readonly Color Pink = new Color(1f, 0.12f, 0.55f, 1f);
    static TMP_FontAsset titleFont, bodyFont;

    static bool Flip { get { return EditorPrefs.GetBool("mm_flip", false); } }

    [MenuItem("Tools/RakitPC/21. Main Menu: Balik Sisi Kamera", false, 1021)]
    static void FlipSide() { EditorPrefs.SetBool("mm_flip", !Flip); Build(); }

    // ============================================================ UTILITAS
    static Transform FindRoot(string startsWith)
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name.StartsWith(startsWith)) return go.transform;
        return null;
    }

    static bool GetBounds(Transform t, out Bounds b)
    {
        b = new Bounds();
        bool first = true;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return !first;
    }

    static void Place(Transform t, Vector3 xz, float tableY)
    {
        Bounds b;
        if (t == null || !GetBounds(t, out b)) return;
        t.position += new Vector3(xz.x - b.center.x, tableY - b.min.y, xz.z - b.center.z);
        EditorUtility.SetDirty(t);
    }

    // Mencari meja di scene: pertama "joined table", lalu objek root mana pun yang namanya mengandung "table"/"meja".
    static bool GetTable(out float y, out float x0, out float x1, out float z0, out float z1)
    {
        y = x0 = x1 = z0 = z1 = 0f;
        Transform tb = FindRoot("joined table");
        if (tb == null)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                string n = go.name.ToLower();
                if (n.Contains("table") || n.Contains("meja")) { tb = go.transform; break; }
            }
        }
        Bounds b;
        if (tb != null && GetBounds(tb, out b))
        {
            y = b.max.y; x0 = b.min.x; x1 = b.max.x; z0 = b.min.z; z1 = b.max.z;
            return true;
        }
        return false;
    }

    static TMP_FontAsset FindFont(params string[] names)
    {
        foreach (string n in names)
            foreach (string g in AssetDatabase.FindAssets("t:TMP_FontAsset " + n))
            {
                var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g));
                if (f != null && f.name.ToLower().Contains(n)) return f;
            }
        return null;
    }

    static T FindAsset<T>(params string[] words) where T : Object
    {
        foreach (string g in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            string low = Path.GetFileNameWithoutExtension(path).ToLower();
            foreach (string w in words) if (low.Contains(w)) return AssetDatabase.LoadAssetAtPath<T>(path);
        }
        return null;
    }

    static Material MakeMat(string name, Color col, float metallic, float smooth, bool emissive, float emissionBoost)
    {
        string path = MatDir + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", col);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", col * emissionBoost);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject Cube(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material m, bool shadow)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos; go.transform.rotation = rot; go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static Light GetOrMakeLight(string name, LightType type)
    {
        Transform t = FindRoot(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name);
        var l = go.GetComponent<Light>();
        if (l == null) l = go.AddComponent<Light>();
        l.type = type;
        return l;
    }

    // ============================================================ VOLUME PROFILE
    static T AddFx<T>(VolumeProfile p) where T : VolumeComponent
    {
        var c = p.Add<T>(true);
        c.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(c, p);
        return c;
    }

    static VolumeProfile MakeProfile(float dofStart, float dofEnd)
    {
        string path = Dir + "/MainMenuProfile.asset";
        AssetDatabase.DeleteAsset(path);
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, path);

        var bloom = AddFx<Bloom>(p);
        bloom.threshold.Override(0.85f); bloom.intensity.Override(1.3f); bloom.scatter.Override(0.72f);

        var vig = AddFx<Vignette>(p);
        vig.intensity.Override(0.38f); vig.smoothness.Override(0.55f);

        var ca = AddFx<ColorAdjustments>(p);
        ca.postExposure.Override(-0.15f); ca.contrast.Override(14f); ca.saturation.Override(8f);

        var grain = AddFx<FilmGrain>(p);
        grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(0.22f); grain.response.Override(0.8f);

        var chroma = AddFx<ChromaticAberration>(p);
        chroma.intensity.Override(0.08f);

        var tone = AddFx<Tonemapping>(p);
        tone.mode.Override(TonemappingMode.ACES);

        var dof = AddFx<DepthOfField>(p);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(dofStart); dof.gaussianEnd.Override(dofEnd); dof.gaussianMaxRadius.Override(1.4f);

        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    // ============================================================ UI HELPER
    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void TopLeft(RectTransform rt, float x, float y, float w, float h)
    { Anchor(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h)); }

    static void BottomLeft(RectTransform rt, float x, float y, float w, float h)
    { Anchor(rt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, y), new Vector2(w, h)); }

    static void Stretch(RectTransform rt)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }

    static Image NewImage(string name, Transform parent, Color c, Sprite s)
    {
        var rt = NewRect(name, parent);
        var im = rt.gameObject.AddComponent<Image>();
        im.color = c; im.sprite = s; im.raycastTarget = false;
        return im;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color col,
                                   TextAlignmentOptions al, FontStyles st, float spacing, TMP_FontAsset font)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = col; t.alignment = al; t.fontStyle = st;
        t.characterSpacing = spacing; t.raycastTarget = false; t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        if (font != null) t.font = font;
        return t;
    }

    static Sprite MakeGradient(string name, bool horizontal)
    {
        string path = Dir + "/" + name + ".png";
        int w = horizontal ? 256 : 4, h = horizontal ? 4 : 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float t = horizontal ? x / (w - 1f) : y / (h - 1f);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Pow(1f - t, 1.6f)));
            }
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Button NewButton(Transform parent, string name, string label, float w, float h, float size, out TextMeshProUGUI txt)
    {
        var rt = NewRect(name, parent);
        rt.sizeDelta = new Vector2(w, h);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = Color.white;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0.08f);
        cb.highlightedColor = new Color(0f, 0.9f, 1f, 0.35f);
        cb.pressedColor = new Color(0f, 0.9f, 1f, 0.6f);
        cb.selectedColor = cb.normalColor;
        cb.disabledColor = new Color(1f, 1f, 1f, 0.03f);
        cb.fadeDuration = 0.12f;
        b.colors = cb;
        var nav = new Navigation(); nav.mode = Navigation.Mode.None; b.navigation = nav;
        txt = NewText("Label", rt, label, size, Color.white, TextAlignmentOptions.Center, FontStyles.Bold, 6f, bodyFont);
        Stretch(txt.rectTransform);
        return b;
    }

    static Slider NewSlider(Transform parent, string name, float w)
    {
        var rt = NewRect(name, parent);
        rt.sizeDelta = new Vector2(w, 28f);
        var hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        var s = rt.gameObject.AddComponent<Slider>();

        var track = NewImage("Track", rt, new Color(1f, 1f, 1f, 0.15f), null);
        Anchor(track.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 6));

        var fillArea = NewRect("Fill Area", rt);
        Anchor(fillArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, 6f));
        var fill = NewImage("Fill", fillArea, Cyan, null);
        Anchor(fill.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 0f));

        var handleArea = NewRect("Handle Slide Area", rt);
        Anchor(handleArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, 0f));
        var handle = NewImage("Handle", handleArea, Color.white, null);
        handle.raycastTarget = true;
        Anchor(handle.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 0f));

        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f; s.maxValue = 1f; s.value = 0.5f;
        var nav = new Navigation(); nav.mode = Navigation.Mode.None; s.navigation = nav;
        return s;
    }

    static MenuItemUI NewItem(Transform parent, string label, float y)
    {
        var rt = NewRect("Item_" + label, parent);
        TopLeft(rt, 140f, y, 560f, 70f);
        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0f);

        var acc = NewImage("Accent", rt, Cyan, null);
        Anchor(acc.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(5f, 54f));

        var txt = NewText("Label", rt, label, 38f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, 14f, titleFont);
        Anchor(txt.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34f, 0f), new Vector2(500f, 60f));
        txt.enableWordWrapping = false;

        var item = rt.gameObject.AddComponent<MenuItemUI>();
        item.highlight = bg; item.accent = acc; item.label = txt; item.labelRect = txt.rectTransform;
        return item;
    }

    static void Row(RectTransform panel, string label, float y, out RectTransform ctrl)
    {
        var l = NewText("Lbl_" + label, panel, label, 28f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.MidlineLeft, FontStyles.Normal, 8f, bodyFont);
        TopLeft(l.rectTransform, 0f, y, 330f, 56f);
        ctrl = NewRect("Ctrl_" + label, panel);
        TopLeft(ctrl, 340f, y, 480f, 56f);
    }

    // ============================================================ BUILD
    [MenuItem("Tools/RakitPC/20. Bangun Main Menu (buka scene MainMenu dulu)", false, 1020)]
    static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "MainMenu") { Debug.LogWarning("Buka scene MainMenu dulu, lalu jalankan menu ini."); return; }

        Directory.CreateDirectory(Dir);
        Directory.CreateDirectory(MatDir);
        AssetDatabase.Refresh();

        foreach (string n in new[] { "MM_UI", "MM_Controller", "MM_Rig", "MM_Env", "MM_KeySpot" })
        {
            GameObject old;
            while ((old = GameObject.Find(n)) != null) Object.DestroyImmediate(old);
        }

        titleFont = FindFont("orbitron", "rajdhani", "chakra", "oxanium", "audiowide");
        bodyFont = FindFont("rajdhani", "chakra", "oxanium", "orbitron");

        // ---------- kamera
        Transform pivot = FindRoot("CamPivot");
        Camera cam = pivot != null ? pivot.GetComponentInChildren<Camera>(true) : null;
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            var cg = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam = cg.GetComponent<Camera>();
            if (pivot != null) cg.transform.SetParent(pivot, false);
        }
        cam.tag = "MainCamera";

        // ---------- meja dan tata letak
        float tableY, x0, x1, z0, z1;
        if (!GetTable(out tableY, out x0, out x1, out z0, out z1))
        { Debug.LogWarning("Meja tidak ditemukan. Pastikan ada objek root bernama 'joined table' (atau yang namanya mengandung 'table'/'meja') di scene MainMenu. Dibatalkan."); return; }

        float sx = x1 - x0, sz = z1 - z0;
        bool longX = sx >= sz;
        float shortSize = longX ? sz : sx;
        Vector3 center = new Vector3((x0 + x1) * 0.5f, tableY, (z0 + z1) * 0.5f);
        Vector3 cp = cam.transform.position;
        Vector3 f = longX ? new Vector3(0f, 0f, cp.z >= center.z ? -1f : 1f)
                          : new Vector3(cp.x >= center.x ? -1f : 1f, 0f, 0f);
        if (Flip) f = -f;
        Vector3 r = Vector3.Cross(Vector3.up, f);
        float sc = Mathf.Clamp(shortSize / 0.9f, 0.5f, 1f);
        System.Func<float, float, Vector3> P = (ro, fo) => center + r * (ro * sc) + f * (fo * sc);

        Transform casing = FindRoot("Casing"), ram = FindRoot("Kingston"), gpu = FindRoot("GPU"), cpu = FindRoot("CPU");
        Place(casing, P(-0.28f, 0.12f), tableY);
        Place(gpu, P(0.08f, -0.10f), tableY);
        Place(ram, P(0.30f, 0.02f), tableY);
        Place(cpu, P(0.16f, -0.34f), tableY);
        Vector3 groupCenter = P(0.04f, 0f) + Vector3.up * 0.20f;

        // ---------- lingkungan
        var env = new GameObject("MM_Env").transform;
        Material wallMat = MakeMat("MM_Wall", new Color(0.04f, 0.045f, 0.06f), 0.2f, 0.4f, false, 0f);
        Material floorMat = MakeMat("MM_Floor", new Color(0.03f, 0.035f, 0.05f), 0.6f, 0.85f, false, 0f);
        Material neonPink = MakeMat("MM_NeonPink", Pink, 0f, 0.5f, true, 3.2f);
        Material neonCyan = MakeMat("MM_NeonCyan", Cyan, 0f, 0.5f, true, 3.2f);

        Transform plane = FindRoot("Plane");
        if (plane != null) { var pr = plane.GetComponent<Renderer>(); if (pr != null) pr.sharedMaterial = floorMat; }

        Quaternion faceRot = Quaternion.LookRotation(f);
        Cube("MM_Wall", env, center + f * 2.4f + Vector3.up * 0.9f, faceRot, new Vector3(8f, 3.4f, 0.06f), wallMat, true);
        var neons = new System.Collections.Generic.List<Renderer>();
        float[] ny = { 0.28f, 0.85f, 1.45f };
        float[] nr = { -1.2f, 0.8f, -0.4f };
        for (int i = 0; i < 3; i++)
        {
            var g = Cube("MM_Neon_" + i, env, center + f * 2.34f + r * nr[i] + Vector3.up * ny[i], faceRot,
                         new Vector3(4.5f, 0.04f, 0.03f), i == 1 ? neonCyan : neonPink, false);
            neons.Add(g.GetComponent<Renderer>());
        }

        // ---------- cahaya dan atmosfer
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.035f, 0.04f, 0.06f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.11f;
        RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.04f);

        Transform dl = FindRoot("Directional Light");
        if (dl != null)
        {
            var l = dl.GetComponent<Light>();
            if (l != null) { l.intensity = 0.25f; l.color = new Color(0.55f, 0.65f, 1f); l.shadows = LightShadows.Soft; }
            dl.rotation = Quaternion.Euler(55f, 35f, 0f);
        }

        Light pink = GetOrMakeLight("Point Light Pink", LightType.Point);
        pink.color = Pink; pink.intensity = 3.5f; pink.range = 3.5f;
        pink.transform.position = P(-0.85f, 0.5f) + Vector3.up * 0.55f;
        Light cyanL = GetOrMakeLight("Point Light Cyan", LightType.Point);
        cyanL.color = Cyan; cyanL.intensity = 3.5f; cyanL.range = 3.5f;
        cyanL.transform.position = P(0.85f, 0.45f) + Vector3.up * 0.45f;

        var keyGo = new GameObject("MM_KeySpot");
        var key = keyGo.AddComponent<Light>();
        key.type = LightType.Spot; key.spotAngle = 58f; key.innerSpotAngle = 30f; key.range = 8f;
        key.intensity = 22f; key.color = new Color(1f, 0.96f, 0.9f); key.shadows = LightShadows.Soft;
        keyGo.transform.position = groupCenter + Vector3.up * 1.8f - f * 0.9f - r * 0.3f;
        keyGo.transform.LookAt(groupCenter);

        // ---------- kamera: pengaturan dan post-processing
        foreach (var mb in (pivot != null ? pivot.GetComponents<MonoBehaviour>() : new MonoBehaviour[0]))
            if (mb != null && mb.GetType().Name == "AutoRotate") mb.enabled = false;
        if (pivot != null) pivot.position = center;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.01f, 0.012f, 0.02f);
        cam.fieldOfView = 38f;
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 40f;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        VolumeProfile profile = MakeProfile(2.2f, 5.0f);
        Volume vol = null;
        Transform gv = FindRoot("Global Volume");
        if (gv != null) vol = gv.GetComponent<Volume>();
        if (vol == null) { var gvo = new GameObject("Global Volume"); vol = gvo.AddComponent<Volume>(); }
        vol.isGlobal = true; vol.weight = 1f; vol.sharedProfile = profile;

        // ---------- titik fokus kamera
        var rig = new GameObject("MM_Rig").transform;
        Bounds bGpu = new Bounds(groupCenter, Vector3.zero), bCase = bGpu;
        if (gpu != null) GetBounds(gpu, out bGpu);
        if (casing != null) GetBounds(casing, out bCase);
        Vector3 cpuPos = cpu != null ? cpu.position : groupCenter;
        Transform fMain = new GameObject("Focus_Main").transform; fMain.SetParent(rig, false); fMain.position = groupCenter;
        Transform fSet = new GameObject("Focus_Settings").transform; fSet.SetParent(rig, false);
        fSet.position = (bGpu.center + cpuPos) * 0.5f + Vector3.up * 0.02f;
        Transform fAbout = new GameObject("Focus_About").transform; fAbout.SetParent(rig, false);
        fAbout.position = bCase.center;

        // ---------- UI
        Sprite gradH = MakeGradient("grad_h", true), gradV = MakeGradient("grad_v", false);

        var uiGo = new GameObject("MM_UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = uiGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var scaler = uiGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = uiGo.transform;

        // bayangan kiri, bawah, atas, dan bar sinematik
        var shade = NewImage("LeftShade", root, new Color(1f, 1f, 1f, 0.92f), gradH);
        Anchor(shade.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(1250f, 0f));
        var bshade = NewImage("BottomShade", root, new Color(1f, 1f, 1f, 0.8f), gradV);
        Anchor(bshade.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0f, 380f));
        var tshade = NewImage("TopShade", root, new Color(1f, 1f, 1f, 0.6f), gradV);
        Anchor(tshade.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0f, 240f));
        tshade.rectTransform.localScale = new Vector3(1f, -1f, 1f);
        var barB = NewImage("CineBar_Bottom", root, Color.black, null);
        Anchor(barB.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0f, 44f));
        var barT = NewImage("CineBar_Top", root, Color.black, null);
        Anchor(barT.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0f, 44f));

        // judul (dua baris: PC BUILDING / SIMULATOR)
        var titleRt = NewRect("TitleBlock", root);
        TopLeft(titleRt, 140f, 110f, 1000f, 340f);
        var titleGroup = titleRt.gameObject.AddComponent<CanvasGroup>();
        var tag = NewText("Tag", titleRt, "SIMULASI PERAKITAN PC 3D", 22f, Cyan, TextAlignmentOptions.Left, FontStyles.Bold, 12f, bodyFont);
        TopLeft(tag.rectTransform, 0f, 0f, 900f, 30f);
        var title = NewText("Title", titleRt, "PC BUILDING\n<color=#00E5FF>SIMULATOR</color>", 84f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold, 8f, titleFont);
        TopLeft(title.rectTransform, 0f, 36f, 1000f, 200f);
        title.enableWordWrapping = false;
        title.lineSpacing = -10f;
        var line1 = NewImage("Line_Pink", titleRt, Pink, null);
        TopLeft(line1.rectTransform, 0f, 250f, 300f, 4f);
        var line2 = NewImage("Line_Cyan", titleRt, Cyan, null);
        TopLeft(line2.rectTransform, 300f, 250f, 120f, 4f);
        var tagline = NewText("Tagline", titleRt, "BUILD.  TEST.  UPGRADE.", 24f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Left, FontStyles.Normal, 16f, bodyFont);
        TopLeft(tagline.rectTransform, 0f, 268f, 900f, 34f);

        // daftar menu
        var menuRt = NewRect("MenuBlock", root);
        Stretch(menuRt);
        var menuGroup = menuRt.gameObject.AddComponent<CanvasGroup>();
        string[] labels = { "PLAY", "SETTINGS", "ABOUT", "QUIT GAME" };
        var items = new MenuItemUI[4];
        for (int i = 0; i < 4; i++) items[i] = NewItem(menuRt, labels[i], 460f + i * 74f);
        var desc = NewText("Desc", menuRt, "", 26f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.TopLeft, FontStyles.Normal, 2f, bodyFont);
        BottomLeft(desc.rectTransform, 140f, 150f, 820f, 100f);

        var hint = NewText("Hint", root, "W / S   PILIH        ENTER   KONFIRMASI        ESC   KEMBALI", 22f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.Left, FontStyles.Normal, 6f, bodyFont);
        BottomLeft(hint.rectTransform, 140f, 70f, 1000f, 34f);
        var ver = NewText("Version", root, "v1.0   |   Unity 2022.3   |   URP", 20f, new Color(1f, 1f, 1f, 0.4f), TextAlignmentOptions.Right, FontStyles.Normal, 4f, bodyFont);
        Anchor(ver.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60f, 70f), new Vector2(600f, 30f));

        // ---------- panel Settings
        var setRt = NewRect("SettingsPanel", root);
        TopLeft(setRt, 140f, 170f, 820f, 740f);
        var setGroup = setRt.gameObject.AddComponent<CanvasGroup>();
        var setHead = NewText("Header", setRt, "SETTINGS", 64f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold, 12f, titleFont);
        TopLeft(setHead.rectTransform, 0f, 0f, 800f, 80f);
        var setLine = NewImage("Line", setRt, Pink, null);
        TopLeft(setLine.rectTransform, 0f, 96f, 700f, 2f);

        RectTransform c;
        Row(setRt, "VOLUME", 130f, out c);
        var volSlider = NewSlider(c, "Slider", 360f); TopLeft(volSlider.GetComponent<RectTransform>(), 0f, 14f, 360f, 28f);
        var volVal = NewText("Value", c, "80%", 26f, Color.white, TextAlignmentOptions.MidlineRight, FontStyles.Bold, 4f, bodyFont);
        TopLeft(volVal.rectTransform, 380f, 0f, 100f, 56f);

        TextMeshProUGUI tmp;
        Row(setRt, "GRAFIS", 220f, out c);
        var qPrev = NewButton(c, "Prev", "<", 56f, 56f, 30f, out tmp); TopLeft((RectTransform)qPrev.transform, 0f, 0f, 56f, 56f);
        var qLabel = NewText("Value", c, "HIGH", 26f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold, 4f, bodyFont);
        TopLeft(qLabel.rectTransform, 66f, 0f, 300f, 56f);
        var qNext = NewButton(c, "Next", ">", 56f, 56f, 30f, out tmp); TopLeft((RectTransform)qNext.transform, 376f, 0f, 56f, 56f);

        Row(setRt, "LAYAR PENUH", 310f, out c);
        TextMeshProUGUI fsTxt;
        var fsBtn = NewButton(c, "Toggle", "AKTIF", 220f, 56f, 24f, out fsTxt); TopLeft((RectTransform)fsBtn.transform, 0f, 0f, 220f, 56f);

        Row(setRt, "SENSITIVITAS KAMERA", 400f, out c);
        var sensSlider = NewSlider(c, "Slider", 360f); TopLeft(sensSlider.GetComponent<RectTransform>(), 0f, 14f, 360f, 28f);
        var sensVal = NewText("Value", c, "1.0x", 26f, Color.white, TextAlignmentOptions.MidlineRight, FontStyles.Bold, 4f, bodyFont);
        TopLeft(sensVal.rectTransform, 380f, 0f, 100f, 56f);

        Row(setRt, "EFEK SINEMATIK", 490f, out c);
        TextMeshProUGUI cineTxt;
        var cineBtn = NewButton(c, "Toggle", "AKTIF", 220f, 56f, 24f, out cineTxt); TopLeft((RectTransform)cineBtn.transform, 0f, 0f, 220f, 56f);

        TextMeshProUGUI sbTxt;
        var setBack = NewButton(setRt, "Back", "KEMBALI", 240f, 56f, 24f, out sbTxt); TopLeft((RectTransform)setBack.transform, 0f, 610f, 240f, 56f);

        // ---------- panel About
        var abRt = NewRect("AboutPanel", root);
        TopLeft(abRt, 140f, 170f, 820f, 740f);
        var abGroup = abRt.gameObject.AddComponent<CanvasGroup>();
        var abHead = NewText("Header", abRt, "ABOUT", 64f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold, 12f, titleFont);
        TopLeft(abHead.rectTransform, 0f, 0f, 800f, 80f);
        var abLine = NewImage("Line", abRt, Pink, null);
        TopLeft(abLine.rectTransform, 0f, 96f, 700f, 2f);
        var abBody = NewText("Body", abRt, "", 28f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.TopLeft, FontStyles.Normal, 2f, bodyFont);
        abBody.lineSpacing = 10f;
        TopLeft(abBody.rectTransform, 0f, 130f, 760f, 480f);
        TextMeshProUGUI abBackTxt;
        var abBack = NewButton(abRt, "Back", "KEMBALI", 240f, 56f, 24f, out abBackTxt); TopLeft((RectTransform)abBack.transform, 0f, 640f, 240f, 56f);

        // ---------- overlay hitam
        var fadeRt = NewRect("Fade", root);
        Stretch(fadeRt);
        var fadeImg = fadeRt.gameObject.AddComponent<Image>();
        fadeImg.color = Color.black;
        var fadeGroup = fadeRt.gameObject.AddComponent<CanvasGroup>();
        var loading = NewText("Loading", fadeRt, "MEMUAT SIMULASI...", 30f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold, 14f, bodyFont);
        Anchor(loading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 60f));

        // panel disembunyikan di editor; controller mengaturnya saat Play
        setGroup.alpha = 0f; setGroup.interactable = false; setGroup.blocksRaycasts = false;
        abGroup.alpha = 0f; abGroup.interactable = false; abGroup.blocksRaycasts = false;
        fadeGroup.alpha = 0f; fadeGroup.blocksRaycasts = false;

        if (FindObjectOfTypeSafe<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // ---------- controller
        var ctrlGo = new GameObject("MM_Controller");
        var ctrl = ctrlGo.AddComponent<MainMenuController>();
        ctrl.cam = cam;
        ctrl.viewForward = f;
        ctrl.poseMain = new MainMenuController.CamPose { focus = fMain, distance = 1.6f, height = 0.55f, lateral = 0.30f };
        ctrl.poseSettings = new MainMenuController.CamPose { focus = fSet, distance = 0.85f, height = 0.35f, lateral = 0.18f };
        ctrl.poseAbout = new MainMenuController.CamPose { focus = fAbout, distance = 1.25f, height = 0.45f, lateral = 0.30f };
        ctrl.titleGroup = titleGroup; ctrl.menuGroup = menuGroup; ctrl.settingsGroup = setGroup;
        ctrl.aboutGroup = abGroup; ctrl.fadeGroup = fadeGroup;
        ctrl.items = items; ctrl.descText = desc; ctrl.loadingText = loading; ctrl.aboutText = abBody;
        ctrl.volumeSlider = volSlider; ctrl.sensSlider = sensSlider; ctrl.volumeValue = volVal; ctrl.sensValue = sensVal;
        ctrl.qualityLabel = qLabel; ctrl.qualityPrev = qPrev; ctrl.qualityNext = qNext;
        ctrl.fullscreenButton = fsBtn; ctrl.fullscreenLabel = fsTxt;
        ctrl.cineButton = cineBtn; ctrl.cineLabel = cineTxt;
        ctrl.settingsBack = setBack; ctrl.aboutBack = abBack;
        ctrl.cineVolume = vol;
        ctrl.pinkLight = pink; ctrl.cyanLight = cyanL;
        ctrl.neonRenderers = neons.ToArray();

        var ui = ctrlGo.AddComponent<AudioSource>(); ui.playOnAwake = false; ui.spatialBlend = 0f;
        var music = ctrlGo.AddComponent<AudioSource>(); music.playOnAwake = false; music.loop = true; music.spatialBlend = 0f;
        ctrl.uiSource = ui; ctrl.musicSource = music;
        ctrl.hoverClip = FindAsset<AudioClip>("hover", "tick");
        ctrl.clickClip = FindAsset<AudioClip>("click", "select", "confirm");
        ctrl.musicClip = FindAsset<AudioClip>("menu", "ambient", "music", "bgm");

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = ctrlGo;
        Debug.Log("Main Menu dibangun. Meja y = " + tableY.ToString("F3") + ", arah kamera " + f
                  + (Flip ? " (dibalik)" : "") + ". Simpan scene (Ctrl+S), lalu Play. "
                  + "Font: " + (titleFont != null ? titleFont.name : "bawaan TMP")
                  + ". Audio: " + (ctrl.clickClip != null ? "klik ditemukan" : "belum ada"));
    }

    static T FindObjectOfTypeSafe<T>() where T : Object { return Object.FindObjectOfType<T>(); }
}
#endif