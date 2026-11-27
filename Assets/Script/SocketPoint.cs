using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SocketPoint : MonoBehaviour
{
    public PartType accepts;
    public string friendlyName = "slot";
    public SocketPoint[] requires;           // slot yang HARUS terisi lebih dulu

    public GameObject installed { get; private set; }
    public bool Occupied { get { return installed != null; } }

    GameObject ghost;
    Material ghostMat;
    Color ghostColor = Color.cyan;
    float ghostStrength = 1f;

    public SocketPoint MissingRequirement()
    {
        if (requires == null) return null;
        foreach (var r in requires)
        {
            if (r == null || r.Occupied) continue;
            SocketPoint deeper = r.MissingRequirement();
            return deeper != null ? deeper : r;
        }
        return null;
    }

    Vector3 LocalScaleForWorld(Vector3 world)
    {
        Vector3 p = transform.lossyScale;
        return new Vector3(
            Mathf.Abs(p.x) > 1e-5f ? world.x / p.x : world.x,
            Mathf.Abs(p.y) > 1e-5f ? world.y / p.y : world.y,
            Mathf.Abs(p.z) > 1e-5f ? world.z / p.z : world.z);
    }

    // strength: 1 = terang (langkah berikutnya), <1 = redup
    public void ShowGhost(GameObject prefab, Material template, Color color, float strength = 1f)
    {
        HideGhost();
        if (prefab == null || template == null) return;

        ghost = Instantiate(prefab, transform);
                PartSanitizer.Strip(ghost, true);
        ghost.name = "Hologram";
        ghost.transform.localPosition = Vector3.zero;
        ghost.transform.localRotation = prefab.transform.localRotation;
        ghost.transform.localScale = LocalScaleForWorld(prefab.transform.localScale);
        foreach (var c in ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var b in ghost.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;

        ghostMat = new Material(template);
        foreach (var r in ghost.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = ghostMat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        ghostColor = color;
        ghostStrength = Mathf.Clamp01(strength);
    }

    public void HideGhost()
    {
        if (ghost != null) Destroy(ghost);
        if (ghostMat != null) Destroy(ghostMat);
        ghost = null;
        ghostMat = null;
    }

    void Update()
    {
        if (ghostMat == null) return;
        float speed = ghostStrength >= 0.99f ? 5f : 3f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
        Color c = ghostColor;
        c.a = Mathf.Lerp(0.25f, 0.65f, pulse) * ghostStrength;
        ghostMat.SetColor("_BaseColor", c);
    }

    public void Install(GameObject part, Quaternion targetRot)
    {
        installed = part;
        HideGhost();
        part.transform.SetParent(transform, true);
        StartCoroutine(Snap(part.transform, targetRot));
    }

    IEnumerator Snap(Transform t, Quaternion targetRot)
    {
        Vector3 p0 = t.localPosition, sBase = t.localScale;
        Quaternion r0 = t.localRotation;
        float time = 0f, dur = 0.3f;
        while (time < dur)
        {
            if (t == null) yield break;
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / dur);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            t.localPosition = Vector3.LerpUnclamped(p0, Vector3.zero, e);
            t.localRotation = Quaternion.Slerp(r0, targetRot, e);
            t.localScale = sBase * (1f + 0.06f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        if (t == null) yield break;
        t.localPosition = Vector3.zero;
        t.localRotation = targetRot;
        t.localScale = sBase;
    }

    void OnDrawGizmos()
    {
        var b = GetComponent<BoxCollider>();
        if (b == null) return;
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}