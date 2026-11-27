using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Skin visual untuk UI gameplay (Top Bar, panel KOMPONEN, Bottom Bar).
// Hanya mengubah warna/font/dekorasi. TIDAK mengubah teks, onClick, layout group, atau logika game.
// Cara melepas: hapus / nonaktifkan objek "GP_UISkin" di scene.
public class GameplayUISkin : MonoBehaviour
{
    public TMP_FontAsset titleFont;
    public TMP_FontAsset bodyFont;
    public bool animate = true;

    static readonly Color Cyan = new Color(0f, 0.9f, 1f, 1f);
    static readonly Color Pink = new Color(1f, 0.12f, 0.55f, 1f);
    static readonly Color Ink = new Color(0.02f, 0.03f, 0.065f, 1f);

    Sprite grad;
    readonly HashSet<int> fonted = new HashSet<int>();
    readonly HashSet<int> skinned = new HashSet<int>();
    readonly List<Image> pulse = new List<Image>();
    readonly List<float> pulseBase = new List<float>();
    RectTransform panelRt, scan;

    IEnumerator Start()
    {
        yield return null;
        yield return null; // tunggu UI game selesai dibangun / di-layout
        grad = MakeGrad();
        Build();
        var wait = new WaitForSecondsRealtime(1f);
        while (true) { Refresh(); yield return wait; }
    }

    // ------------------------------------------------------------ BUILD
    void Build()
    {
        // TOP BAR
        Component lvl = FindText(s => s.StartsWith("LEVEL"));
        if (lvl != null)
        {
            RectTransform bar = AncestorWithImage(lvl.transform, 1.01f, 0.25f);
            if (bar != null) SkinBar(bar, lvl);
            else Debug.LogWarning("[GP_UISkin] Top bar tidak ditemukan.");
        }
        else Debug.LogWarning("[GP_UISkin] Teks 'LEVEL ...' tidak ditemukan, Top Bar dilewati.");

        // PANEL HOLOGRAM
        Component head = FindText(s => s == "KOMPONEN");
        if (head != null)
        {
            panelRt = AncestorWithImage(head.transform, 0.6f, 0.95f);
            if (panelRt != null) SkinPanel(panelRt, head);
            else Debug.LogWarning("[GP_UISkin] Panel KOMPONEN tidak ditemukan.");
        }
        else Debug.LogWarning("[GP_UISkin] Teks 'KOMPONEN' tidak ditemukan, panel dilewati.");

        // TOMBOL (Bottom Bar + Menu)
        foreach (var b in FindObjectsOfType<Button>(true))
        {
            if (panelRt != null && b.transform.IsChildOf(panelRt)) continue;
            string l = LabelOf(b).Trim().ToUpper();
            if (l.StartsWith("PARTS")) SkinButton(b, Cyan);
            else if (l.StartsWith("HINT")) SkinButton(b, new Color(0.6f, 0.88f, 1f, 1f));
            else if (l.StartsWith("RESET")) SkinButton(b, Pink);
            else if (l == "MENU") SkinButton(b, Cyan);
        }
    }

