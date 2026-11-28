using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Gaya UI bersama (warna, font, panel, tombol) supaya Pause, Result, Help, dan Level Select satu identitas
// dengan Main Menu dan GameplayUISkin. Semua canvas buatan kode diberi nama awalan "MM_"
// supaya GameplayUISkin tidak mengubah font-nya.
public static class UIKit
{
    public static readonly Color Cyan = new Color(0f, 0.9f, 1f, 1f);
    public static readonly Color Pink = new Color(1f, 0.12f, 0.55f, 1f);
    public static readonly Color Ink = new Color(0.02f, 0.03f, 0.065f, 1f);
    public static readonly Color Gold = new Color(1f, 0.8f, 0.1f, 1f);

    public static TMP_FontAsset TitleFont, BodyFont;
    public static int EscUsedFrame = -1;     // dipakai Help agar ESC yang menutup Help tidak ikut membuka Pause
    static Sprite grad;

    public static void UseFonts(TMP_FontAsset title, TMP_FontAsset body)
    {
        if (title != null) TitleFont = title;
        if (body != null) BodyFont = body;
        if (TitleFont == null) TitleFont = TMP_Settings.defaultFontAsset;
        if (BodyFont == null) BodyFont = TitleFont;
    }

    public static void UseGameplayFonts()
    {
        var s = Object.FindObjectOfType<GameplayUISkin>();
        UseFonts(s != null ? s.titleFont : null, s != null ? s.bodyFont : null);
    }

    public static Sprite Grad()
    {
        if (grad != null) return grad;
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
        grad = Sprite.Create(tex, new Rect(0, 0, 128, 1), new Vector2(0.5f, 0.5f), 100f);
        return grad;
    }

