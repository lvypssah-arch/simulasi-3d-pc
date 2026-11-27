using System.Collections;
using UnityEngine;

public class SeatedLook : MonoBehaviour
{
    public static SeatedLook I;

    public Transform cam;                          // Main Camera (anak Player)
    public Transform pivotOverride;                // opsional: objek yang jadi pusat pandang

    // Dipertahankan hanya supaya menu 6 di SocketTools.cs tetap bisa dikompilasi.
    // Tidak berpengaruh: posisi awal kini dihitung dari pusat slot perakitan.
    [HideInInspector] public bool useTransformAsStart = false;

    [Header("Posisi awal")]
    [Range(0.3f, 1f)] public float startZoom = 0.6f;     // 1 = jarak lama, makin kecil makin dekat
    public float startMinPitch = 22f;                    // sudut turun minimal saat mulai (derajat)

    [Header("Rotasi (klik kanan + geser)")]
    public float rotateSpeed = 3f;
    public float yawLimit = 150f;                        // batas putar kiri/kanan dari posisi awal
    public Vector2 pitchLimit = new Vector2(10f, 80f);   // plus = menunduk

    [Header("Zoom (scroll)")]
    public float zoomSpeed = 0.18f;                      // persentase jarak per klik scroll
    public float minDistance = 0.45f;
    public float maxDistanceFactor = 1.6f;               // maksimum = jarak awal x angka ini

    [Header("Pan (klik tengah + geser)")]
    public float panSpeed = 1f;
    public float panRadius = 1.2f;                       // batas geser dari pusat awal (meter)

    [Header("Kehalusan")]
    public float smooth = 12f;

    [Header("Fokus saat memegang komponen")]
    public float focusFovDrop = 6f;
    [Range(0f, 1f)] public float focusPull = 0.3f;       // seberapa jauh pusat bergeser ke slot tujuan
    public float focusSpeed = 4f;

    // keadaan target (diubah input) dan keadaan tampil (dihaluskan)
    float yaw, pitch, dist;
    float tYaw, tPitch, tDist;
    Vector3 pivot, tPivot, homePivot;
    float homeYaw, homePitch, homeDist, maxDist;
    float tableY;

    Camera camComp;
    float baseFov = 65f;
    Vector3 shakeOffset;

    bool focusOn;
    Vector3 focusPoint;
    float blend;

    bool dragRot, dragPan, ready;

    void Awake()
    {
        I = this;
        if (cam == null)
        {
            Camera c = GetComponentInChildren<Camera>();
            if (c != null) cam = c.transform;
        }
        if (cam == null)
        {
            Debug.LogError("SeatedLook: field Cam kosong dan tidak ada kamera di dalam Player.");
            enabled = false;
        }
    }

    void Start()
    {
        if (!enabled) return;
        camComp = cam.GetComponent<Camera>();
        if (camComp != null) baseFov = camComp.fieldOfView;

        // pusat pandang: pusat semua slot perakitan
        Vector3 center;
        tableY = cam.position.y - 1f;
        if (pivotOverride != null) center = pivotOverride.position;
        else
        {
            center = cam.position + cam.forward * 1.5f;
            var m = FindObjectOfType<AssemblyManager>();
            if (m != null && m.allSockets != null && m.allSockets.Length > 0)
            {
                Bounds b = new Bounds();
                bool first = true;
                float minY = float.MaxValue;
                foreach (var s in m.allSockets)
                {
                    if (s == null) continue;
                    if (first) { b = new Bounds(s.transform.position, Vector3.zero); first = false; }
                    else b.Encapsulate(s.transform.position);
                    minY = Mathf.Min(minY, s.transform.position.y);
                }
                if (!first) { center = b.center; tableY = minY; }
            }
        }

        Vector3 dir = center - cam.position;
        float d0 = Mathf.Max(dir.magnitude, 0.8f);
        float flat = new Vector2(dir.x, dir.z).magnitude;
        homeYaw = flat > 0.01f ? Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg : cam.eulerAngles.y;
        homePitch = flat > 0.01f ? Mathf.Atan2(-dir.y, flat) * Mathf.Rad2Deg : 25f;
        homePitch = Mathf.Clamp(Mathf.Max(homePitch, startMinPitch), pitchLimit.x, pitchLimit.y);
        homeDist = Mathf.Max(minDistance + 0.15f, d0 * startZoom);
        maxDist = Mathf.Max(homeDist * maxDistanceFactor, homeDist + 0.5f);
        homePivot = center;

        ResetView(true);
        ready = true;
    }

