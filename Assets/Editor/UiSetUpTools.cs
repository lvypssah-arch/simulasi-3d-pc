#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class UiSetupTools
{
    // ---- parameter yang boleh diubah ----
    const float PanelW = 420f, PanelH = 520f;   // ukuran asli Window
    const float PanelScale = 1.2f;              // pembesaran panel di layar 1080p
    

    static void SetupCanvas(Canvas cv)
    {
        var sc = cv.GetComponent<CanvasScaler>();
        if (sc == null) sc = Undo.AddComponent<CanvasScaler>(cv.gameObject);
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        if (cv.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(cv.gameObject);
    }

    // ------------------------------------------------------------ 13. panel komponen ke layar kiri
    [MenuItem("Tools/RakitPC/13. Panel Komponen ke Layar Kiri", false, 1013)]
    static void PanelToScreen()
    {
        var panel = Object.FindObjectOfType<PartsPanel>();
        if (panel == null) { Debug.LogWarning("PartsPanel tidak ditemukan."); return; }
        Canvas cv = panel.GetComponent<Canvas>();
        if (cv == null) { Debug.LogWarning("PartsPanel tidak punya komponen Canvas."); return; }
        RectTransform win = panel.transform.Find("Window") as RectTransform;
        if (win == null) { Debug.LogWarning("Anak bernama Window tidak ditemukan di PartsPanel."); return; }

        Undo.RecordObject(cv, "Panel layar");
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 20;
        SetupCanvas(cv);

        RectTransform root = (RectTransform)panel.transform;
        Undo.RecordObject(root, "Panel layar");
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        Undo.RecordObject(win, "Window panel");
        win.anchorMin = new Vector2(0f, 1f);
        win.anchorMax = new Vector2(0f, 1f);
        win.pivot = new Vector2(0f, 1f);
        win.sizeDelta = new Vector2(PanelW, PanelH);
        win.anchoredPosition3D = new Vector3(24f, -96f, 0f);
        win.localRotation = Quaternion.identity;
        win.localScale = Vector3.one * PanelScale;

        Undo.RecordObject(panel, "Panel window");
        panel.window = win;
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("PartsPanel dipindah ke layar (kiri atas, " + (PanelW * PanelScale).ToString("F0") + " x "
                  + (PanelH * PanelScale).ToString("F0") + " piksel di 1080p). Jangan jalankan menu 6 versi lama lagi.");
    }

    // ------------------------------------------------------------ 14. tombol layar
    static GameObject MakeButton(Transform parent, string name, string label, Color col, AssemblyManager mgr, string id)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = col;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var tgo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        tgo.transform.SetParent(go.transform, false);
        var r = (RectTransform)tgo.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(8f, 4f);
        r.offsetMax = new Vector2(-8f, -4f);
        var t = tgo.GetComponent<TextMeshProUGUI>();
        t.text = label;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.fontStyle = FontStyles.Bold;
        t.enableAutoSizing = true;
        t.fontSizeMin = 20f;
        t.fontSizeMax = 34f;
        t.raycastTarget = false;

        UnityEventTools.AddStringPersistentListener(btn.onClick, mgr.UiAction, id);
        return go;
    }

    [MenuItem("Tools/RakitPC/14. Buat Tombol PARTS HINT RESET di Layar", false, 1014)]
    static void MakeButtonBar()
    {
        var mgr = Object.FindObjectOfType<AssemblyManager>();
        if (mgr == null) { Debug.LogWarning("AssemblyManager tidak ditemukan."); return; }

        GameObject old = GameObject.Find("UI_ButtonBar");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var root = new GameObject("UI_ButtonBar", typeof(RectTransform), typeof(Canvas),
                                  typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Bar tombol");
        var cv = root.GetComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 15;
        SetupCanvas(cv);

        var bar = new GameObject("Bar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        bar.transform.SetParent(root.transform, false);
        var rt = (RectTransform)bar.transform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 32f);
        rt.sizeDelta = new Vector2(740f, 78f);
        var h = bar.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 16f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;

        MakeButton(bar.transform, "Btn_UI_Parts", "PARTS (Tab)", new Color(0.10f, 0.45f, 0.85f, 0.95f), mgr, "parts");
        GameObject hint = MakeButton(bar.transform, "Btn_UI_Hint", "HINT", new Color(0.85f, 0.55f, 0.05f, 0.95f), mgr, "hint");
        MakeButton(bar.transform, "Btn_UI_Reset", "RESET", new Color(0.75f, 0.15f, 0.20f, 0.95f), mgr, "reset");

        Undo.RecordObject(mgr, "Bar tombol");
        mgr.screenButtonBar = root;
        mgr.hintButtonUI = hint;
        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Tombol layar dibuat (UI_ButtonBar). Saat Play, tombol 3D di meja dimatikan otomatis. "
                  + "HINT hanya tampil kalau Allow Hint di AssemblyManager dicentang.");
    }

    // ------------------------------------------------------------ 15. teks HUD
    static void Fit(TMP_Text t, float min, float max)
    {
        if (t == null) return;
        Undo.RecordObject(t, "HUD font");
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
        EditorUtility.SetDirty(t);
    }

    [MenuItem("Tools/RakitPC/15. Perjelas Teks HUD", false, 1015)]
    static void HudReadable()
    {
        var hud = Object.FindObjectOfType<HUDController>();
        if (hud == null) { Debug.LogWarning("HUDController tidak ditemukan."); return; }

        Fit(hud.levelText, 20f, 34f);
        Fit(hud.stepText, 20f, 34f);
        Fit(hud.scoreText, 20f, 34f);
        Fit(hud.timeText, 20f, 34f);
        Fit(hud.mistakeText, 20f, 34f);
        Fit(hud.toastText, 22f, 36f);
        Fit(hud.holdTitle, 22f, 34f);
        Fit(hud.holdBody, 16f, 26f);
        Fit(hud.resultTitle, 28f, 56f);
        Fit(hud.resultBody, 20f, 36f);
        foreach (var b in hud.GetComponentsInChildren<Button>(true))
            foreach (var t in b.GetComponentsInChildren<TMP_Text>(true)) Fit(t, 18f, 32f);

        Canvas cv = hud.GetComponentInParent<Canvas>();
        var sc = cv != null ? cv.GetComponent<CanvasScaler>() : null;
        Debug.Log("HUD: Canvas " + (cv != null ? cv.renderMode.ToString() : "tidak ada")
                  + ", Scaler " + (sc != null ? sc.uiScaleMode + " ref " + sc.referenceResolution : "tidak ada")
                  + ". Teks HUD diatur auto-size. Pastikan Game view Scale 1x dan tab Game dimaksimalkan.");
        EditorSceneManager.MarkAllScenesDirty();
    }
}
#endif