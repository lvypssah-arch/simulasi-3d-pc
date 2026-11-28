using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Result Screen baru. HUDController.ShowResult meneruskan datanya ke sini; tombolnya memanggil
// fungsi HUDController yang sudah ada (OnRestartButton, OnMainMenuButton, OnNextButton).
public class ResultScreen : MonoBehaviour
{
    public static ResultScreen I;
    public static bool IsShowing { get { return I != null && I.showing; } }

    CanvasGroup group;
    RectTransform panel;
    TMP_Text tagTxt, title, subtitle, scoreVal, timeVal, mistakeVal, bestText;
    Image[] stars = new Image[3];
    GameObject starsRow;
    Button nextBtn;
    HUDController hud;
    bool built, showing;

    void Awake() { I = this; }
    void OnDestroy() { if (I == this) I = null; }

    public void Show(HUDController h, bool win, string levelName, int score, int mistakes, float time,
                     int starCount, bool hasNext, string reason)
    {
        hud = h;
        Build();
        showing = true;
        Color accent = win ? UIKit.Cyan : UIKit.Pink;

        tagTxt.text = string.IsNullOrEmpty(levelName) ? "LEVEL" : levelName;
        tagTxt.color = accent;
        title.text = win ? "PC MENYALA!" : "LEVEL GAGAL";
        title.color = win ? Color.white : UIKit.Pink;
        subtitle.text = win ? "SELAMAT, LEVEL SELESAI" : (string.IsNullOrEmpty(reason) ? "" : reason.ToUpper());
        timeVal.text = Util.FormatTime(time);
        mistakeVal.text = mistakes.ToString();
        scoreVal.text = "0";

        int best = AssemblyManager.I != null ? PlayerPrefs.GetInt("best_" + AssemblyManager.I.levelId, 0) : 0;
        bestText.text = (win && best > 0) ? "SKOR TERBAIK   " + best : "";

        starsRow.SetActive(win);
        for (int i = 0; i < 3; i++)
        {
            stars[i].color = (win && i < starCount) ? UIKit.Gold : new Color(0.3f, 0.3f, 0.35f, 0.9f);
            stars[i].rectTransform.localScale = Vector3.zero;
        }
        nextBtn.gameObject.SetActive(win && hasNext);

        group.interactable = true; group.blocksRaycasts = true;
        StopAllCoroutines();
        StartCoroutine(Play(score, win, starCount));
    }

    IEnumerator Play(int score, bool win, int starCount)
    {
        float t = 0f, dur = 0.45f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            group.alpha = k;
            panel.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, k);
            yield return null;
        }
        group.alpha = 1f; panel.localScale = Vector3.one;

        // hitung naik skor
        t = 0f; dur = 0.9f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / dur), 3f);
            scoreVal.text = Mathf.RoundToInt(score * k).ToString();
            yield return null;
        }
        scoreVal.text = score.ToString();

        if (win)
            for (int i = 0; i < 3; i++)
            {
                float s = 0f;
                while (s < 0.25f)
                {
                    s += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(s / 0.25f);
                    stars[i].rectTransform.localScale = Vector3.one * (k < 0.7f ? k / 0.7f * 1.25f : Mathf.Lerp(1.25f, 1f, (k - 0.7f) / 0.3f));
                    yield return null;
                }
                stars[i].rectTransform.localScale = Vector3.one;
            }
    }

    // ---------------------------------------------------------------- UI
    void Build()
    {
        if (built) return;
        built = true;
        var cv = UIKit.Overlay("MM_Result", 90, transform);
        group = cv.GetComponent<CanvasGroup>();
        group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;

        var dim = UIKit.Img(cv.transform, "Dim", new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.74f), true);
        UIKit.Stretch(dim.rectTransform);

        panel = UIKit.Panel(cv.transform, "Panel", 0.93f);
        UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 680f));

        tagTxt = UIKit.Label(panel, "Tag", "LEVEL", 22f, UIKit.Cyan, TextAlignmentOptions.TopLeft, FontStyles.Bold, 12f, false);
        UIKit.TopLeft(tagTxt.rectTransform, 70f, 52f, 700f, 30f);
        title = UIKit.Label(panel, "Title", "PC MENYALA!", 84f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold, 8f, true);
        UIKit.TopLeft(title.rectTransform, 70f, 88f, 800f, 110f);
        subtitle = UIKit.Label(panel, "Subtitle", "", 24f, new Color(1f, 1f, 1f, 0.65f), TextAlignmentOptions.TopLeft, FontStyles.Normal, 6f, false);
        UIKit.TopLeft(subtitle.rectTransform, 70f, 200f, 900f, 36f);

        // bintang (berlian)
        starsRow = UIKit.Rect("Stars", panel).gameObject;
        UIKit.TopLeft((RectTransform)starsRow.transform, 860f, 110f, 250f, 60f);
        for (int i = 0; i < 3; i++)
        {
            var im = UIKit.Img(starsRow.transform, "Star" + (i + 1), UIKit.Gold);
            UIKit.TopLeft(im.rectTransform, i * 84f + 6f, 8f, 44f, 44f);
            im.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            im.rectTransform.anchoredPosition = new Vector2(i * 84f + 30f, -30f);
            im.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            stars[i] = im;
        }

        UIKit.GradLine(panel, 70f, 262f, 1040f, 2f);

        scoreVal = Stat(70f, "SKOR");
        timeVal = Stat(430f, "WAKTU");
        mistakeVal = Stat(790f, "KESALAHAN");

        bestText = UIKit.Label(panel, "Best", "", 22f, UIKit.Cyan, TextAlignmentOptions.TopLeft, FontStyles.Normal, 6f, false);
        UIKit.TopLeft(bestText.rectTransform, 70f, 452f, 700f, 34f);

        var row = UIKit.Rect("Buttons", panel);
        UIKit.TopLeft(row, 70f, 540f, 1040f, 72f);
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 24f; h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        var sz = new Vector2(300f, 72f);
        UIKit.MakeButton(row, "Retry", "ULANGI", UIKit.Cyan, sz, () => { if (hud != null) hud.OnRestartButton(); });
        UIKit.MakeButton(row, "MainMenu", "MENU UTAMA", UIKit.Pink, sz, () => { if (hud != null) hud.OnMainMenuButton(); });
        nextBtn = UIKit.MakeButton(row, "Next", "BERIKUTNYA", UIKit.Cyan, sz, () => { if (hud != null) hud.OnNextButton(); });
    }

    TMP_Text Stat(float x, string label)
    {
        var l = UIKit.Label(panel, "L_" + label, label, 20f, UIKit.Cyan, TextAlignmentOptions.TopLeft, FontStyles.Bold, 10f, false);
        UIKit.TopLeft(l.rectTransform, x, 296f, 320f, 28f);
        var v = UIKit.Label(panel, "V_" + label, "0", 76f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold, 4f, true);
        UIKit.TopLeft(v.rectTransform, x, 330f, 320f, 100f);
        return v;
    }
}
