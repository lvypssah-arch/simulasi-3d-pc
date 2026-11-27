using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PartCard : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text specText;
    public TMP_Text countText;
    public Button button;
    public CanvasGroup group;

    Outline glow;
    bool highlight;
    int remainingCount;
    bool locked;
    string lockReason = "";
    string normalSpec = "";

    public void Setup(PartData d, AssemblyManager mgr)
    {
        if (button == null || nameText == null || specText == null
            || countText == null || group == null)
        {
            Debug.LogError("PartCard: ada field yang belum diisi pada prefab PartCard.");
            return;
        }
        if (icon != null && d.icon != null) icon.sprite = d.icon;
        nameText.text = d.displayName;
        normalSpec = string.IsNullOrEmpty(d.specs) ? "" : d.specs.Split('\n')[0];
        specText.text = normalSpec;
        SetRemaining(d.copies);
        button.onClick.AddListener(() => mgr.PickPart(d));
    }

    public void SetRemaining(int n)
    {
        remainingCount = n;
        Apply();
    }

    // Kartu dikunci sampai slot prasyaratnya terpasang
    public void SetLocked(bool on, string reason)
    {
        if (on == locked && reason == lockReason) return;
        locked = on;
        lockReason = reason ?? "";
        Apply();
    }

    void Apply()
    {
        if (countText == null || button == null || group == null || specText == null) return;
        int n = remainingCount;
        countText.text = n > 1 ? "x" + n : (n == 1 ? "" : "OK");
        bool usable = n > 0 && !locked;
        button.interactable = usable;
        group.alpha = n <= 0 ? 0.45f : (locked ? 0.6f : 1f);
        specText.text = (locked && n > 0) ? "<color=#FFC24A>" + lockReason + "</color>" : normalSpec;
        if (!usable && highlight) SetHighlight(false);
    }

    static void Fit(TMP_Text t, float min, float max)
    {
        if (t == null) return;
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
    }

    public void MakeReadable(float nameMin, float nameMax, float specMin, float specMax, float countMax)
    {
        Fit(nameText, nameMin, nameMax);
        Fit(specText, specMin, specMax);
        Fit(countText, 16f, countMax);
    }

    public void SetHighlight(bool on)
    {
        highlight = on;
        if (glow == null)
        {
            Graphic g = (button != null && button.targetGraphic != null)
                ? button.targetGraphic : GetComponentInChildren<Graphic>();
            if (g == null) return;
            glow = g.gameObject.GetComponent<Outline>();
            if (glow == null) glow = g.gameObject.AddComponent<Outline>();
            glow.effectDistance = new Vector2(4f, -4f);
            glow.enabled = false;
        }
        glow.enabled = on;
    }

    void Update()
    {
        if (!highlight || glow == null) return;
        float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        glow.effectColor = new Color(1f, 0.85f, 0.1f, Mathf.Lerp(0.4f, 1f, p));
    }
}