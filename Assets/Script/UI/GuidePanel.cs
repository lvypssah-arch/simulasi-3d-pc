using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tombol "?" kecil + panel kontrol. Dibuka/ditutup kapan saja (tombol ?, H, F1, atau ESC saat terbuka).
// Tidak menghentikan permainan.
[DefaultExecutionOrder(-200)]
public class GuidePanel : MonoBehaviour
{
    public static bool IsOpen;

    public Vector2 buttonOffset = new Vector2(-24f, -100f);   // dari pojok kanan atas layar
    public float pulseSeconds = 12f;                          // tombol berdenyut sebentar agar mudah ditemukan

    Image buttonGlow;
    float pulseUntil;

    CanvasGroup panelGroup;
    RectTransform panel;
    Coroutine anim;
    bool open;

    void Start() { Build(); pulseUntil = Time.unscaledTime + pulseSeconds; }
    void OnDestroy() { IsOpen = false; }

    void Update()
    {
        if (buttonGlow != null && !open && Time.unscaledTime < pulseUntil)
            buttonGlow.color = new Color(UIKit.Cyan.r, UIKit.Cyan.g, UIKit.Cyan.b, 0.10f + 0.16f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f)));
        if (Time.timeScale == 0f && !open) return;                 // sedang pause / tidak relevan
        if (ResultScreen.IsShowing) return;
        if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.F1)) Toggle();
        else if (open && Input.GetKeyDown(KeyCode.Escape)) { UIKit.EscUsedFrame = Time.frameCount; SetOpen(false); }
    }

    public void Toggle() { SetOpen(!open); }

    void SetOpen(bool on)
    {
        if (open == on) return;
        open = on; IsOpen = on;
        panelGroup.interactable = on; panelGroup.blocksRaycasts = on;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Anim(on));
    }

    IEnumerator Anim(bool on)
    {
        float from = panelGroup.alpha, to = on ? 1f : 0f, dur = on ? 0.2f : 0.14f, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            panelGroup.alpha = Mathf.Lerp(from, to, k);
            panel.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, panelGroup.alpha);
            yield return null;
        }
        panelGroup.alpha = to;
        panel.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, to);
    }

    // ---------------------------------------------------------------- UI
    void Build()
    {
        var cv = UIKit.Overlay("MM_Help", 20, transform);

        // tombol ?
        var btn = UIKit.MakeButton(cv.transform, "HelpButton", "?  PANDUAN", UIKit.Cyan, new Vector2(176f, 48f), Toggle, 20f);
        var brt = (RectTransform)btn.transform;
        UIKit.Anchor(brt, new Vector2(1f, 1f), buttonOffset, new Vector2(176f, 48f));
        brt.GetComponent<LayoutElement>().ignoreLayout = true;
        var gl = brt.Find("Glow");
        if (gl != null) buttonGlow = gl.GetComponent<Image>();

        // panel
        var pcv = UIKit.Overlay("MM_GuidePanel", 21, transform);
        panelGroup = pcv.GetComponent<CanvasGroup>();
        panelGroup.alpha = 0f; panelGroup.interactable = false; panelGroup.blocksRaycasts = false;

        panel = UIKit.Panel(pcv.transform, "Panel", 0.92f);
        UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 640f));

        UIKit.Label(panel, "Tag", "PANDUAN", 20f, UIKit.Cyan, TextAlignmentOptions.TopLeft, FontStyles.Bold, 12f, false);
        UIKit.TopLeft(panel.Find("Tag") as RectTransform, 60f, 36f, 500f, 28f);
        var title = UIKit.Label(panel, "Title", "KONTROL", 52f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold, 10f, true);
        UIKit.TopLeft(title.rectTransform, 60f, 62f, 600f, 70f);
        UIKit.GradLine(panel, 60f, 138f, 640f, 2f);

        var left = Column(panel, 50f, 166f);
        Header(left, "KAMERA");
        Row(left, "KLIK KANAN + GESER", "Putar kamera");
        Row(left, "SCROLL", "Zoom");
        Row(left, "KLIK TENGAH + GESER", "Geser kamera");
        Row(left, "W A S D / PANAH", "Putar kamera pelan");
        Row(left, "C", "Kembalikan posisi kamera");
        Header(left, "ANTARMUKA");
        Row(left, "TAB", "Buka / tutup panel komponen");
        Row(left, "HINT", "Petunjuk pemasangan (mengurangi skor)");
        Row(left, "RESET", "Ulangi level");
        Row(left, "ESC", "Pause menu");
        Row(left, "H / F1", "Buka / tutup panel ini");

        var right = Column(panel, 600f, 166f);
        Header(right, "PERAKITAN");
        Row(right, "KLIK KARTU", "Ambil komponen, gerakkan ke meja");
        Row(right, "KLIK KIRI", "Pasang di slot hologram");
        Row(right, "Q / E", "Putar horizontal");
        Row(right, "R / F", "Putar vertikal");
        Row(right, "Z / X", "Miringkan");
        Row(right, "ALTERNATIF", "PgDn/PgUp, End/Home, Del/Ins");
        Row(right, "SHIFT", "Putar lebih halus");
        Row(right, "ALT", "Putar langsung 90 derajat");
        Row(right, "T", "Reset putaran komponen");
        Row(right, "ESC / BACKSPACE", "Batalkan komponen yang dipegang");

        // tombol tutup (X) di pojok kanan atas panel
        var close = UIKit.MakeButton(panel, "Close", "X", UIKit.Pink, new Vector2(52f, 52f), Toggle, 24f);
        close.GetComponent<LayoutElement>().ignoreLayout = true;
        UIKit.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(52f, 52f));

        var foot = UIKit.Label(panel, "Foot", "H / F1 / ESC   TUTUP PANDUAN", 18f, new Color(1f, 1f, 1f, 0.45f),
            TextAlignmentOptions.TopLeft, FontStyles.Normal, 6f, false);
        UIKit.TopLeft(foot.rectTransform, 50f, 596f, 700f, 28f);
    }

    static RectTransform Column(RectTransform parent, float x, float y)
    {
        var rt = UIKit.Rect("Col", parent);
        UIKit.TopLeft(rt, x, y, 530f, 430f);
        var v = UIKit.VStack(rt, 6f);
        v.childAlignment = TextAnchor.UpperLeft;
        return rt;
    }

    static void Header(RectTransform col, string s)
    {
        var t = UIKit.Label(col, "H_" + s, s, 20f, UIKit.Cyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, 10f, false);
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 34f; le.preferredWidth = 530f;
    }

    static void Row(RectTransform col, string key, string desc)
    {
        var row = UIKit.Rect("Row", col);
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 14f; h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        var rle = row.gameObject.AddComponent<LayoutElement>();
        rle.minHeight = 30f; rle.preferredWidth = 530f;

        // tampilan "keycap": kotak tipis dengan garis cyan di kiri
        var cell = UIKit.Img(row, "KeyCell", new Color(1f, 1f, 1f, 0.07f));
        var kle = cell.gameObject.AddComponent<LayoutElement>();
        kle.preferredWidth = 220f; kle.minWidth = 220f; kle.minHeight = 30f;
        var cl = UIKit.Img(cell.transform, "Accent", UIKit.Cyan);
        cl.rectTransform.anchorMin = new Vector2(0, 0); cl.rectTransform.anchorMax = new Vector2(0, 1);
        cl.rectTransform.pivot = new Vector2(0, 0.5f); cl.rectTransform.sizeDelta = new Vector2(3f, 0f);
        cl.rectTransform.anchoredPosition = Vector2.zero;
        var k = UIKit.Label(cell.transform, "Key", key, 16f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, 1f, false);
        k.enableWordWrapping = false;
        UIKit.Stretch(k.rectTransform);
        k.rectTransform.offsetMin = new Vector2(12f, 0f); k.rectTransform.offsetMax = new Vector2(-6f, 0f);
        var d = UIKit.Label(row, "Desc", desc, 16f, new Color(1f, 1f, 1f, 0.72f), TextAlignmentOptions.MidlineLeft, FontStyles.Normal, 1f, false);
        var dle = d.gameObject.AddComponent<LayoutElement>();
        dle.preferredWidth = 296f; dle.flexibleWidth = 1f;
    }
}
