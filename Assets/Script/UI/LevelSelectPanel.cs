using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Panel Level Select di Main Menu. Dibuat lewat kode di bawah canvas Main Menu,
// jadi tidak perlu menjalankan MainMenuBuilder ulang atau mengubah scene.
// Kontrol: A / D atau panah = pilih, ENTER / klik = mulai, ESC atau tombol KEMBALI = kembali.
public class LevelSelectPanel : MonoBehaviour
{
    public CanvasGroup Group { get; private set; }

    MainMenuController menu;
    LevelCard[] cards;
    int selected = -1;

    public static LevelSelectPanel Create(MainMenuController menu, Transform canvasRoot,
                                          TMP_FontAsset titleFont, TMP_FontAsset bodyFont)
    {
        UIKit.UseFonts(titleFont, bodyFont);
        var rt = UIKit.Rect("LevelSelectPanel", canvasRoot);
        UIKit.TopLeft(rt, 140f, 170f, 1240f, 760f);
        var p = rt.gameObject.AddComponent<LevelSelectPanel>();
        p.Group = rt.gameObject.AddComponent<CanvasGroup>();
        p.menu = menu;
        p.Build(rt);
        p.Group.alpha = 0f; p.Group.interactable = false; p.Group.blocksRaycasts = false;
        return p;
    }

    void Build(RectTransform root)
    {
        var head = UIKit.Label(root, "Header", "PILIH LEVEL", 64f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold, 12f, true);
        UIKit.TopLeft(head.rectTransform, 0f, 0f, 1000f, 80f);
        var line = UIKit.Img(root, "Line", UIKit.Pink);
        UIKit.TopLeft(line.rectTransform, 0f, 96f, 700f, 2f);

        var levels = GameLevels.All;
        cards = new LevelCard[levels.Length];
        for (int i = 0; i < levels.Length; i++)
        {
            var info = levels[i];
            bool missing = !Application.CanStreamedLevelBeLoaded(info.scene);
            bool locked = GameLevels.IsLocked(info) || missing;
            string note = missing ? "SCENE BELUM ADA DI BUILD SETTINGS" : (locked ? "TERKUNCI" : "");
            cards[i] = LevelCard.Create(root, info, i, locked, note, OnHover, OnClick);
            UIKit.TopLeft((RectTransform)cards[i].transform, i * 560f, 150f, 530f, 400f);
        }

        var back = UIKit.MakeButton(root, "Back", "KEMBALI", UIKit.Pink, new Vector2(240f, 56f), menu.BackFromLevels, 24f);
        UIKit.TopLeft((RectTransform)back.transform, 0f, 600f, 240f, 56f);

        var hint = UIKit.Label(root, "Hint", "A / D   PILIH        ENTER   MULAI        ESC   KEMBALI", 20f,
            new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.TopLeft, FontStyles.Normal, 6f, false);
        UIKit.TopLeft(hint.rectTransform, 0f, 690f, 900f, 30f);
    }

    // dipanggil controller setelah panel selesai fade-in
    public void OnShown()
    {
        for (int i = 0; i < cards.Length; i++) cards[i].RefreshBest();
        Select(selected >= 0 ? selected : FirstAvailable(), false);
    }

    int FirstAvailable()
    {
        for (int i = 0; i < cards.Length; i++) if (!cards[i].locked) return i;
        return 0;
    }

    void OnHover(int i) { if (CanInput()) Select(i, true); }
    void OnClick(int i) { if (!CanInput()) return; Select(i, false); Launch(i); }
    bool CanInput() { return Group != null && Group.interactable && !menu.Busy; }

    void Select(int i, bool fromHover)
    {
        if (cards == null || cards.Length == 0) return;
        i = (i + cards.Length) % cards.Length;
        selected = i;
        for (int k = 0; k < cards.Length; k++) cards[k].SetSelected(k == i);
    }

    void Launch(int i)
    {
        if (cards[i].locked) return;
        menu.StartLevel(cards[i].info.scene);
    }

    void Update()
    {
        if (!CanInput()) return;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            Select(selected + 1, false);
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            Select(selected - 1, false);
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) && selected >= 0)
            Launch(selected);
    }
}

