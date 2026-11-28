using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pause Menu: ESC atau tombol MENU di HUD. Menghentikan Time.timeScale, sehingga timer, perakitan,
// kamera (SeatedLook), dan interaksi klik (PointerInteractor) ikut berhenti.
[DefaultExecutionOrder(-100)]
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu I;
    public static bool IsOpen { get { return I != null && I.open; } }

    const string KVol = "vol_master", KSens = "cam_sens", KFull = "fullscreen";

    CanvasGroup group;
    RectTransform side, pageMain, pageSettings;
    TMP_Text volText, sensText, fsText;
    Slider volSlider, sensSlider;
    bool built, open, fsState;
    float sensApplied = 1f;
    Coroutine anim;

    void Awake() { I = this; }

    void OnDestroy()
    {
        if (I == this) I = null;
        if (open) Time.timeScale = 1f;
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (UIKit.EscUsedFrame == Time.frameCount) return;       // ESC sudah dipakai menutup Help

        if (open)
        {
            if (pageSettings != null && pageSettings.gameObject.activeSelf) ShowPage(0);
            else Close();
        }
        else
        {
            if (ResultScreen.IsShowing) return;
            // saat memegang komponen, ESC tetap membatalkan komponen (perilaku lama AssemblyManager)
            if (AssemblyManager.I != null && AssemblyManager.I.IsHolding) return;
            Open();
        }
    }

    public void Open()
    {
        if (open || ResultScreen.IsShowing) return;
        Build();
        open = true;
        Time.timeScale = 0f;
        ShowPage(0);
        group.interactable = true; group.blocksRaycasts = true;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(UIKit.FadeIn(group, side, group.alpha, 1f, 0.22f, 60f));
    }

    public void Close()
    {
        if (!open) return;
        open = false;
        Time.timeScale = 1f;
        group.interactable = false; group.blocksRaycasts = false;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(UIKit.FadeIn(group, side, group.alpha, 0f, 0.16f, 60f));
    }

    void ShowPage(int p)
    {
        pageMain.gameObject.SetActive(p == 0);
        pageSettings.gameObject.SetActive(p == 1);
        if (p == 1) SyncSettings();
    }

    // ---------------------------------------------------------------- bangun UI
    void Build()
    {
        if (built) return;
        built = true;
        var cv = UIKit.Overlay("MM_Pause", 100, transform);
        group = cv.GetComponent<CanvasGroup>();
        group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;

        var dim = UIKit.Img(cv.transform, "Dim", new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.62f), true);
        UIKit.Stretch(dim.rectTransform);

        // panel kiri setinggi layar, seperti Main Menu
        var bg = UIKit.Img(cv.transform, "Side", new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.9f), true);
        side = bg.rectTransform;
        side.anchorMin = new Vector2(0, 0); side.anchorMax = new Vector2(0, 1); side.pivot = new Vector2(0, 0.5f);
        side.sizeDelta = new Vector2(760f, 0f); side.anchoredPosition = Vector2.zero;
        var edge = UIKit.Img(side, "Edge", UIKit.Pink);
        edge.rectTransform.anchorMin = new Vector2(1, 0); edge.rectTransform.anchorMax = new Vector2(1, 1);
        edge.rectTransform.pivot = new Vector2(1, 0.5f); edge.rectTransform.sizeDelta = new Vector2(2f, 0f);
        edge.rectTransform.anchoredPosition = Vector2.zero;

        UIKit.Label(side, "Tag", "SIMULASI DIJEDA", 22f, UIKit.Cyan, TextAlignmentOptions.TopLeft, FontStyles.Bold, 12f, false);
        UIKit.TopLeft(side.Find("Tag") as RectTransform, 140f, 150f, 560f, 30f);
        var title = UIKit.Label(side, "Title", "PAUSE", 84f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold, 8f, true);
        UIKit.TopLeft(title.rectTransform, 140f, 184f, 560f, 110f);
        UIKit.GradLine(side, 140f, 302f, 480f, 2f);

        // halaman utama
        pageMain = UIKit.Rect("PageMain", side);
        UIKit.TopLeft(pageMain, 140f, 340f, 480f, 560f);
        UIKit.VStack(pageMain, 14f);
        var sz = new Vector2(480f, 64f);
        UIKit.MakeButton(pageMain, "Resume", "RESUME", UIKit.Cyan, sz, Close);
        UIKit.MakeButton(pageMain, "Restart", "RESTART LEVEL", UIKit.Cyan, sz, Restart);
        UIKit.MakeButton(pageMain, "Settings", "SETTINGS", UIKit.Cyan, sz, () => ShowPage(1));
        UIKit.MakeButton(pageMain, "MainMenu", "MAIN MENU", UIKit.Pink, sz, MainMenu);
