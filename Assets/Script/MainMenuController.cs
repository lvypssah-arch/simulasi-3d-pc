using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public enum State { Intro, Main, Settings, About, Levels, Loading }

    [System.Serializable]
    public class CamPose
    {
        public Transform focus;
        public float distance = 1.6f;
        public float height = 0.55f;
        public float lateral = 0.30f;    // menggeser objek ke kanan layar
    }

    [Header("Game")]
    public string playScene = "Level_Entry";
    public string gameName = "PC BUILDING SIMULATOR";     // dipakai di panel About

    [Header("Kamera sinematik")]
    public Camera cam;
    public Vector3 viewForward = Vector3.forward;
    public CamPose poseMain = new CamPose();
    public CamPose poseSettings = new CamPose();
    public CamPose poseAbout = new CamPose();
    public float introDolly = 0.35f;
    public float swayYaw = 4f;
    public float swayRoll = 0.35f;
    public float swaySpeed = 0.35f;
    public float parallax = 0.07f;
    public float poseSmooth = 2.2f;

    [Header("Judul (otomatis diterapkan saat Play)")]
    public string tagText = "SIMULASI PERAKITAN PC 3D";
    public string titleText = "PC BUILDING\n<color=#00E5FF>SIMULATOR</color>";
    public string taglineText = "BUILD.  TEST.  UPGRADE.";
    public float titleFontSize = 84f;

    [Header("UI")]
    public CanvasGroup titleGroup, menuGroup, settingsGroup, aboutGroup, fadeGroup;
    [HideInInspector] public CanvasGroup levelsGroup;      // dibuat otomatis oleh LevelSelectPanel
    LevelSelectPanel levelPanel;
    public bool Busy { get { return busy; } }
    public MenuItemUI[] items;
    public TMP_Text descText, loadingText, aboutText;
    public string[] descriptions =
    {
        "Mulai simulasi perakitan. Pasang komponen langkah demi langkah dengan panduan hologram.",
        "Atur volume, kualitas grafis, layar penuh, dan sensitivitas kamera.",
        "Informasi tentang game dan pengembangnya.",
        "Tutup game."
    };
    [TextArea(4, 14)]
    public string aboutBody =
        "PC BUILDING SIMULATOR adalah simulasi perakitan PC 3D untuk mengenal komponen komputer: casing, motherboard, CPU, cooler, RAM, GPU, PSU, penyimpanan, hingga perangkat input.\n\n" +
        "Pasang setiap komponen di tempat yang benar, ikuti urutan perakitan, lalu uji dan upgrade sistemnya.\n\n" +
        "PENGEMBANG : [isi nama kamu]\nMATA KULIAH : [isi mata kuliah / kampus]\nENGINE : Unity 2022.3 (URP)";

    [Header("Settings")]
    public Slider volumeSlider, sensSlider;
    public TMP_Text volumeValue, sensValue, qualityLabel, fullscreenLabel, cineLabel;
    public Button qualityPrev, qualityNext, fullscreenButton, cineButton, settingsBack, aboutBack;
    public Volume cineVolume;

    [Header("Cahaya dan neon")]
    public Light pinkLight, cyanLight;
    public Renderer[] neonRenderers;

    [Header("Audio (opsional)")]
    public AudioSource uiSource, musicSource;
    public AudioClip hoverClip, clickClip, musicClip;

    const string KVol = "vol_master", KQuality = "quality", KFull = "fullscreen", KSens = "cam_sens", KCine = "cine_fx";

    State state = State.Intro;
    bool busy, fs = true, cine = true;
    int selected = -1;
    CamPose pose;
    Vector3 curFocus;
    float curDist, curHeight, introT;
    Vector2 mouseSmooth;
    float pinkBase = 1f, cyanBase = 1f;
    Material[] neonMats;
    Color[] neonBase;
    readonly Dictionary<CanvasGroup, Vector2> basePos = new Dictionary<CanvasGroup, Vector2>();

    // ------------------------------------------------------------ awal
    void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        ApplyTitleText();
        Register(titleGroup); Register(menuGroup); Register(settingsGroup); Register(aboutGroup); Register(fadeGroup);
        ApplySavedSettings();
        HookUi();
        BuildLevelSelect();
        SyncSettingsUi();
        CacheGlow();
        SnapCamera();
        if (aboutText != null) aboutText.text = BuildAboutText();

        SetAlpha(titleGroup, 0f);
        SetAlpha(menuGroup, 0f); SetInteractive(menuGroup, false);
        SetAlpha(settingsGroup, 0f); SetInteractive(settingsGroup, false);
        SetAlpha(aboutGroup, 0f); SetInteractive(aboutGroup, false);
        SetAlpha(fadeGroup, 1f);
        if (fadeGroup != null) fadeGroup.blocksRaycasts = true;
        if (loadingText != null) loadingText.alpha = 0f;
    }

    void Start()
    {
        // Deskripsi per opsi dihapus agar Main Menu bersih. Objek tetap ada (fontnya dipakai Level Select).
        if (descText != null) { descText.text = ""; descText.gameObject.SetActive(false); }
        if (musicSource != null && musicClip != null)
        {
            musicSource.clip = musicClip; musicSource.loop = true; musicSource.volume = 0f; musicSource.Play();
            StartCoroutine(FadeMusic(0.35f, 3f));
        }
        StartCoroutine(Intro());
    }

    void Register(CanvasGroup g)
    {
        if (g != null) basePos[g] = ((RectTransform)g.transform).anchoredPosition;
    }

    static void SetAlpha(CanvasGroup g, float a) { if (g != null) g.alpha = a; }
    static void SetInteractive(CanvasGroup g, bool on) { if (g != null) { g.interactable = on; g.blocksRaycasts = on; } }

    // Menimpa teks dan tata letak judul di scene supaya selalu sesuai, tanpa harus menjalankan Builder ulang.
    void ApplyTitleText()
    {
        if (titleGroup == null) return;
        Transform root = titleGroup.transform;

        Transform tag = root.Find("Tag");
        if (tag != null)
        {
            TMP_Text t = tag.GetComponent<TMP_Text>();
            if (t != null) t.text = tagText;
            PlaceTopLeft(tag, 0f, 0f, 900f, 30f);
        }

        Transform title = root.Find("Title");
        if (title != null)
        {
            TMP_Text t = title.GetComponent<TMP_Text>();
            if (t != null)
            {
                t.text = titleText;
                t.fontSize = titleFontSize;
                t.enableWordWrapping = false;
                t.lineSpacing = -10f;
                t.alignment = TextAlignmentOptions.TopLeft;
            }
            PlaceTopLeft(title, 0f, 36f, 1000f, 200f);
        }

        Transform pinkLine = root.Find("Line_Pink");
        if (pinkLine != null) PlaceTopLeft(pinkLine, 0f, 250f, 300f, 4f);

        Transform cyanLine = root.Find("Line_Cyan");
        if (cyanLine != null) PlaceTopLeft(cyanLine, 300f, 250f, 120f, 4f);

        Transform tagline = root.Find("Tagline");
        if (tagline != null)
        {
            TMP_Text t = tagline.GetComponent<TMP_Text>();
            if (t != null) t.text = taglineText;
            PlaceTopLeft(tagline, 0f, 268f, 900f, 34f);
        }
    }

    static void PlaceTopLeft(Transform tr, float x, float y, float w, float h)
    {
        RectTransform rt = tr as RectTransform;
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    void HookUi()
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            items[i].index = i;
            items[i].onHover = OnItemHover;
            items[i].onClick = OnItemClick;
        }
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolume);
        if (sensSlider != null) sensSlider.onValueChanged.AddListener(OnSens);
        if (qualityPrev != null) qualityPrev.onClick.AddListener(() => StepQuality(-1));
        if (qualityNext != null) qualityNext.onClick.AddListener(() => StepQuality(1));
        if (fullscreenButton != null) fullscreenButton.onClick.AddListener(ToggleFullscreen);
        if (cineButton != null) cineButton.onClick.AddListener(ToggleCine);
        if (settingsBack != null) settingsBack.onClick.AddListener(Back);
        if (aboutBack != null) aboutBack.onClick.AddListener(Back);
    }

    void CacheGlow()
    {
        if (pinkLight != null) pinkBase = pinkLight.intensity;
        if (cyanLight != null) cyanBase = cyanLight.intensity;
        if (neonRenderers == null) neonRenderers = new Renderer[0];
        neonMats = new Material[neonRenderers.Length];
        neonBase = new Color[neonRenderers.Length];
        for (int i = 0; i < neonRenderers.Length; i++)
        {
            if (neonRenderers[i] == null) continue;
            neonMats[i] = neonRenderers[i].material;
            neonBase[i] = neonMats[i].HasProperty("_EmissionColor") ? neonMats[i].GetColor("_EmissionColor") : Color.white;
        }
    }

    // ------------------------------------------------------------ pengaturan
    void ApplySavedSettings()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(KVol, 0.8f);
        if (PlayerPrefs.HasKey(KQuality))
        {
            int q = Mathf.Clamp(PlayerPrefs.GetInt(KQuality), 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(q, true);
        }
        fs = PlayerPrefs.HasKey(KFull) ? PlayerPrefs.GetInt(KFull) == 1 : Screen.fullScreen;
        if (PlayerPrefs.HasKey(KFull)) Screen.fullScreen = fs;
        cine = PlayerPrefs.GetInt(KCine, 1) == 1;
        if (cineVolume != null) cineVolume.enabled = cine;
    }

    void SyncSettingsUi()
    {
        float vol = PlayerPrefs.GetFloat(KVol, 0.8f);
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(vol);
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(vol * 100f) + "%";
        float sens = PlayerPrefs.GetFloat(KSens, 1f);
        if (sensSlider != null) sensSlider.SetValueWithoutNotify(Mathf.InverseLerp(0.4f, 2f, sens));
        if (sensValue != null) sensValue.text = sens.ToString("0.0") + "x";
        RefreshLabels();
    }

    void RefreshLabels()
    {
        if (qualityLabel != null) qualityLabel.text = QualitySettings.names[QualitySettings.GetQualityLevel()].ToUpper();
        if (fullscreenLabel != null) fullscreenLabel.text = fs ? "AKTIF" : "NONAKTIF";
        if (cineLabel != null) cineLabel.text = cine ? "AKTIF" : "NONAKTIF";
    }

    void OnVolume(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(KVol, v);
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    void OnSens(float v)
    {
        float s = Mathf.Lerp(0.4f, 2f, v);
        PlayerPrefs.SetFloat(KSens, s);
        if (sensValue != null) sensValue.text = s.ToString("0.0") + "x";
    }

    void StepQuality(int d)
    {
        int n = QualitySettings.names.Length;
        int q = (QualitySettings.GetQualityLevel() + d + n) % n;
        QualitySettings.SetQualityLevel(q, true);
        PlayerPrefs.SetInt(KQuality, q);
        RefreshLabels();
        PlayUi(clickClip);
    }

    void ToggleFullscreen()
    {
        fs = !fs;
        Screen.fullScreen = fs;
        PlayerPrefs.SetInt(KFull, fs ? 1 : 0);
        RefreshLabels();
        PlayUi(clickClip);
    }

    void ToggleCine()
    {
        cine = !cine;
        if (cineVolume != null) cineVolume.enabled = cine;
        PlayerPrefs.SetInt(KCine, cine ? 1 : 0);
        RefreshLabels();
        PlayUi(clickClip);
    }

    // ------------------------------------------------------------ input dan menu
    void OnItemHover(int i) { if (state == State.Main && !busy) Select(i, true); }
    void OnItemClick(int i) { if (state == State.Main && !busy) { Select(i, false); Activate(i); } }

    void Select(int i, bool sound)
    {
        if (items == null || items.Length == 0) return;
        i = (i + items.Length) % items.Length;
        bool changed = i != selected;
        selected = i;
        for (int k = 0; k < items.Length; k++) if (items[k] != null) items[k].SetSelected(k == i);
        if (changed && sound) PlayUi(hoverClip);
    }

    void Activate(int i)
    {
        PlayUi(clickClip);
        switch (i)
        {
            case 0:
                if (levelsGroup != null) StartCoroutine(OpenLevels());
                else StartCoroutine(PlayRoutine(playScene));
                break;
            case 1: StartCoroutine(OpenPanel(settingsGroup, poseSettings, State.Settings)); break;
            case 2: StartCoroutine(OpenPanel(aboutGroup, poseAbout, State.About)); break;
            case 3: StartCoroutine(QuitRoutine()); break;
        }
    }

    void Back()
    {
        if (busy) return;
        if (state == State.Settings) StartCoroutine(ClosePanel(settingsGroup));
        else if (state == State.About) StartCoroutine(ClosePanel(aboutGroup));
        else if (state == State.Levels) StartCoroutine(ClosePanel(levelsGroup));
        PlayUi(clickClip);
    }

    // ---------------------------------------------------------------- Level Select
    public void BackFromLevels() { if (state == State.Levels) Back(); }

    public void StartLevel(string sceneName)
    {
        if (busy) return;
        PlayUi(clickClip);
        StartCoroutine(PlayRoutine(sceneName));
    }

    IEnumerator OpenLevels()
    {
        yield return OpenPanel(levelsGroup, poseSettings, State.Levels);
        if (levelPanel != null) levelPanel.OnShown();
    }

    void BuildLevelSelect()
    {
        if (aboutGroup == null) return;
        TMP_Text head = aboutGroup.GetComponentInChildren<TMP_Text>(true);
        TMP_FontAsset tf = head != null ? head.font : null;
        TMP_FontAsset bf = descText != null ? descText.font : tf;
        levelPanel = LevelSelectPanel.Create(this, aboutGroup.transform.parent, tf, bf);
        levelsGroup = levelPanel.Group;
        Register(levelsGroup);
    }

    // Teks About: ringkas, hanya info yang memang ada.
    string BuildAboutText()
    {
        return "<size=46><b>" + gameName + "</b></size>\n\n" +
               "Simulasi perakitan PC 3D untuk mengenal komponen komputer.\n\n" +
               "Pasang setiap komponen di tempat yang benar, ikuti urutan perakitan, lalu nyalakan sistemnya.\n\n" +
               "<size=20><color=#00E5FF><cspace=8>DIKEMBANGKAN OLEH</cspace></color></size>\n" +
               "Levizera Sylomita Passah\n" +
               "Tiffany Patricia Laurelle Pondaag\n" +
               "Michelle Sumuweng";
    }

    void Update()
    {
        float t = Time.unscaledTime;
        if (pinkLight != null) pinkLight.intensity = pinkBase * (0.78f + 0.22f * Mathf.Sin(t * 1.6f));
        if (cyanLight != null) cyanLight.intensity = cyanBase * (0.78f + 0.22f * Mathf.Sin(t * 1.25f + 1.9f));
        if (neonMats != null)
            for (int i = 0; i < neonMats.Length; i++)
                if (neonMats[i] != null)
                    neonMats[i].SetColor("_EmissionColor", neonBase[i] * (0.7f + 0.3f * Mathf.Sin(t * 1.1f + i * 1.3f)));

        if (busy) return;
        if (state == State.Main)
        {
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Select(selected + 1, true);
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Select(selected - 1, true);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                if (selected >= 0) Activate(selected);
        }
        else if (state == State.Settings || state == State.About || state == State.Levels)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) Back();
        }
    }

    // ------------------------------------------------------------ kamera
    void SnapCamera()
    {
        pose = poseMain;
        Vector3 fwd = Flat();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        curFocus = (poseMain.focus != null ? poseMain.focus.position : Vector3.zero) - right * poseMain.lateral;
        curDist = poseMain.distance;
        curHeight = poseMain.height;
    }

    Vector3 Flat()
    {
        Vector3 f = new Vector3(viewForward.x, 0f, viewForward.z);
        return f.sqrMagnitude < 1e-4f ? Vector3.forward : f.normalized;
    }

    void LateUpdate()
    {
        if (cam == null || pose == null) return;
        float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
        Vector3 fwd = Flat();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        float k = 1f - Mathf.Exp(-poseSmooth * dt);
        if (pose.focus != null) curFocus = Vector3.Lerp(curFocus, pose.focus.position - right * pose.lateral, k);
        curDist = Mathf.Lerp(curDist, pose.distance, k);
        curHeight = Mathf.Lerp(curHeight, pose.height, k);

        Vector2 m = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width), Input.mousePosition.y / Mathf.Max(1, Screen.height)) * 2f - Vector2.one;
        m = new Vector2(Mathf.Clamp(m.x, -1f, 1f), Mathf.Clamp(m.y, -1f, 1f));
        mouseSmooth = Vector2.Lerp(mouseSmooth, m, 1f - Mathf.Exp(-3f * dt));

        float introMul = Mathf.Lerp(1f + introDolly, 1f, 1f - Mathf.Pow(1f - introT, 3f));
        Vector3 off = -fwd * (curDist * introMul) + Vector3.up * curHeight;
        off = Quaternion.AngleAxis(Mathf.Sin(t * swaySpeed) * swayYaw, Vector3.up) * off;

        Vector3 noise = new Vector3(Mathf.PerlinNoise(t * 0.2f, 0.1f) - 0.5f, Mathf.PerlinNoise(t * 0.23f, 5.3f) - 0.5f, 0f) * 0.02f;
        Vector3 pos = curFocus + off + right * (mouseSmooth.x * parallax) + Vector3.up * (mouseSmooth.y * parallax * 0.5f) + noise;

        cam.transform.position = pos;
        Quaternion look = Quaternion.LookRotation(curFocus - pos, Vector3.up);
        cam.transform.rotation = look * Quaternion.Euler(0f, 0f, Mathf.Sin(t * swaySpeed * 0.8f + 1f) * swayRoll);
    }

    // ------------------------------------------------------------ transisi
    IEnumerator FadeGroup(CanvasGroup g, float to, float dur, float slide)
    {
        if (g == null) yield break;
        RectTransform rt = (RectTransform)g.transform;
        Vector2 bp;
        if (!basePos.TryGetValue(g, out bp)) bp = rt.anchoredPosition;
        float from = g.alpha, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.SmoothStep(from, to, Mathf.Clamp01(t / dur));
            g.alpha = a;
            rt.anchoredPosition = bp + new Vector2(-slide * (1f - a), 0f);
            yield return null;
        }
        g.alpha = to;
        rt.anchoredPosition = bp + new Vector2(-slide * (1f - to), 0f);
    }

    IEnumerator Intro()
    {
        busy = true; state = State.Intro;
        StartCoroutine(IntroDolly());
        yield return new WaitForSecondsRealtime(0.25f);
        yield return FadeGroup(fadeGroup, 0f, 1.6f, 0f);
        if (fadeGroup != null) fadeGroup.blocksRaycasts = false;
        StartCoroutine(FadeGroup(titleGroup, 1f, 1.0f, 0f));
        yield return new WaitForSecondsRealtime(0.5f);
        yield return FadeGroup(menuGroup, 1f, 0.8f, 40f);
        SetInteractive(menuGroup, true);
        state = State.Main; busy = false;
        Select(0, false);
    }

    IEnumerator IntroDolly()
    {
        float t = 0f, dur = 3.6f;
        while (t < dur) { t += Time.unscaledDeltaTime; introT = Mathf.Clamp01(t / dur); yield return null; }
        introT = 1f;
    }

    IEnumerator OpenPanel(CanvasGroup panel, CamPose p, State s)
    {
        busy = true;
        SetInteractive(menuGroup, false);
        StartCoroutine(FadeGroup(titleGroup, 0f, 0.3f, 0f));
        yield return FadeGroup(menuGroup, 0f, 0.28f, 30f);
        pose = p; state = s;
        yield return FadeGroup(panel, 1f, 0.38f, 40f);
        SetInteractive(panel, true);
        busy = false;
    }

    IEnumerator ClosePanel(CanvasGroup panel)
    {
        busy = true;
        SetInteractive(panel, false);
        yield return FadeGroup(panel, 0f, 0.25f, 40f);
        pose = poseMain; state = State.Main;
        StartCoroutine(FadeGroup(titleGroup, 1f, 0.4f, 0f));
        yield return FadeGroup(menuGroup, 1f, 0.35f, 30f);
        SetInteractive(menuGroup, true);
        busy = false;
    }

    IEnumerator PlayRoutine(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("MainMenu: scene '" + sceneName + "' tidak ada di Build Settings.");
            yield break;
        }
        busy = true; state = State.Loading;
        SetInteractive(menuGroup, false);
        // Panel Level Select dibuat belakangan sehingga tergambar di atas layar hitam. Sembunyikan dulu,
        // dan pastikan layar hitam + teks loading ada paling depan, supaya tidak ada UI yang bertumpuk.
        if (levelsGroup != null) { SetInteractive(levelsGroup, false); StartCoroutine(FadeGroup(levelsGroup, 0f, 0.3f, 0f)); }
        if (fadeGroup != null) { fadeGroup.transform.SetAsLastSibling(); fadeGroup.blocksRaycasts = true; }
        StartCoroutine(FadeMusic(0f, 0.9f));
        yield return FadeGroup(fadeGroup, 1f, 0.9f, 0f);
        if (loadingText != null) loadingText.alpha = 1f;
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone) yield return null;
    }

    IEnumerator QuitRoutine()
    {
        busy = true; state = State.Loading;
        SetInteractive(menuGroup, false);
        if (fadeGroup != null) fadeGroup.blocksRaycasts = true;
        StartCoroutine(FadeMusic(0f, 0.7f));
        yield return FadeGroup(fadeGroup, 1f, 0.7f, 0f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------ audio
    void PlayUi(AudioClip c) { if (uiSource != null && c != null) uiSource.PlayOneShot(c); }

    IEnumerator FadeMusic(float to, float dur)
    {
        if (musicSource == null) yield break;
        float from = musicSource.volume, t = 0f;
        while (t < dur) { t += Time.unscaledDeltaTime; musicSource.volume = Mathf.Lerp(from, to, t / dur); yield return null; }
        musicSource.volume = to;
    }
}