    public static Canvas Overlay(string name, int order, Transform parent)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var cs = go.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        return c;
    }

    // ---------------------------------------------------------------- rect
    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    public static void TopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    public static Image Img(Transform parent, string name, Color c, bool raycast = false)
    {
        var rt = Rect(name, parent);
        var im = rt.gameObject.AddComponent<Image>();
        im.color = c; im.raycastTarget = raycast;
        return im;
    }

    public static TextMeshProUGUI Label(Transform parent, string name, string s, float size, Color c,
        TextAlignmentOptions al, FontStyles st, float spacing, bool title)
    {
        var rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = title ? TitleFont : BodyFont;
        t.text = s; t.fontSize = size; t.color = c; t.alignment = al; t.fontStyle = st;
        t.characterSpacing = spacing; t.raycastTarget = false;
        t.enableWordWrapping = true; t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    public static void Brackets(Transform parent, Color c, float len, float th)
    {
        for (int ax = 0; ax <= 1; ax++)
            for (int ay = 0; ay <= 1; ay++)
            {
                var a = new Vector2(ax, ay);
                Anchor(Img(parent, "Cn_h", c).rectTransform, a, Vector2.zero, new Vector2(len, th));
                Anchor(Img(parent, "Cn_v", c).rectTransform, a, Vector2.zero, new Vector2(th, len));
            }
    }

    // Panel gelap bergaya HUD: latar, garis gradasi di atas, siku cyan di sudut.
    public static RectTransform Panel(Transform parent, string name, float alpha)
    {
        var bg = Img(parent, name, new Color(Ink.r, Ink.g, Ink.b, alpha), true);
        var rt = bg.rectTransform;
        var line = Img(rt, "TopLine", Color.white); line.sprite = Grad();
        line.rectTransform.anchorMin = new Vector2(0, 1); line.rectTransform.anchorMax = new Vector2(1, 1);
        line.rectTransform.pivot = new Vector2(0.5f, 1f);
        line.rectTransform.anchoredPosition = Vector2.zero; line.rectTransform.sizeDelta = new Vector2(0, 3);
        Brackets(rt, Cyan, 28f, 2f);
        return rt;
    }

    public static Image GradLine(Transform parent, float x, float y, float w, float h)
    {
        var im = Img(parent, "Line", Color.white);
        im.sprite = Grad();
        TopLeft(im.rectTransform, x, y, w, h);
        return im;
    }

    // ---------------------------------------------------------------- tombol & slider
    public static Button MakeButton(Transform parent, string name, string label, Color accent, Vector2 size,
        UnityAction onClick, float fontSize = 26f)
    {
        var rt = Rect(name, parent);
        rt.sizeDelta = size;
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size.x; le.preferredHeight = size.y; le.minWidth = size.x; le.minHeight = size.y;

        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = Color.white;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        b.transition = Selectable.Transition.ColorTint;
        var cb = b.colors;
        cb.colorMultiplier = 1f;
        cb.normalColor = new Color(0.03f, 0.05f, 0.09f, 0.82f);
        cb.highlightedColor = new Color(0.06f, 0.14f, 0.20f, 0.95f);
        cb.pressedColor = new Color(accent.r, accent.g, accent.b, 0.5f);
        cb.selectedColor = cb.normalColor;
        cb.disabledColor = new Color(0.03f, 0.05f, 0.09f, 0.4f);
        cb.fadeDuration = 0.12f;
        b.colors = cb;

        var glow = Img(rt, "Glow", new Color(accent.r, accent.g, accent.b, 0f));
        Stretch(glow.rectTransform);
        var line = Img(rt, "Accent", new Color(accent.r, accent.g, accent.b, 0.75f));
        line.rectTransform.anchorMin = new Vector2(0, 0); line.rectTransform.anchorMax = new Vector2(1, 0);
        line.rectTransform.pivot = new Vector2(0.5f, 0f);
        line.rectTransform.anchoredPosition = Vector2.zero; line.rectTransform.sizeDelta = new Vector2(0, 3);

        var t = Label(rt, "Label", label, fontSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold, 6f, false);
        Stretch(t.rectTransform);

        var fx = rt.gameObject.AddComponent<SkinHoverFx>();
        fx.button = b; fx.accent = line; fx.glow = glow; fx.baseA = 0.75f; fx.glowMax = 0.18f;

        if (onClick != null) b.onClick.AddListener(onClick);
        return b;
    }

    public static void SetButtonLabel(Button b, string s)
    {
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.text = s;
    }

    public static Slider MakeSlider(Transform parent, string name, Vector2 size, UnityAction<float> onChange)
    {
        var rt = Rect(name, parent);
        rt.sizeDelta = size;
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size.x; le.preferredHeight = size.y; le.minHeight = size.y;

        var track = Img(rt, "Track", new Color(1f, 1f, 1f, 0.12f), true);
        track.rectTransform.anchorMin = new Vector2(0, 0.5f); track.rectTransform.anchorMax = new Vector2(1, 0.5f);
        track.rectTransform.sizeDelta = new Vector2(0, 6);

        var area = Rect("FillArea", rt);
        area.anchorMin = new Vector2(0, 0.5f); area.anchorMax = new Vector2(1, 0.5f);
        area.sizeDelta = new Vector2(-20f, 6f); area.anchoredPosition = Vector2.zero;
        var fill = Img(area, "Fill", Cyan);
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.sizeDelta = Vector2.zero;

        var slide = Rect("HandleSlide", rt);
        Stretch(slide); slide.offsetMin = new Vector2(10f, 0f); slide.offsetMax = new Vector2(-10f, 0f);
        var handle = Img(slide, "Handle", Color.white, true);
        handle.rectTransform.anchorMin = new Vector2(0, 0.5f); handle.rectTransform.anchorMax = new Vector2(0, 0.5f);
        handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(14f, 30f);

        var s = rt.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight; s.minValue = 0f; s.maxValue = 1f;
        if (onChange != null) s.onValueChanged.AddListener(onChange);
        return s;
    }

    public static VerticalLayoutGroup VStack(RectTransform rt, float spacing)
    {
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing; v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = false; v.childForceExpandHeight = false;
        return v;
    }

    public static IEnumerator FadeIn(CanvasGroup g, RectTransform slideRt, float from, float to, float dur, float slideX)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            float a = Mathf.Lerp(from, to, k);
            g.alpha = a;
            if (slideRt != null) slideRt.anchoredPosition = new Vector2(-slideX * (1f - a), slideRt.anchoredPosition.y);
            yield return null;
        }
        g.alpha = to;
        if (slideRt != null) slideRt.anchoredPosition = new Vector2(-slideX * (1f - to), slideRt.anchoredPosition.y);
    }
}
