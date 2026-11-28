using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AssemblyManager : MonoBehaviour
{
    public static AssemblyManager I;

    public enum OrientationMode { SnapToCorrect, KeepPlayerRotation, RequireMatch }

    [Header("Pengaturan level")]
    public string levelId = "Entry";
    public string levelTitle = "LEVEL ENTRY";
    public bool showHologram = true;
    public bool allowHint = false;
    public int scoreCorrect = 100;
    public int penaltyWrongType = 50;
    public int penaltyWrongOrder = 30;
    public int penaltyOccupied = 20;
    public int hintCost = 50;
    public int maxMistakes = 0;          // 0 = tidak dibatasi
    public float timeLimit = 0f;         // detik, 0 = tanpa batas
    public string nextScene = "";

    [Header("Panduan hologram (Level Entry)")]
    public bool guideFromStart = true;       // hologram slot yang siap tampil sejak awal
    [Range(0.1f, 1f)] public float guideDimStrength = 0.45f;   // slot selain langkah berikutnya
    public bool openPanelAtStart = true;
    public bool closePanelOnPick = false;
    public bool caseSeeThrough = true;       // casing jadi transparan setelah dipasang
    [Range(0.05f, 0.6f)] public float caseAlpha = 0.22f;
    public GameObject screenButtonBar;       // diisi menu 14
    public GameObject hintButtonUI;          // diisi menu 14
    public bool focusOnHold = true;          // kamera menoleh dan zoom ringan ke slot tujuan
    public bool lockUnavailableParts = true; // kartu dikunci sampai prasyarat terpasang

    [Header("Rotasi komponen")]
    public OrientationMode orientationMode = OrientationMode.SnapToCorrect;
    public float rotateSpeed = 120f;         // derajat per detik
    public float matchTolerance = 25f;       // dipakai mode RequireMatch
    public int penaltyWrongOrientation = 20;

    [Header("Referensi scene")]
    public Camera cam;
    public LayerMask socketMask;
    public Material hologramMaterial;
    public PartData[] parts;
    public SocketPoint[] allSockets;
    public PartsPanel panel;
    public HUDController hud;
    public GameObject hintButton3D;

    [Header("Gerak komponen")]
    public float holdLift = 0.2f;
    public float holdHeight = 0.95f;
    public float followSpeed = 18f;
    public bool compensatePivot = true;
    public float maxHoldDistance = 3f;
    public float maxRayDistance = 30f;

    [Header("Warna hologram")]
    public Color holoOk = new Color(0f, 0.9f, 1f);
    public Color holoWait = new Color(1f, 0.7f, 0f);
    public Color holoWrong = new Color(1f, 0.15f, 0.15f);

    [Header("Efek PC menyala")]
    public Renderer monitorScreen;
    public Material screenOnMaterial;
    public TMPro.TMP_Text monitorText;

    [Header("Audio dan FX")]
    public AudioSource sfx;
    public AudioClip clipPick, clipPlace, clipError, clipPower, clipWin, clipFail;
    public GameObject snapFx;

    public bool IsHolding { get { return held != null; } }

    // urutan pemasangan yang disarankan (menentukan "langkah berikutnya")
    static readonly PartType[] Order =
    {
        PartType.Case, PartType.Mobo, PartType.CPU, PartType.Cooler, PartType.RAM,
        PartType.GPU, PartType.PSU, PartType.Cable, PartType.SSD, PartType.Fan,
        PartType.Keyboard, PartType.Mouse
    };

    GameObject held;
    PartData heldData;
    Light caseLight;
    readonly Dictionary<PartType, int> remaining = new Dictionary<PartType, int>();
    readonly List<SocketPoint> shown = new List<SocketPoint>();
    int totalSteps, placed, score, mistakes;
    float elapsed;
    bool finished, powerStarted;

    Quaternion userRot = Quaternion.identity;   // putaran pemain di atas orientasi dasar
    float tableY;
    static bool rotateHintShown;

    // ------------------------------------------------------------ siklus hidup
    void Awake() { I = this; Time.timeScale = 1f; }
    void OnEnable()  { Clickable3D.OnClicked += HandleClick; }
    void OnDisable() { Clickable3D.OnClicked -= HandleClick; }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("AssemblyManager: kamera tidak ditemukan. Beri tag MainCamera pada kamera Player.");
            enabled = false; return;
        }
        if (panel == null || hud == null)
        {
            Debug.LogError("AssemblyManager: field Panel atau Hud belum diisi.");
            enabled = false; return;
        }

        parts = parts == null ? new PartData[0] : System.Array.FindAll(parts, p => p != null);
        allSockets = allSockets == null ? new SocketPoint[0]
                                        : System.Array.FindAll(allSockets, s => s != null);
        if (parts.Length == 0)
        {
            Debug.LogError("AssemblyManager: list Parts kosong.");
            enabled = false; return;
        }

        foreach (var p in parts)
        {
            if (remaining.ContainsKey(p.type))
            {
                Debug.LogWarning("[CEK] Jenis " + p.type + " muncul lebih dari sekali di Parts (" + p.name + ").");
                continue;
            }
            remaining[p.type] = p.copies;
            totalSteps += p.copies;
        }

        panel.Build(parts, this);
        hud.Init(levelTitle, totalSteps, maxMistakes);
        hud.SetTime(timeLimit > 0f ? timeLimit : 0f);

        // tombol: pakai tombol layar kalau barnya ada, tombol 3D di meja dimatikan
        if (screenButtonBar != null)
        {
            screenButtonBar.SetActive(true);
            foreach (var c in FindObjectsOfType<Clickable3D>(true))
                if (c.actionId == "parts" || c.actionId == "hint" || c.actionId == "reset")
                    c.gameObject.SetActive(false);
            if (hintButtonUI != null) hintButtonUI.SetActive(allowHint);
        }
        else if (hintButton3D != null) hintButton3D.SetActive(allowHint);

        // bidang gerak komponen mengikuti slot yang paling rendah (permukaan meja)
        if (allSockets.Length > 0)
        {
            float baseY = float.MaxValue;
            foreach (var s in allSockets) baseY = Mathf.Min(baseY, s.transform.position.y);
            holdHeight = baseY + holdLift;
            tableY = baseY;
        }
        Validate();
        WarnStrayCameras();

        // Petunjuk awal tidak lagi ditampilkan sebagai toast; semua kontrol ada di panel Panduan (tombol ? / H).

        RefreshHolograms();
        if (openPanelAtStart) panel.SetOpen(true);
    }

    void Validate()
    {
        int layer = LayerMask.NameToLayer("Socket");
        if (layer < 0) Debug.LogWarning("[CEK] Layer bernama Socket belum dibuat.");
        else if ((socketMask.value & (1 << layer)) == 0)
            Debug.LogWarning("[CEK] Socket Mask belum mencakup layer Socket.");
        if (hologramMaterial == null && showHologram)
            Debug.LogWarning("[CEK] Hologram Material kosong, hologram tidak akan tampil.");

        foreach (var p in parts)
        {
            if (p.prefab == null) Debug.LogWarning("[CEK] PartData " + p.name + ": Prefab kosong.");
            int slots = 0;
            foreach (var s in allSockets) if (s.accepts == p.type) slots++;
            if (slots < p.copies)
                Debug.LogWarning("[CEK] " + p.name + " butuh " + p.copies + " slot " + p.type
                    + " di All Sockets, ditemukan " + slots + ". Level tidak akan bisa selesai.");
        }
        foreach (var s in allSockets)
        {
            if (layer >= 0 && s.gameObject.layer != layer)
                Debug.LogWarning("[CEK] " + s.name + ": Layer bukan Socket.");
            if (!remaining.ContainsKey(s.accepts))
                Debug.LogWarning("[CEK] " + s.name + " menerima " + s.accepts
                    + " tetapi tidak ada PartData untuk jenis itu di Parts.");
        }
    }

    void Update()
    {
        if (finished || Time.timeScale == 0f) return;

        if (!powerStarted) elapsed += Time.deltaTime;
        if (timeLimit > 0f)
        {
            float left = timeLimit - elapsed;
            hud.SetTime(left);
            if (left <= 0f) { Fail("Waktu habis!"); return; }
        }
        else hud.SetTime(elapsed);

        if (Input.GetKeyDown(KeyCode.Tab)) panel.Toggle();

        if (held == null) return;
        MoveHeld();
        HandleRotateInput();

        if (Input.GetMouseButtonDown(0))
        {
            if (Util.PointerOverUI())
                Debug.LogWarning("Klik diblokir UI: " + Util.UnderPointerNames());
            else
                TryPlace();
        }
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) CancelHold();
    }

    // ------------------------------------------------------------ helper
    PartData DataOf(PartType t)
    {
        foreach (var p in parts) if (p.type == t) return p;
        return null;
    }

    string NameOf(PartType t)
    {
        PartData d = DataOf(t);
        return d != null ? d.displayName : t.ToString();
    }

    int Remaining(PartType t)
    {
        int n;
        return remaining.TryGetValue(t, out n) ? n : 0;
    }

    void Play(AudioClip c) { if (sfx != null && c != null) sfx.PlayOneShot(c); }

    // Slot yang paling pas dipasang sekarang: urutan Order, prasyarat sudah terpenuhi
    SocketPoint NextSlot()
    {
        foreach (var t in Order)
        {
            if (Remaining(t) <= 0) continue;
            foreach (var s in allSockets)
                if (s.accepts == t && !s.Occupied && s.MissingRequirement() == null) return s;
        }
        return null;
    }

    string NextText()
    {
        SocketPoint n = NextSlot();
        return n != null ? "  Berikutnya: " + NameOf(n.accepts) + "." : "";
    }

    void WarnStrayCameras()
    {
        foreach (var c in FindObjectsOfType<Camera>())
        {
            if (c == cam || c.targetTexture != null) continue;
            Debug.LogWarning("[CEK] Kamera lain aktif di scene: " + SceneObjectPath(c.transform)
                             + " (depth " + c.depth + "). Kamera ini bisa mengambil alih tampilan Game.");
        }
    }

    static string SceneObjectPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    // Kamera menoleh dan zoom ringan ke slot tujuan selama komponen dipegang
    void UpdateFocus()
    {
        if (SeatedLook.I == null) return;
        if (!focusOnHold || finished || powerStarted || held == null || heldData == null)
        {
            SeatedLook.I.ClearFocus();
            return;
        }
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var s in allSockets)
            if (s.accepts == heldData.type && !s.Occupied) { sum += s.transform.position; n++; }
        if (n == 0) { SeatedLook.I.ClearFocus(); return; }
        SeatedLook.I.FocusOn(sum / n + Vector3.up * 0.08f);
    }

    // Kartu komponen dikunci kalau semua slot jenisnya masih menunggu prasyarat
    void UpdateLocks()
    {
        foreach (var p in parts)
        {
            bool locked = false;
            string reason = "";
            if (lockUnavailableParts && !finished && !powerStarted && Remaining(p.type) > 0)
            {
                bool ready = false;
                SocketPoint blocker = null;
                foreach (var s in allSockets)
                {
                    if (s.accepts != p.type || s.Occupied) continue;
                    SocketPoint m = s.MissingRequirement();
                    if (m == null) { ready = true; break; }
                    if (blocker == null) blocker = m;
                }
                if (!ready && blocker != null)
                {
                    locked = true;
                    reason = "Pasang " + NameOf(blocker.accepts) + " dulu";
                }
            }
            panel.SetLock(p.type, locked, reason);
        }
    }

    // ------------------------------------------------------------ memegang komponen
    public void PickPart(PartData data)
    {
        if (finished || powerStarted || data == null) return;
        if (Remaining(data.type) <= 0) return;
        if (data.prefab == null)
        {
            Debug.LogError("PartData " + data.name + " tidak punya Prefab.");
            return;
        }

        bool same = held != null && heldData == data;
        CancelHold();
        if (same) return;

        heldData = data;
        held = Instantiate(data.prefab);
        PartSanitizer.Strip(held, false);
        userRot = Quaternion.identity;
        held.name = data.displayName + " (dipegang)";
        foreach (var c in held.GetComponentsInChildren<Collider>()) c.enabled = false;
        held.transform.position = cam.transform.position + cam.transform.forward * 0.6f;

        RefreshHolograms();
        hud.ShowHoldInfo(data);
        if (!rotateHintShown)
        {
            rotateHintShown = true;
            // petunjuk rotasi ada di panel Panduan (tombol ? / H)
        }
        if (closePanelOnPick) panel.SetOpen(false);
        Play(clipPick);
    }

    public void CancelHold()
    {
        if (held == null) return;
        Destroy(held);
        held = null;
        heldData = null;
        userRot = Quaternion.identity;
        hud.ClearHoldInfo();
        RefreshHolograms();            // kembali ke hologram panduan
    }

    // Saat memegang komponen: hologram slot yang cocok (hijau/oranye).
    // Saat tidak memegang: hologram panduan untuk semua slot yang siap dipasang.
    void RefreshHolograms()
    {
        ClearHolograms();
        if (panel == null) return;
        UpdateFocus();
        UpdateLocks();
        if (finished || powerStarted) { panel.Highlight(null); return; }

        if (held != null)
        {
            panel.Highlight(null);
            if (!showHologram || hologramMaterial == null) return;
            foreach (var s in allSockets)
            {
                if (s.accepts != heldData.type || s.Occupied) continue;
                bool ready = s.MissingRequirement() == null;
                s.ShowGhost(heldData.prefab, hologramMaterial, ready ? holoOk : holoWait);
                shown.Add(s);
            }
            return;
        }

        if (!guideFromStart) { panel.Highlight(null); return; }

        SocketPoint next = NextSlot();
        panel.Highlight(next != null ? (PartType?)next.accepts : null);
        if (!showHologram || hologramMaterial == null) return;

        foreach (var s in allSockets)
        {
            if (s.Occupied || Remaining(s.accepts) <= 0 || s.MissingRequirement() != null) continue;
            PartData d = DataOf(s.accepts);
            if (d == null || d.prefab == null) continue;
            s.ShowGhost(d.prefab, hologramMaterial, holoOk, s == next ? 1f : guideDimStrength);
            shown.Add(s);
        }
    }

    void ClearHolograms()
    {
        foreach (var s in shown) if (s != null) s.HideGhost();
        shown.Clear();
    }

    void MoveHeld()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 target;
        Quaternion wantRot = PlayerRot();
        SocketPoint s = showHologram ? FindSocket(ray) : null;

        if (s != null && s.accepts == heldData.type && !s.Occupied && s.MissingRequirement() == null)
        {
            target = s.transform.position + Vector3.up * 0.07f;
            // mode Snap: tampilkan orientasi yang sama dengan hologram
            if (orientationMode == OrientationMode.SnapToCorrect) wantRot = CanonicalRot(s);
        }
        else
        {
            Vector3 pivotOffset = Vector3.zero;
            float halfH = 0f;
            if (compensatePivot)
            {
                Renderer[] renderers = held.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                    pivotOffset = b.center - held.transform.position;
                    halfH = b.extents.y;
                }
            }

            Plane plane = new Plane(Vector3.up, new Vector3(0f, holdHeight, 0f));
            float d;
            if (plane.Raycast(ray, out d))
                target = ray.GetPoint(Mathf.Min(d, maxHoldDistance)) - pivotOffset;
            else
                target = (cam.transform.position + cam.transform.forward * 0.6f) - pivotOffset;

            // jangan tembus meja saat diputar berdiri
            if (halfH > 0f)
            {
                float bottom = target.y + pivotOffset.y - halfH;
                float minBottom = tableY + 0.02f;
                if (bottom < minBottom) target.y += minBottom - bottom;
            }
        }

        float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        held.transform.position = Vector3.Lerp(held.transform.position, target, k);
        held.transform.rotation = Quaternion.Slerp(held.transform.rotation, wantRot, k);
    }

    SocketPoint FindSocket(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, socketMask,
                                               QueryTriggerInteraction.Collide);
        if (hits.Length == 0) return null;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        SocketPoint first = null;
        foreach (var h in hits)
        {
            SocketPoint s = h.collider.GetComponent<SocketPoint>();
            if (s == null) continue;
            if (first == null) first = s;
            if (s.accepts == heldData.type && !s.Occupied) return s;
        }
        return first;
    }

    // ------------------------------------------------------------ rotasi komponen
    // orientasi dasar komponen yang dipegang, ditambah putaran pemain
    Quaternion PlayerRot()
    {
        return userRot * heldData.prefab.transform.rotation;
    }

    // orientasi akhir yang benar di slot tertentu (sama dengan hologram)
    Quaternion CanonicalRot(SocketPoint s)
    {
        return s.transform.rotation * heldData.prefab.transform.localRotation;
    }

    // rotasi lokal terhadap slot yang dipakai saat komponen terpasang
    Quaternion InstallRotation(SocketPoint s)
    {
        if (orientationMode == OrientationMode.KeepPlayerRotation)
            return Quaternion.Inverse(s.transform.rotation) * PlayerRot();
        return heldData.prefab.transform.localRotation;
    }

    void HandleRotateInput()
    {
        if (held == null || heldData == null) return;
        bool fine = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool step = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        float yaw   = RotAmount(KeyCode.E, KeyCode.PageUp, KeyCode.Q, KeyCode.PageDown, step, fine);
        float pitch = RotAmount(KeyCode.R, KeyCode.Home, KeyCode.F, KeyCode.End, step, fine);
        float roll  = RotAmount(KeyCode.Z, KeyCode.Insert, KeyCode.X, KeyCode.Delete, step, fine);

        if (yaw != 0f)   userRot = Quaternion.AngleAxis(yaw, Vector3.up) * userRot;
        if (pitch != 0f) userRot = Quaternion.AngleAxis(pitch, cam.transform.right) * userRot;
        if (roll != 0f)  userRot = Quaternion.AngleAxis(roll, cam.transform.forward) * userRot;
        if (Input.GetKeyDown(KeyCode.T)) userRot = Quaternion.identity;
    }

    float RotAmount(KeyCode plusA, KeyCode plusB, KeyCode minusA, KeyCode minusB, bool step, bool fine)
    {
        if (step)
        {
            if (Input.GetKeyDown(plusA) || Input.GetKeyDown(plusB)) return 90f;
            if (Input.GetKeyDown(minusA) || Input.GetKeyDown(minusB)) return -90f;
            return 0f;
        }
        float dir = 0f;
        if (Input.GetKey(plusA) || Input.GetKey(plusB)) dir += 1f;
        if (Input.GetKey(minusA) || Input.GetKey(minusB)) dir -= 1f;
        return dir * rotateSpeed * (fine ? 0.25f : 1f) * Time.unscaledDeltaTime;
    }

    // ------------------------------------------------------------ logika pemasangan
    void TryPlace()
    {
        if (heldData == null) return;
        SocketPoint s = FindSocket(cam.ScreenPointToRay(Input.mousePosition));
        if (s == null)
        {
            Debug.Log("Ray tidak mengenai slot. Socket Mask = " + socketMask.value);
            hud.Toast("Arahkan ke hologram di meja, lalu klik.", HUDController.Tone.Info, 1.8f);
            return;
        }

        if (s.Occupied)
        {
            Mistake(penaltyOccupied, s.friendlyName + " sudah terisi.", s);
            return;
        }
        if (s.accepts != heldData.type)
        {
            Mistake(penaltyWrongType,
                "Salah pasang! " + heldData.displayName + " tidak cocok di " + s.friendlyName + ".", s);
            return;
        }
        SocketPoint missing = s.MissingRequirement();
        if (missing != null)
        {
            Mistake(penaltyWrongOrder,
                "Urutan salah! Pasang " + NameOf(missing.accepts) + " dulu.", s);
            return;
        }
        if (orientationMode == OrientationMode.RequireMatch)
        {
            float diff = Quaternion.Angle(PlayerRot(), CanonicalRot(s));
            if (diff > matchTolerance)
            {
                Mistake(penaltyWrongOrientation,
                    "Orientasi belum tepat (selisih " + Mathf.RoundToInt(diff) + " derajat). Putar dulu komponennya.", s);
                return;
            }
        }
        Success(s);
    }

    void Success(SocketPoint s)
    {
        PartType type = heldData.type;
        GameObject part = held;
        Quaternion rot = InstallRotation(s);
        ClearHolograms();
        held = null;
        heldData = null;
        userRot = Quaternion.identity;

        s.Install(part, rot);

        if (type == PartType.Case)
        {
            foreach (var c in part.GetComponentsInChildren<Collider>()) c.enabled = true;
            caseLight = part.GetComponentInChildren<Light>(true);
            if (caseSeeThrough) MakeSeeThrough(part);
        }
        if (type == PartType.Monitor)
        {
            TMPro.TMP_Text txt = part.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (txt != null) monitorText = txt;
            foreach (var r in part.GetComponentsInChildren<Renderer>(true))
                if (r.name.ToLower().Contains("screen")) { monitorScreen = r; break; }
        }
        if (snapFx != null) Destroy(Instantiate(snapFx, s.transform.position, Quaternion.identity), 2f);

        remaining[type] = Remaining(type) - 1;
        placed++;
        score += scoreCorrect;
        hud.SetScore(score);
        hud.SetProgress(placed, totalSteps);
        hud.ClearHoldInfo();
        hud.Toast("Benar! " + NameOf(type) + " terpasang." + (guideFromStart ? NextText() : ""),
                  HUDController.Tone.Success, 2.4f);
        panel.Refresh(remaining);
        Play(clipPlace);

        RefreshHolograms();            // hologram langkah-langkah berikutnya

        if (placed == totalSteps)
        {
            hud.Toast("Semua komponen terpasang! Tekan tombol POWER di casing.",
                      HUDController.Tone.Info, 6f);
            foreach (var c in FindObjectsOfType<Clickable3D>())
                if (c.actionId == "power") c.pulse = true;
        }
    }

    // Casing jadi transparan supaya hologram dan komponen di dalamnya terlihat (URP Lit)
    void MakeSeeThrough(GameObject go)
    {
        int changed = 0;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
            Material[] mats = r.materials;
            bool any = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || !m.HasProperty("_Surface")) continue;
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_ZWrite", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_SrcBlendAlpha"))
                {
                    m.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
                    m.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHATEST_ON");
                m.renderQueue = 3000;
                if (m.HasProperty("_BaseColor"))
                {
                    Color c = m.GetColor("_BaseColor");
                    c.a = caseAlpha;
                    m.SetColor("_BaseColor", c);
                }
                any = true;
                changed++;
            }
            if (any) r.materials = mats;
        }
        if (changed == 0)
            Debug.LogWarning("[CEK] Material casing tidak mendukung transparansi (bukan URP Lit), casing tetap solid. "
                             + "Hologram di dalam casing tidak akan terlihat; matikan Case See Through atau ganti material casing.");
    }

    void Mistake(int penalty, string message, SocketPoint s)
    {
        mistakes++;
        score = Mathf.Max(0, score - penalty);
        hud.SetScore(score);
        hud.SetMistakes(mistakes, maxMistakes);
        hud.Toast(message + "  (-" + penalty + ")", HUDController.Tone.Error, 3.2f);
        hud.FlashRed();
        Play(clipError);
        if (SeatedLook.I != null) SeatedLook.I.Shake();
        if (s != null && heldData != null && hologramMaterial != null)
            StartCoroutine(RedGhost(s, heldData));

        if (maxMistakes > 0 && mistakes >= maxMistakes) Fail("Terlalu banyak kesalahan!");
    }

    IEnumerator RedGhost(SocketPoint s, PartData d)
    {
        s.ShowGhost(d.prefab, hologramMaterial, holoWrong);
        yield return new WaitForSeconds(0.6f);
        if (s == null) yield break;
        s.HideGhost();                 // hapus hologram merah (termasuk di slot yang sudah terisi)
        if (finished) yield break;
        RefreshHolograms();
    }

    // ------------------------------------------------------------ tombol
    public void UiAction(string id) { HandleClick(id); }   // dipanggil tombol layar

    void HandleClick(string id)
    {
        if (finished) return;
        switch (id)
        {
            case "parts": panel.Toggle(); break;
            case "hint":  UseHint(); break;
            case "reset": SceneManager.LoadScene(SceneManager.GetActiveScene().name); break;
            case "power": PressPower(); break;
        }
    }

    public void UseHint()
    {
        if (finished || powerStarted) return;
        if (held != null)
        {
            hud.Toast("Letakkan atau batalkan komponen dulu sebelum minta petunjuk.",
                      HUDController.Tone.Info, 2.5f);
            return;
        }
        SocketPoint s = NextSlot();
        if (s == null) return;
        PartData d = DataOf(s.accepts);
        if (d == null || d.prefab == null) return;

        if (guideFromStart && showHologram)
        {
            // hologram sudah tampil: petunjuk hanya menyebut dan menyorot langkah berikutnya, tanpa penalti
            hud.Toast("Petunjuk: pasang " + d.displayName + " di " + s.friendlyName + ".",
                      HUDController.Tone.Info, 3f);
            panel.Highlight(s.accepts);
            return;
        }
        if (hologramMaterial == null) return;
        score = Mathf.Max(0, score - hintCost);
        hud.SetScore(score);
        hud.Toast("Petunjuk: pasang " + d.displayName + ".  (-" + hintCost + ")",
                  HUDController.Tone.Info, 3f);
        StartCoroutine(HintRoutine(s, d));
    }

    IEnumerator HintRoutine(SocketPoint s, PartData d)
    {
        s.ShowGhost(d.prefab, hologramMaterial, holoOk);
        yield return new WaitForSeconds(3f);
        if (s != null && held == null) { s.HideGhost(); RefreshHolograms(); }
    }

    // ------------------------------------------------------------ tombol power
    void PressPower()
    {
        if (powerStarted) return;
        if (placed < totalSteps)
        {
            Mistake(penaltyWrongOrder, "Rakitan belum lengkap, PC belum bisa dinyalakan!", null);
            return;
        }
        powerStarted = true;
        ClearHolograms();
        panel.Highlight(null);
        StartCoroutine(BootSequence());
    }

    IEnumerator BootSequence()
    {
        Play(clipPower);
        panel.SetOpen(false);
        foreach (var sp in FindObjectsOfType<Spinner>(true)) sp.Run();
        if (caseLight != null) caseLight.enabled = true;
        if (monitorScreen != null && screenOnMaterial != null)
            monitorScreen.sharedMaterial = screenOnMaterial;
        if (monitorText != null) monitorText.text = BuildBootText();

        float t = 0f;
        while (t < 3.5f)
        {
            t += Time.deltaTime;
            if (caseLight != null)
                caseLight.color = Color.HSVToRGB(Mathf.Repeat(t * 0.35f, 1f), 0.7f, 1f);
            yield return null;
        }
        Complete();
    }

    string BuildBootText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>SISTEM BERHASIL MENYALA</b>");
        foreach (var p in parts)
        {
            string first = string.IsNullOrEmpty(p.specs) ? "" : p.specs.Split('\n')[0];
            sb.AppendLine(p.displayName + " : " + first);
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------ akhir level
    void Complete()
    {
        finished = true;
        int bonus = timeLimit > 0f ? Mathf.RoundToInt(Mathf.Max(0f, timeLimit - elapsed) * 2f) : 0;
        score += bonus;
        int stars = mistakes == 0 ? 3 : (mistakes <= 2 ? 2 : 1);

        if (score > PlayerPrefs.GetInt("best_" + levelId, 0))
            PlayerPrefs.SetInt("best_" + levelId, score);
        if (levelId == "Entry") PlayerPrefs.SetInt("unlock_advance", 1);
        PlayerPrefs.Save();

        hud.SetScore(score);
        hud.ShowResult(true, score, mistakes, elapsed, stars, nextScene != "", "");
        Play(clipWin);
    }

    void Fail(string reason)
    {
        if (finished) return;
        finished = true;
        CancelHold();
        ClearHolograms();
        hud.ShowResult(false, score, mistakes, elapsed, 0, false, reason);
        Play(clipFail);
    }

    public void LoadNext()
    {
        if (nextScene != "") SceneManager.LoadScene(nextScene);
    }
}