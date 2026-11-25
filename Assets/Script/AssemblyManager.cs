using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AssemblyManager : MonoBehaviour
{
    public static AssemblyManager I;

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
    public float holdHeight = 0.95f;
    public float followSpeed = 18f;
    public bool compensatePivot = true;      // sementara, sampai pivot prefab dirapikan
    public float maxHoldDistance = 3f;       // jarak maksimum titik pegang dari kamera

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

    GameObject held;
    PartData heldData;
    Light caseLight;
    readonly Dictionary<PartType, int> remaining = new Dictionary<PartType, int>();
    readonly List<SocketPoint> shown = new List<SocketPoint>();
    int totalSteps, placed, score, mistakes;
    float elapsed;
    bool finished, powerStarted;

    // ------------------------------------------------------------ siklus hidup
    void Awake() { I = this; Time.timeScale = 1f; }
    void OnEnable()  { Clickable3D.OnClicked += HandleClick; }
    void OnDisable() { Clickable3D.OnClicked -= HandleClick; }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        foreach (var p in parts) { remaining[p.type] = p.copies; totalSteps += p.copies; }
        panel.Build(parts, this);
        hud.Init(levelTitle, totalSteps, maxMistakes);
        hud.SetTime(timeLimit > 0f ? timeLimit : 0f);
        if (hintButton3D != null) hintButton3D.SetActive(allowHint);
        hud.Toast("Tekan tombol PARTS di meja untuk membuka daftar komponen.",
                  HUDController.Tone.Info, 5f);

        // bidang gerak komponen mengikuti tinggi meja yang sebenarnya
        if (allSockets != null && allSockets.Length > 0 && allSockets[0] != null)
            holdHeight = allSockets[0].transform.position.y + 0.2f;
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

        if (held == null) return;
        MoveHeld();

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

    void Play(AudioClip c) { if (sfx != null && c != null) sfx.PlayOneShot(c); }

    // ------------------------------------------------------------ memegang komponen
    public void PickPart(PartData data)
    {
        if (finished || powerStarted) return;
        if (remaining[data.type] <= 0) return;

        bool same = held != null && heldData == data;
        CancelHold();
        if (same) return;                        // klik kartu yang sama = batal

        heldData = data;
        held = Instantiate(data.prefab);
        held.name = data.displayName + " (dipegang)";
        foreach (var c in held.GetComponentsInChildren<Collider>()) c.enabled = false;
        held.transform.position = cam.transform.position + cam.transform.forward * 0.6f;

        RefreshHolograms();
        hud.ShowHoldInfo(data);
        Play(clipPick);
    }

    public void CancelHold()
    {
        if (held == null) return;
        Destroy(held);
        held = null;
        heldData = null;
        ClearHolograms();
        hud.ClearHoldInfo();
    }

    void RefreshHolograms()
    {
        ClearHolograms();
        if (!showHologram || held == null) return;
        foreach (var s in allSockets)
        {
            if (s.accepts != heldData.type || s.Occupied) continue;
            bool ready = s.MissingRequirement() == null;
            s.ShowGhost(heldData.prefab, hologramMaterial, ready ? holoOk : holoWait);
            shown.Add(s);
        }
    }

    void ClearHolograms()
    {
        foreach (var s in shown) s.HideGhost();
        shown.Clear();
    }

    void MoveHeld()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 target;
        SocketPoint s = showHologram ? FindSocket(ray) : null;

        if (s != null && s.accepts == heldData.type && !s.Occupied && s.MissingRequirement() == null)
        {
            target = s.transform.position + Vector3.up * 0.07f;      // magnet ke hologram
        }
        else
        {
            // selisih titik tengah visual terhadap pivot prefab
            Vector3 pivotOffset = Vector3.zero;
            if (compensatePivot)
            {
                Renderer[] renderers = held.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                    pivotOffset = b.center - held.transform.position;
                }
            }

            Plane plane = new Plane(Vector3.up, new Vector3(0f, holdHeight, 0f));
            float d;
            if (plane.Raycast(ray, out d))
                target = ray.GetPoint(Mathf.Min(d, maxHoldDistance)) - pivotOffset;
            else
                target = (cam.transform.position + cam.transform.forward * 0.6f) - pivotOffset;
        }

        float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        held.transform.position = Vector3.Lerp(held.transform.position, target, k);
        held.transform.rotation = Quaternion.Slerp(held.transform.rotation,
                                                   heldData.prefab.transform.rotation, k);
    }

    // Slot yang ditunjuk pointer. Utamakan slot yang cocok dan masih kosong.
    SocketPoint FindSocket(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, 6f, socketMask, QueryTriggerInteraction.Collide);
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

    // ------------------------------------------------------------ logika pemasangan
    void TryPlace()
    {
        SocketPoint s = FindSocket(cam.ScreenPointToRay(Input.mousePosition));
        if (s == null)
        {
            Debug.Log("Ray tidak mengenai slot. Socket Mask = " + socketMask.value);
            hud.Toast("Arahkan ke slot di meja, lalu klik.", HUDController.Tone.Info, 1.8f);
            return;
        }

        // Aturan 1: slot sudah terisi
        if (s.Occupied)
        {
            Mistake(penaltyOccupied, s.friendlyName + " sudah terisi.", s);
            return;
        }
        // Aturan 2: jenis komponen salah
        if (s.accepts != heldData.type)
        {
            Mistake(penaltyWrongType,
                "Salah pasang! " + heldData.displayName + " tidak cocok di " + s.friendlyName + ".", s);
            return;
        }
        // Aturan 3: urutan salah (prasyarat belum terpasang)
        SocketPoint missing = s.MissingRequirement();
        if (missing != null)
        {
            Mistake(penaltyWrongOrder,
                "Urutan salah! Pasang " + NameOf(missing.accepts) + " dulu.", s);
            return;
        }
        Success(s);
    }

    void Success(SocketPoint s)
    {
        PartType type = heldData.type;
        GameObject part = held;
        Quaternion rot = heldData.prefab.transform.localRotation;
        ClearHolograms();
        held = null;
        heldData = null;

        s.Install(part, rot);
        if (type == PartType.Case)
        {
            foreach (var c in part.GetComponentsInChildren<Collider>()) c.enabled = true;
            caseLight = part.GetComponentInChildren<Light>(true);
        }
        if (snapFx != null) Destroy(Instantiate(snapFx, s.transform.position, Quaternion.identity), 2f);

        remaining[type]--;
        placed++;
        score += scoreCorrect;
        hud.SetScore(score);
        hud.SetProgress(placed, totalSteps);
        hud.ClearHoldInfo();
        hud.Toast("Benar! " + NameOf(type) + " terpasang.", HUDController.Tone.Success, 1.8f);
        panel.Refresh(remaining);
        Play(clipPlace);

        if (placed == totalSteps)
        {
            hud.Toast("Semua komponen terpasang! Tekan tombol POWER di casing.",
                      HUDController.Tone.Info, 6f);
            foreach (var c in FindObjectsOfType<Clickable3D>())
                if (c.actionId == "power") c.pulse = true;
        }
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
        if (s != null && heldData != null) StartCoroutine(RedGhost(s, heldData));

        if (maxMistakes > 0 && mistakes >= maxMistakes) Fail("Terlalu banyak kesalahan!");
    }

    IEnumerator RedGhost(SocketPoint s, PartData d)
    {
        s.ShowGhost(d.prefab, hologramMaterial, holoWrong);
        yield return new WaitForSeconds(0.6f);
        if (finished) { s.HideGhost(); yield break; }
        if (held != null) RefreshHolograms(); else s.HideGhost();
    }

    // ------------------------------------------------------------ tombol 3D
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
        foreach (var s in allSockets)
        {
            if (s.Occupied || s.MissingRequirement() != null || remaining[s.accepts] <= 0) continue;
            PartData d = DataOf(s.accepts);
            score = Mathf.Max(0, score - hintCost);
            hud.SetScore(score);
            hud.Toast("Petunjuk: pasang " + d.displayName + ".  (-" + hintCost + ")",
                      HUDController.Tone.Info, 3f);
            StartCoroutine(HintRoutine(s, d));
            return;
        }
    }

    IEnumerator HintRoutine(SocketPoint s, PartData d)
    {
        s.ShowGhost(d.prefab, hologramMaterial, holoOk);
        yield return new WaitForSeconds(3f);
        if (held == null) s.HideGhost();
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
        foreach (var p in parts) sb.AppendLine(p.displayName + " : " + p.specs.Split('\n')[0]);
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
        hud.ShowResult(false, score, mistakes, elapsed, 0, false, reason);
        Play(clipFail);
    }

    public void LoadNext()
    {
        if (nextScene != "") SceneManager.LoadScene(nextScene);
    }
}