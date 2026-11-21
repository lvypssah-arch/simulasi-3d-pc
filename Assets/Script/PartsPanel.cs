using System.Collections;
using System.Collections.Generic;
using UnityEngine;
 
public class PartsPanel : MonoBehaviour
{
    public RectTransform content;      // Content milik Scroll View
    public PartCard cardPrefab;
    public CanvasGroup group;
 
    readonly Dictionary<PartType, PartCard> cards = new Dictionary<PartType, PartCard>();
    Vector3 baseScale;
    bool isOpen;
    Coroutine anim;
 
    void Awake()
    {
        baseScale = transform.localScale;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        transform.localScale = baseScale * 0.85f;
    }
 
    public void Build(PartData[] parts, AssemblyManager mgr)
    {
        foreach (var p in parts)
        {
            PartCard c = Instantiate(cardPrefab, content);
            c.Setup(p, mgr);
            cards[p.type] = c;
        }
    }
 
    public void Refresh(Dictionary<PartType, int> remaining)
    {
        foreach (var kv in cards) kv.Value.SetRemaining(remaining[kv.Key]);
    }
 
    public void Toggle() { SetOpen(!isOpen); }
 
    public void SetOpen(bool open)
    {
        isOpen = open;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Animate(open));
    }
 
    IEnumerator Animate(bool open)
    {
        float from = group.alpha, to = open ? 1f : 0f;
        if (open) { group.interactable = true; group.blocksRaycasts = true; }
        float t = 0f, dur = 0.25f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur));
            transform.localScale = baseScale * Mathf.Lerp(0.85f, 1f, group.alpha);
            yield return null;
        }
        group.alpha = to;
        transform.localScale = baseScale * (open ? 1f : 0.85f);
        if (!open) { group.interactable = false; group.blocksRaycasts = false; }
    }
}