    void SkinBar(RectTransform bar, Component lvl)
    {
        Bg(bar, new Color(Ink.r, Ink.g, Ink.b, 0.82f));
        Deco(bar, "GP_Line", Color.white, grad, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 2));
        var tick = Deco(bar, "GP_Tick", Cyan, null, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, 0));
        Pulse(tick);
        StyleText(lvl, titleFont, true, Cyan, 6f, true);
        fonted.Add(lvl.GetInstanceID());
    }

    void SkinPanel(RectTransform panel, Component head)
    {
        Bg(panel, new Color(Ink.r, Ink.g, Ink.b, 0.84f));
        Deco(panel, "GP_TopLine", Color.white, grad, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 2));
        for (int ax = 0; ax <= 1; ax++)
            for (int ay = 0; ay <= 1; ay++)
            {
                var a = new Vector2(ax, ay);
                Pulse(Deco(panel, "GP_Cn_h", Cyan, null, a, a, a, Vector2.zero, new Vector2(24, 2)));
                Pulse(Deco(panel, "GP_Cn_v", Cyan, null, a, a, a, Vector2.zero, new Vector2(2, 24)));
            }
        scan = Deco(panel, "GP_Scan", new Color(Cyan.r, Cyan.g, Cyan.b, 0.10f), null,
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 2)).rectTransform;

        StyleText(head, titleFont, true, Color.white, 10f, true);
        fonted.Add(head.GetInstanceID());
        Deco(head.transform, "GP_HeadLine", Color.white, grad, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(0, 2));

        var sb = panel.GetComponentInChildren<Scrollbar>(true);
        if (sb != null)
        {
            var track = sb.GetComponent<Image>();
            if (track != null) track.color = new Color(1f, 1f, 1f, 0.06f);
            var hImg = sb.targetGraphic as Image;
            if (hImg != null)
            {
                hImg.color = Color.white;
                var cb = sb.colors;
                cb.normalColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f);
                cb.highlightedColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f);
                cb.pressedColor = Cyan; cb.selectedColor = cb.normalColor;
                sb.colors = cb;
            }
        }
    }

    void SkinButton(Button b, Color accent)
    {
        if (!skinned.Add(b.GetInstanceID())) return;
        var img = b.targetGraphic as Image;
        if (img == null) img = b.GetComponent<Image>();
        if (img != null) { img.sprite = null; img.type = Image.Type.Simple; img.color = Color.white; }

        b.transition = Selectable.Transition.ColorTint;
        var cb = b.colors;
        cb.colorMultiplier = 1f;
        cb.normalColor = new Color(0.03f, 0.05f, 0.09f, 0.80f);
        cb.highlightedColor = new Color(0.06f, 0.14f, 0.20f, 0.92f);
        cb.pressedColor = new Color(accent.r, accent.g, accent.b, 0.5f);
        cb.selectedColor = cb.normalColor;
        cb.disabledColor = new Color(0.03f, 0.05f, 0.09f, 0.4f);
        cb.fadeDuration = 0.12f;
        b.colors = cb;

        var glow = Deco(b.transform, "GP_Glow", new Color(accent.r, accent.g, accent.b, 0f), null, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        glow.transform.SetAsFirstSibling();
        var line = Deco(b.transform, "GP_Accent", accent, null, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 3));
        var fx = b.gameObject.AddComponent<SkinHoverFx>();
        fx.button = b; fx.accent = line; fx.glow = glow; fx.baseA = 0.75f; fx.glowMax = 0.18f;
        var c = line.color; c.a = 0.75f; line.color = c;

        var lt = b.GetComponentInChildren<TMP_Text>(true);
        if (lt != null)
        {
            lt.font = bodyFont != null ? bodyFont : lt.font;
            lt.characterSpacing = 6f; lt.color = Color.white;
            lt.fontStyle |= FontStyles.Bold | FontStyles.UpperCase;
            fonted.Add(lt.GetInstanceID());
        }
        else
        {
            var ut = b.GetComponentInChildren<Text>(true);
            if (ut != null) { ut.color = Color.white; ut.fontStyle = FontStyle.Bold; }
        }
    }

    void AddCardFx(Button b)
    {
        var glow = Deco(b.transform, "GP_Glow", new Color(Cyan.r, Cyan.g, Cyan.b, 0f), null, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        glow.transform.SetAsFirstSibling();
        var acc = Deco(b.transform, "GP_Acc", new Color(Cyan.r, Cyan.g, Cyan.b, 0f), null, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(3, 0));
        var fx = b.gameObject.AddComponent<SkinHoverFx>();
        fx.button = b; fx.accent = acc; fx.glow = glow; fx.baseA = 0f; fx.glowMax = 0.12f;
    }

    // ------------------------------------------------------------ REFRESH (kartu komponen bisa dibuat belakangan)
    void Refresh()
    {
        if (bodyFont != null)
        {
            foreach (var t in FindObjectsOfType<TextMeshProUGUI>(true))
            {
                if (!fonted.Add(t.GetInstanceID())) continue;
                if (t.canvas != null && t.canvas.name.StartsWith("MM_")) continue;
                if (t.GetComponentInParent<ScrollRect>() != null) continue; // kartu komponen: font asli dipertahankan
                t.font = bodyFont;
                if (Mathf.Approximately(t.characterSpacing, 0f)) t.characterSpacing = 2f;
            }
        }
        if (panelRt != null)
        {
            foreach (var b in panelRt.GetComponentsInChildren<Button>(true))
                if (skinned.Add(b.GetInstanceID())) AddCardFx(b);
        }
    }

    void Update()
    {
        if (!animate) return;
        float k = 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 2f);
        for (int i = 0; i < pulse.Count; i++)
        {
            if (pulse[i] == null) continue;
            var c = pulse[i].color; c.a = pulseBase[i] * k; pulse[i].color = c;
        }
        if (scan != null && panelRt != null && scan.gameObject.activeInHierarchy)
            scan.anchoredPosition = new Vector2(0f, -((Time.unscaledTime * 0.22f) % 1f) * panelRt.rect.height);
    }

    // ------------------------------------------------------------ HELPER
    void Pulse(Image im) { pulse.Add(im); pulseBase.Add(im.color.a); }

    static string TextOf(Component c)
    {
        var t = c as TMP_Text; if (t != null) return t.text;
        var u = c as Text; return u != null ? u.text : "";
    }

    static Component FindText(System.Func<string, bool> pred)
    {
        foreach (var t in FindObjectsOfType<TMP_Text>(true)) if (pred(t.text.Trim().ToUpper())) return t;
        foreach (var t in FindObjectsOfType<Text>(true)) if (pred(t.text.Trim().ToUpper())) return t;
        return null;
    }

    static string LabelOf(Button b)
    {
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) return t.text;
        var u = b.GetComponentInChildren<Text>(true);
        return u != null ? u.text : "";
    }

    static void StyleText(Component c, TMP_FontAsset f, bool setColor, Color col, float spacing, bool bold)
    {
        var t = c as TMP_Text;
        if (t != null)
        {
            if (f != null) t.font = f;
            if (setColor) t.color = col;
            t.characterSpacing = spacing;
            if (bold) t.fontStyle |= FontStyles.Bold;
            return;
        }
        var u = c as Text;
        if (u != null) { if (setColor) u.color = col; if (bold) u.fontStyle = FontStyle.Bold; }
    }

    static RectTransform AncestorWithImage(Transform start, float maxFracW, float maxFracH)
    {
        var cv = start.GetComponentInParent<Canvas>();
        if (cv == null) return null;
        var root = cv.rootCanvas.GetComponent<RectTransform>();
        Transform t = start.parent;
        for (int i = 0; i < 4 && t != null; i++, t = t.parent)
        {
            var rt = t as RectTransform;
            if (rt == null || t.GetComponent<Canvas>() != null) break;
            if (rt.rect.width <= root.rect.width * maxFracW && rt.rect.height <= root.rect.height * maxFracH
                && t.GetComponent<Image>() != null) return rt;
        }
        var p = start.parent as RectTransform;
        return (p != null && p.GetComponent<Canvas>() == null) ? p : null;
    }

    static Image Bg(RectTransform rt, Color c)
    {
        var im = rt.GetComponent<Image>();
        if (im == null) im = rt.gameObject.AddComponent<Image>();
        im.sprite = null; im.type = Image.Type.Simple; im.color = c;
        return im;
    }

    static Image Deco(Transform parent, string name, Color c, Sprite s, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var im = go.GetComponent<Image>();
        im.color = c; im.sprite = s; im.raycastTarget = false;
        return im;
    }

    static Sprite MakeGrad()
    {
        var tex = new Texture2D(128, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < 128; x++)
        {
            float t = x / 127f;
            Color c = t < 0.5f ? Color.Lerp(Cyan, Pink, t * 2f)
                               : Color.Lerp(Pink, new Color(Pink.r, Pink.g, Pink.b, 0f), (t - 0.5f) * 2f);
            tex.SetPixel(x, 0, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 128, 1), new Vector2(0.5f, 0.5f), 100f);
    }
}

// Efek hover ringan: garis aksen + glow. Tidak mengganggu event Button.
public class SkinHoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Button button;
    public Image accent, glow;
    public float baseA = 0.6f, glowMax = 0.16f;
    float t; bool hov;

    public void OnPointerEnter(PointerEventData e) { hov = true; }
    public void OnPointerExit(PointerEventData e) { hov = false; }
    void OnDisable() { hov = false; t = 0f; Apply(); }

    void Update()
    {
        float target = (hov && button != null && button.interactable) ? 1f : 0f;
        if (Mathf.Approximately(t, target)) return;
        t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime * 9f);
        Apply();
    }

    void Apply()
    {
        if (accent != null) { var c = accent.color; c.a = Mathf.Lerp(baseA, 1f, t); accent.color = c; }
        if (glow != null) { var c = glow.color; c.a = glowMax * t; glow.color = c; }
    }
}