#if !UNITY_WEBGL && !UNITY_IOS
        UIKit.MakeButton(pageMain, "Quit", "QUIT GAME", UIKit.Pink, sz, Quit);
#endif

        // halaman settings
        pageSettings = UIKit.Rect("PageSettings", side);
        UIKit.TopLeft(pageSettings, 140f, 340f, 480f, 560f);
        UIKit.VStack(pageSettings, 10f);
        volText = LabelRow(pageSettings, "VOLUME");
        volSlider = UIKit.MakeSlider(pageSettings, "Volume", new Vector2(480f, 36f), OnVolume);
        sensText = LabelRow(pageSettings, "SENSITIVITAS KAMERA");
        sensSlider = UIKit.MakeSlider(pageSettings, "Sens", new Vector2(480f, 36f), OnSens);
        var fsBtn = UIKit.MakeButton(pageSettings, "Fullscreen", "LAYAR PENUH", UIKit.Cyan, new Vector2(480f, 56f), ToggleFullscreen, 22f);
        fsText = fsBtn.GetComponentInChildren<TMP_Text>(true);
        UIKit.MakeButton(pageSettings, "Back", "KEMBALI", UIKit.Pink, new Vector2(240f, 56f), () => ShowPage(0), 22f);

        var hint = UIKit.Label(side, "Hint", "ESC   LANJUTKAN", 22f, new Color(1f, 1f, 1f, 0.5f),
            TextAlignmentOptions.BottomLeft, FontStyles.Normal, 6f, false);
        UIKit.TopLeft(hint.rectTransform, 140f, 980f, 520f, 40f);
    }

    static TMP_Text LabelRow(RectTransform parent, string text)
    {
        var t = UIKit.Label(parent, "Lbl", text, 22f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold, 6f, false);
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 34f; le.preferredWidth = 480f;
        return t;
    }

    // ---------------------------------------------------------------- settings
    void SyncSettings()
    {
        float vol = PlayerPrefs.GetFloat(KVol, AudioListener.volume);
        volSlider.SetValueWithoutNotify(vol);
        volText.text = "VOLUME   " + Mathf.RoundToInt(vol * 100f) + "%";
        sensApplied = Mathf.Max(0.1f, PlayerPrefs.GetFloat(KSens, 1f));
        sensSlider.SetValueWithoutNotify(Mathf.InverseLerp(0.4f, 2f, sensApplied));
        sensText.text = "SENSITIVITAS KAMERA   " + sensApplied.ToString("0.0") + "x";
        fsState = Screen.fullScreen;
        fsText.text = "LAYAR PENUH : " + (fsState ? "AKTIF" : "NONAKTIF");
    }

    void OnVolume(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(KVol, v);
        volText.text = "VOLUME   " + Mathf.RoundToInt(v * 100f) + "%";
    }

    void OnSens(float v01)
    {
        float s = Mathf.Round(Mathf.Lerp(0.4f, 2f, v01) * 10f) / 10f;
        PlayerPrefs.SetFloat(KSens, s);
        // SeatedLook membaca cam_sens hanya saat Start, jadi kecepatan putar disesuaikan langsung di sini
        if (SeatedLook.I != null) SeatedLook.I.rotateSpeed *= s / sensApplied;
        sensApplied = s;
        sensText.text = "SENSITIVITAS KAMERA   " + s.ToString("0.0") + "x";
    }

    void ToggleFullscreen()
    {
        fsState = !fsState;
        Screen.fullScreen = fsState;
        PlayerPrefs.SetInt(KFull, fsState ? 1 : 0);
        fsText.text = "LAYAR PENUH : " + (fsState ? "AKTIF" : "NONAKTIF");
    }

    // ---------------------------------------------------------------- aksi
    void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
