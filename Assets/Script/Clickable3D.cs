using UnityEngine;
 
[RequireComponent(typeof(Collider))]
public class Clickable3D : MonoBehaviour
{
    public static event System.Action<string> OnClicked;
 
    public string actionId = "parts";   // parts / hint / reset / power
    public bool interactable = true;
    public bool pulse = false;          // berdenyut (dipakai tombol power)
    public float hoverScale = 1.12f;
 
    Vector3 baseScale;
    bool hover;
    float pressTimer;
 
    void Awake() { baseScale = transform.localScale; }
 
    public void SetHover(bool h) { hover = h && interactable; }
 
    public void Press()
    {
        if (!interactable) return;
        pressTimer = 0.15f;
        if (OnClicked != null) OnClicked(actionId);
    }
 
    void Update()
    {
        float target = hover ? hoverScale : 1f;
        if (pulse) target *= 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);
        if (pressTimer > 0f) { pressTimer -= Time.unscaledDeltaTime; target *= 0.85f; }
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * target,
                                            12f * Time.unscaledDeltaTime);
    }
}
