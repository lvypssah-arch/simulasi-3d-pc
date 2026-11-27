using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PartsPanel : MonoBehaviour
{
    public RectTransform content;      // Content milik Scroll View
    public PartCard cardPrefab;
    public CanvasGroup group;
    public RectTransform window;       // bagian yang dianimasikan (anak "Window"), diisi menu 13

    [Header("Keterbacaan kartu")]
    public bool enforceFonts = true;
    public float nameMin = 20f, nameMax = 26f;
    public float specMin = 14f, specMax = 18f;
    public float countMax = 30f;

    readonly Dictionary<PartType, PartCard> cards = new Dictionary<PartType, PartCard>();
    RectTransform target;
    Vector3 baseScale;
    bool isOpen;
    Coroutine anim;

    void Awake()
    {
        if (window == null)
        {
            Transform w = transform.Find("Window");
            if (w != null) window = w as RectTransform;
        }
        target = window != null ? window : (RectTransform)transform;
        baseScale = target.localScale;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        target.localScale = baseScale * 0.85f;
    }

    public void Build(PartData[] parts, AssemblyManager mgr)
    {
        foreach (var p in parts)
        {
            PartCard c = Instantiate(cardPrefab, content);
            c.Setup(p, mgr);
            if (enforceFonts) c.MakeReadable(nameMin, nameMax, specMin, specMax, countMax);
            cards[p.type] = c;
        }
    }

    public void Refresh(Dictionary<PartType, int> remaining)
    {
        foreach (var kv in cards) kv.Value.SetRemaining(remaining[kv.Key]);
    }

    // Menyalakan kartu komponen yang harus dipasang berikutnya (null = tidak ada)
    public void Highlight(PartType? type)
    {
        foreach (var kv in cards)
            kv.Value.SetHighlight(type.HasValue && kv.Key == type.Value);
    }
    public void SetLock(PartType type, bool locked, string reason)
    
    {
        PartCard c;
        if (cards.TryGetValue(type, out c)) c.SetLocked(locked, reason);
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
            target.localScale = baseScale * Mathf.Lerp(0.85f, 1f, group.alpha);
            yield return null;
        }
        group.alpha = to;
        target.localScale = baseScale * (open ? 1f : 0.85f);
        if (!open) { group.interactable = false; group.blocksRaycasts = false; }
    }
}