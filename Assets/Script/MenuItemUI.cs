using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuItemUI : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [HideInInspector] public int index;
    public System.Action<int> onHover;
    public System.Action<int> onClick;

    public Image highlight;
    public Image accent;
    public TMP_Text label;
    public RectTransform labelRect;

    public float shift = 18f;
    public float speed = 12f;

    static readonly Color Cyan = new Color(0f, 0.9f, 1f, 1f);

    bool selected;
    float t;               // 0 = tidak dipilih, 1 = dipilih
    float baseLabelX;
    bool ready;

    void Awake()
    {
        if (labelRect != null) baseLabelX = labelRect.anchoredPosition.x;
        ready = true;
        Apply(0f);
    }

    public void SetSelected(bool on)
    {
        selected = on;
    }

    void Update()
    {
        float target = selected ? 1f : 0f;
        if (Mathf.Abs(t - target) < 0.001f) { if (t != target) { t = target; Apply(t); } return; }
        t = Mathf.MoveTowards(t, target, speed * Time.unscaledDeltaTime);
        Apply(t);
    }

    void Apply(float v)
    {
        if (!ready) return;
        float e = Mathf.SmoothStep(0f, 1f, v);

        if (highlight != null)
            highlight.color = new Color(0f, 0.9f, 1f, 0.16f * e);

        if (accent != null)
        {
            accent.color = new Color(Cyan.r, Cyan.g, Cyan.b, e);
            RectTransform ar = accent.rectTransform;
            ar.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, e), 1f);
        }

        if (labelRect != null)
        {
            Vector2 p = labelRect.anchoredPosition;
            p.x = baseLabelX + shift * e;
            labelRect.anchoredPosition = p;
        }

        if (label != null)
            label.color = Color.Lerp(new Color(1f, 1f, 1f, 0.65f), Color.white, e);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (onHover != null) onHover(index);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (onClick != null) onClick(index);
    }
}