    public void ResetView(bool instant)
    {
        tYaw = homeYaw; tPitch = homePitch; tDist = homeDist; tPivot = homePivot;
        if (instant) { yaw = tYaw; pitch = tPitch; dist = tDist; pivot = tPivot; Apply(); }
    }

    public void FocusOn(Vector3 worldPoint) { focusPoint = worldPoint; focusOn = true; }
    public void ClearFocus() { focusOn = false; }

    void Update()
    {
        if (!ready || Time.timeScale == 0f) return;

        bool overUI = Util.PointerOverUI();

        // drag hanya sah kalau dimulai di luar UI
        if (Input.GetMouseButtonDown(1)) dragRot = !overUI;
        if (Input.GetMouseButtonUp(1)) dragRot = false;
        if (Input.GetMouseButtonDown(2)) dragPan = !overUI;
        if (Input.GetMouseButtonUp(2)) dragPan = false;

        if (dragRot && Input.GetMouseButton(1))
        {
            tYaw += Input.GetAxis("Mouse X") * rotateSpeed;
            tPitch -= Input.GetAxis("Mouse Y") * rotateSpeed;
        }
        if (dragPan && Input.GetMouseButton(2))
        {
            Vector3 right = cam.right; right.y = 0f; right.Normalize();
            Vector3 fwd = cam.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            float k = panSpeed * dist * 0.025f;
            tPivot -= right * (Input.GetAxis("Mouse X") * k) + fwd * (Input.GetAxis("Mouse Y") * k);
        }

        float scroll = overUI ? 0f : Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f) tDist *= 1f - scroll * zoomSpeed;

        // dua jari: putar (layar sentuh)
        if (Input.touchCount == 2)
        {
            Vector2 d = (Input.GetTouch(0).deltaPosition + Input.GetTouch(1).deltaPosition) * 0.5f;
            tYaw += d.x * 0.08f;
            tPitch -= d.y * 0.08f;
        }

        // keyboard: putar pelan
        tYaw += Input.GetAxisRaw("Horizontal") * 50f * Time.deltaTime;
        tPitch -= Input.GetAxisRaw("Vertical") * 35f * Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.C)) ResetView(false);

        // batas
        tYaw = Mathf.Clamp(tYaw, homeYaw - yawLimit, homeYaw + yawLimit);
        tPitch = Mathf.Clamp(tPitch, pitchLimit.x, pitchLimit.y);
        tDist = Mathf.Clamp(tDist, minDistance, maxDist);
        Vector3 off = tPivot - homePivot; off.y = 0f;
        if (off.magnitude > panRadius) off = off.normalized * panRadius;
        tPivot = new Vector3(homePivot.x + off.x, homePivot.y, homePivot.z + off.z);

        // haluskan
        float f = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);
        yaw = Mathf.Lerp(yaw, tYaw, f);
        pitch = Mathf.Lerp(pitch, tPitch, f);
        dist = Mathf.Lerp(dist, tDist, f);
        pivot = Vector3.Lerp(pivot, tPivot, f);

        blend = Mathf.MoveTowards(blend, focusOn ? 1f : 0f, focusSpeed * Time.unscaledDeltaTime);
        Apply();
    }

    void Apply()
    {
        float s = Mathf.SmoothStep(0f, 1f, blend);
        Vector3 p = s > 0f ? Vector3.Lerp(pivot, focusPoint, focusPull * s) : pivot;

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pos = p - rot * Vector3.forward * dist;
        pos.y = Mathf.Max(pos.y, tableY + 0.12f);          // jangan menembus meja

        cam.rotation = rot;
        cam.position = pos + shakeOffset;
        if (camComp != null) camComp.fieldOfView = baseFov - focusFovDrop * s;
    }

    public void Shake(float duration = 0.25f, float magnitude = 0.012f)
    {
        if (cam == null) return;
        StopAllCoroutines();
        shakeOffset = Vector3.zero;
        StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            shakeOffset = Random.insideUnitSphere * magnitude * (1f - t / duration);
            yield return null;
        }
        shakeOffset = Vector3.zero;
    }
}