public class LevelCard : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public GameLevels.Info info;
    public bool locked;

    int index;
    System.Action<int> onHover, onClick;
    Image bg, bar, top;
    RectTransform rt;
    TMP_Text number, title, desc, status;
    string lockNote;
    bool selected;
    float t;

    public static LevelCard Create(Transform parent, GameLevels.Info info, int index, bool locked, string lockNote,
                                   System.Action<int> onHover, System.Action<int> onClick)
    {
        var bgImg = UIKit.Img(parent, "Card_" + info.number, new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.86f), true);
        var c = bgImg.gameObject.AddComponent<LevelCard>();
        c.info = info; c.index = index; c.locked = locked; c.lockNote = lockNote;
        c.onHover = onHover; c.onClick = onClick;
        c.bg = bgImg; c.rt = bgImg.rectTransform;

        c.top = UIKit.Img(c.rt, "Top", Color.white); c.top.sprite = UIKit.Grad();
        c.top.rectTransform.anchorMin = new Vector2(0, 1); c.top.rectTransform.anchorMax = new Vector2(1, 1);
        c.top.rectTransform.pivot = new Vector2(0.5f, 1f); c.top.rectTransform.sizeDelta = new Vector2(0, 3);
        c.top.rectTransform.anchoredPosition = Vector2.zero;

        c.bar = UIKit.Img(c.rt, "Bar", UIKit.Cyan);
        c.bar.rectTransform.anchorMin = new Vector2(0, 0); c.bar.rectTransform.anchorMax = new Vector2(0, 1);
        c.bar.rectTransform.pivot = new Vector2(0, 0.5f); c.bar.rectTransform.sizeDelta = new Vector2(5f, 0);
        c.bar.rectTransform.anchoredPosition = Vector2.zero;

        c.number = UIKit.Label(c.rt, "Number", info.number.ToString("00"), 96f, UIKit.Cyan, TextAlignmentOptions.TopLeft, FontStyles.Bold, 4f, true);
        UIKit.TopLeft(c.number.rectTransform, 36f, 28f, 300f, 120f);
        c.title = UIKit.Label(c.rt, "Title", info.title, 38f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold, 8f, true);
        UIKit.TopLeft(c.title.rectTransform, 36f, 160f, 470f, 56f);
        c.desc = UIKit.Label(c.rt, "Desc", info.description, 24f, new Color(1f, 1f, 1f, 0.72f), TextAlignmentOptions.TopLeft, FontStyles.Normal, 2f, false);
        UIKit.TopLeft(c.desc.rectTransform, 36f, 226f, 460f, 100f);
        c.status = UIKit.Label(c.rt, "Status", "", 20f, UIKit.Cyan, TextAlignmentOptions.BottomLeft, FontStyles.Bold, 6f, false);
        UIKit.TopLeft(c.status.rectTransform, 36f, 340f, 470f, 36f);

        c.RefreshBest();
        c.Apply(0f);
        return c;
    }

    public void RefreshBest()
    {
        if (locked) { status.text = lockNote; status.color = UIKit.Pink; return; }
        int best = PlayerPrefs.GetInt("best_" + info.levelId, 0);
        status.text = best > 0 ? "SKOR TERBAIK   " + best : "BELUM DIMAINKAN";
        status.color = best > 0 ? UIKit.Cyan : new Color(1f, 1f, 1f, 0.45f);
    }

    public void SetSelected(bool on) { selected = on; }

    void Update()
    {
        float target = selected ? 1f : 0f;
        if (Mathf.Abs(t - target) < 0.001f) return;
        t = Mathf.MoveTowards(t, target, 8f * Time.unscaledDeltaTime);
        Apply(t);
    }

    void Apply(float v)
    {
        float e = Mathf.SmoothStep(0f, 1f, v);
        float dim = locked ? 0.35f : 1f;
        bg.color = Color.Lerp(new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.86f), new Color(0.04f, 0.13f, 0.18f, 0.95f), e);
        bar.color = new Color(UIKit.Cyan.r, UIKit.Cyan.g, UIKit.Cyan.b, e * dim);
        top.color = new Color(1f, 1f, 1f, (0.35f + 0.65f * e) * dim);
        number.color = new Color(UIKit.Cyan.r, UIKit.Cyan.g, UIKit.Cyan.b, (0.45f + 0.55f * e) * dim);
        title.color = new Color(1f, 1f, 1f, (0.75f + 0.25f * e) * dim);
        desc.color = new Color(1f, 1f, 1f, 0.72f * dim);
        rt.localScale = Vector3.one * (1f + 0.025f * e);
    }

    public void OnPointerEnter(PointerEventData e) { if (onHover != null) onHover(index); }
    public void OnPointerClick(PointerEventData e) { if (onClick != null) onClick(index); }
}
