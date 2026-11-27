#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SocketTools
{
    const string PreviewName = "_PRATINJAU";

    // ---- parameter yang boleh diubah ----
    const float Wall = 0.01f;            // tebal dinding casing (m)
    const float RearGap = 0.03f;         // jarak mobo ke dinding belakang, ruang kipas (m)
    const float PanelVpHeight = 0.38f;   // tinggi panel di layar (0-1). Lebih besar = teks lebih terbaca
    const float SeatHeight = 0.65f;      // tinggi kamera di atas meja (m)
    const float TableMargin = 0.06f;     // jarak aman benda ke tepi meja (m)

    static SocketTools()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) RemovePreviews(false);
    }

    static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
    {
        RemovePreviews(false);
    }

    // ------------------------------------------------------------ helper
    static float Safe(float world, float parent)
    {
        return Mathf.Abs(parent) > 1e-5f ? world / parent : world;
    }

    static PartData FindPartData(PartType t)
    {
        var mgr = Object.FindObjectOfType<AssemblyManager>();
        if (mgr != null && mgr.parts != null)
            foreach (var p in mgr.parts) if (p != null && p.type == t) return p;
        return null;
    }

    public static bool TryBounds(PartType t, out Bounds b)
    {
        b = new Bounds();
        PartData d = FindPartData(t);
        if (d == null || d.prefab == null)
        {
            Debug.LogWarning("PartData atau Prefab untuk " + t + " tidak ada di daftar Parts AssemblyManager.");
            return false;
        }
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(d.prefab);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        b = SceneCheck.BoundsOf(go);
        Object.DestroyImmediate(go);
        return true;
    }

    // kotak pembatas badan objek (tanpa teks TextMeshPro)
    public static bool BodyBounds(GameObject go, out Bounds b)
    {
        b = new Bounds();
        bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return !first;
    }

    static void RotMinMax(Bounds b, Quaternion q, out Vector3 mn, out Vector3 mx)
    {
        mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            Vector3 sg = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
            Vector3 p = q * (b.center + Vector3.Scale(b.extents, sg));
            mn = Vector3.Min(mn, p);
            mx = Vector3.Max(mx, p);
        }
    }

    public static void AddCorners(List<Vector3> l, Bounds b, Transform space)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 sg = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
            Vector3 p = b.center + Vector3.Scale(b.extents, sg);
            l.Add(space != null ? space.TransformPoint(p) : p);
        }
    }

    static void AddPart(List<Vector3> l, SocketPoint s, PartType t)
    {
        Bounds b;
        if (!TryBounds(t, out b)) { l.Add(s.transform.position); return; }
        AddCorners(l, b, s.transform);
    }

    static void AddBody(List<Vector3> l, GameObject go)
    {
        if (go == null) return;
        Bounds b;
        if (BodyBounds(go, out b)) AddCorners(l, b, null);
        else l.Add(go.transform.position);
    }

    static SocketPoint FindSlot(PartType t)
    {
        foreach (var s in Object.FindObjectsOfType<SocketPoint>()) if (s.accepts == t) return s;
        return null;
    }

    static void SetY(Transform t, float y)
    {
        Undo.RecordObject(t, "Turunkan ke meja");
        Vector3 p = t.position;
        p.y = y;
        t.position = p;
    }

    static void XZ(List<Vector3> l, out float x0, out float x1, out float z0, out float z1)
    {
        x0 = float.MaxValue; x1 = float.MinValue; z0 = float.MaxValue; z1 = float.MinValue;
        foreach (var p in l)
        {
            x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
            z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
        }
    }

    static bool HitsObstacle(List<Vector3> part, float dx, float dz, List<Bounds> obst, float pad)
    {
        float x0, x1, z0, z1;
        XZ(part, out x0, out x1, out z0, out z1);
        x0 += dx; x1 += dx; z0 += dz; z1 += dz;
        foreach (var o in obst)
            if (x1 > o.min.x - pad && x0 < o.max.x + pad && z1 > o.min.z - pad && z0 < o.max.z + pad) return true;
        return false;
    }

    static int BottomOutside(List<Vector3> pts, float dx, float dz)
    {
        float minY = float.MaxValue;
        foreach (var p in pts) minY = Mathf.Min(minY, p.y);
        int n = 0;
        foreach (var p in pts)
        {
            if (p.y > minY + 0.01f) continue;
            if (!TableSurface.ContainsMargin(p.x + dx, p.z + dz, 0.03f)) n++;
        }
        return n;
    }

    // ------------------------------------------------------------ 1. pindah ke atas meja
        [MenuItem("Tools/RakitPC/1. Pindahkan Stasiun ke Atas Meja dan Turunkan ke Permukaan", false, 1001)]
    static void MoveToTable()
    {
        if (!TableSurface.Build()) { Debug.LogWarning(TableSurface.Info); return; }
        float sy = TableSurface.Y;
        GameObject root = GameObject.Find("AssemblyRoot");
        SocketPoint cs = FindSlot(PartType.Case), kb = FindSlot(PartType.Keyboard), ms = FindSlot(PartType.Mouse);
        if (root == null || cs == null || kb == null || ms == null)
        {
            Debug.LogWarning("AssemblyRoot atau slot Case, Keyboard, Mouse tidak ditemukan.");
            return;
        }
        Bounds bc, bk, bm;
        if (!TryBounds(PartType.Case, out bc) || !TryBounds(PartType.Keyboard, out bk)
            || !TryBounds(PartType.Mouse, out bm)) return;

        var obst = new List<Bounds>();
        foreach (string n in new[] { "Btn_Parts", "Btn_Hint", "Btn_Reset", "Monitor" })
        {
            GameObject g = GameObject.Find(n);
            Bounds b;
            if (g != null && BodyBounds(g, out b)) obst.Add(b);
        }

        var pc = new List<Vector3>(); AddCorners(pc, bc, cs.transform);
        var pk = new List<Vector3>(); AddCorners(pk, bk, kb.transform);
        var pm = new List<Vector3>(); AddCorners(pm, bm, ms.transform);
        var all = new List<Vector3>(); all.AddRange(pc); all.AddRange(pk); all.AddRange(pm);
        float gx0, gx1, gz0, gz1;
        XZ(all, out gx0, out gx1, out gz0, out gz1);

        float dx = 0f, dz = 0f;
        if (gx1 > TableSurface.MaxX - TableMargin) dx = (TableSurface.MaxX - TableMargin) - gx1;
        if (gx0 + dx < TableSurface.MinX + TableMargin) dx = (TableSurface.MinX + TableMargin) - gx0;
        if (gz1 > TableSurface.MaxZ - TableMargin) dz = (TableSurface.MaxZ - TableMargin) - gz1;
        if (gz0 + dz < TableSurface.MinZ + TableMargin) dz = (TableSurface.MinZ + TableMargin) - gz0;
        float dx0 = dx, dz0 = dz;

        for (int k = 0; k < 80; k++)
        {
            if (!HitsObstacle(pc, dx, dz, obst, 0.05f) && !HitsObstacle(pk, dx, dz, obst, 0.05f)
                && !HitsObstacle(pm, dx, dz, obst, 0.05f)) break;
            dx -= 0.05f;
        }
        if (dx != dx0 && BottomOutside(pc, dx, dz) + BottomOutside(pk, dx, dz) + BottomOutside(pm, dx, dz) > 0)
        {
            dx = dx0; dz = dz0;
            Debug.LogWarning("Menghindari tombol atau monitor membuat stasiun keluar meja, jadi penghindaran dibatalkan. "
                             + "Periksa posisi tombol dan monitor secara manual.");
        }

        Undo.RecordObject(root.transform, "Pindah ke meja");
        root.transform.position += new Vector3(dx, 0f, dz);
        foreach (var s in new[] { cs, kb, ms })
        {
            if (s.transform.IsChildOf(root.transform)) continue;
            Undo.RecordObject(s.transform, "Pindah slot");
            s.transform.position += new Vector3(dx, 0f, dz);
        }

        Vector3 rp = root.transform.position;
        rp.y = sy;
        root.transform.position = rp;
        SetY(cs.transform, sy - bc.min.y);
        SetY(kb.transform, sy - bk.min.y);
        SetY(ms.transform, sy - bm.min.y);

        foreach (string n in new[] { "Btn_Parts", "Btn_Hint", "Btn_Reset", "Monitor" })
        {
            GameObject g = GameObject.Find(n);
            Bounds b;
            if (g == null || !BodyBounds(g, out b)) continue;
            float gap = b.min.y - sy;
            if (Mathf.Abs(gap) < 0.003f) continue;
            if (Mathf.Abs(gap) > 0.5f)
            {
                Debug.LogWarning(n + ": selisih " + gap.ToString("F2") + " m dari meja, tidak diubah. Periksa manual.");
                continue;
            }
            Undo.RecordObject(g.transform, "Turunkan ke meja");
            g.transform.position -= new Vector3(0f, gap, 0f);
        }

        int bad = BottomOutside(pc, dx, dz) + BottomOutside(pk, dx, dz) + BottomOutside(pm, dx, dz);
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Permukaan meja y = " + sy.ToString("F3") + ". Stasiun digeser (" + dx.ToString("F2") + ", "
                  + dz.ToString("F2") + ") m. Casing, keyboard, mouse, tombol, dan monitor diturunkan ke permukaan."
                  + (bad > 0 ? " [PERHATIAN] " + bad + " sudut dasar masih di luar meja. Geser AssemblyRoot manual lalu jalankan menu 0."
                             : " Semua sudut dasar berada di atas meja."));
    }
    // ------------------------------------------------------------ 2. Requires
    [MenuItem("Tools/RakitPC/2. Isi Requires Otomatis", false, 1002)]
    static void FillRequires()
    {
        var map = new Dictionary<string, SocketPoint>();
        foreach (var s in Object.FindObjectsOfType<SocketPoint>()) map[s.name] = s;

        SetRequires(map, "Mobo_Socket", "Case_Socket");
        SetRequires(map, "PSU_Socket", "Case_Socket");
        SetRequires(map, "Fan_Socket", "Case_Socket");
        SetRequires(map, "SSD_Socket", "Mobo_Socket");      // SSD M.2 menempel di motherboard
        SetRequires(map, "CPU_Socket", "Mobo_Socket");
        SetRequires(map, "Cooler_Socket", "CPU_Socket");
        SetRequires(map, "RAM1_Socket", "Mobo_Socket");
        SetRequires(map, "RAM2_Socket", "Mobo_Socket");
        SetRequires(map, "GPU_Socket", "Mobo_Socket");
        SetRequires(map, "Power_Socket", "Mobo_Socket", "PSU_Socket");
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Requires terisi. Case_Socket, Mouse_Socket, Keyboard_Socket sengaja tanpa prasyarat.");
    }

    static void SetRequires(Dictionary<string, SocketPoint> map, string slot, params string[] req)
    {
        SocketPoint s;
        if (!map.TryGetValue(slot, out s)) { Debug.LogWarning("Slot tidak ditemukan: " + slot); return; }
        var list = new List<SocketPoint>();
        foreach (string r in req)
        {
            SocketPoint q;
            if (map.TryGetValue(r, out q)) list.Add(q);
            else Debug.LogWarning("Prasyarat tidak ditemukan: " + r + " (untuk " + slot + ")");
        }
        Undo.RecordObject(s, "Isi Requires");
        s.requires = list.ToArray();
        EditorUtility.SetDirty(s);
    }

    // ------------------------------------------------------------ 3. pratinjau
    [MenuItem("Tools/RakitPC/3. Pratinjau: Pasang Komponen di Slot", false, 1003)]
    static void ShowPreviews()
    {
        RemovePreviews(false);
        int n = 0;
        foreach (var s in Object.FindObjectsOfType<SocketPoint>())
        {
            PartData d = FindPartData(s.accepts);
            if (d == null || d.prefab == null)
            {
                Debug.LogWarning("Tidak ada PartData (di Parts) untuk slot " + s.name);
                continue;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(d.prefab, s.transform);
            go.name = PreviewName;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = d.prefab.transform.localRotation;
            Vector3 ps = s.transform.lossyScale;
            Vector3 w = d.prefab.transform.localScale;
            go.transform.localScale = new Vector3(Safe(w.x, ps.x), Safe(w.y, ps.y), Safe(w.z, ps.z));
            SceneVisibilityManager.instance.DisablePicking(go, true);
            n++;
        }
        Debug.Log("Pratinjau dipasang pada " + n + " slot. Pratinjau hilang otomatis saat Save atau Play.");
    }

    [MenuItem("Tools/RakitPC/7. Pratinjau: Hapus", false, 1007)]
    static void RemoveMenu() { RemovePreviews(true); }

    static void RemovePreviews(bool log)
    {
        int n = 0;
        foreach (var t in Object.FindObjectsOfType<Transform>())
        {
            if (t != null && t.name == PreviewName) { Object.DestroyImmediate(t.gameObject); n++; }
        }
        if (log) Debug.Log("Pratinjau dihapus: " + n);
    }

    // ------------------------------------------------------------ 4. cek posisi
    static float Overshoot(Bounds inner, Bounds outer)
    {
        float o = 0f;
        for (int i = 0; i < 3; i++)
        {
            o = Mathf.Max(o, outer.min[i] - inner.min[i]);
            o = Mathf.Max(o, inner.max[i] - outer.max[i]);
        }
        return o;
    }

    [MenuItem("Tools/RakitPC/4. Pratinjau: Cek Posisi", false, 1004)]
    static void CheckPreviews()
    {
        var previews = new List<Transform>();
        foreach (var t in Object.FindObjectsOfType<Transform>())
            if (t.name == PreviewName) previews.Add(t);
        if (previews.Count == 0) { Debug.LogWarning("Belum ada pratinjau. Jalankan menu 3 dulu."); return; }

        float deskY; string di;
        bool hasDesk = SceneCheck.FindDeskTop(out deskY, out di);
        Bounds caseB = new Bounds();
        bool hasCase = false;
        foreach (var p in previews)
        {
            var s = p.parent.GetComponent<SocketPoint>();
            if (s != null && s.accepts == PartType.Case) { caseB = SceneCheck.BoundsOf(p.gameObject); hasCase = true; }
        }

        var sb = new StringBuilder("=== CEK PRATINJAU ===\n");
        if (hasDesk) sb.AppendLine("Permukaan meja y = " + deskY.ToString("F3"));
        if (hasCase) sb.AppendLine("Batas casing: " + caseB.min.ToString("F3") + " sampai " + caseB.max.ToString("F3"));
        foreach (var p in previews)
        {
            var s = p.parent.GetComponent<SocketPoint>();
            if (s == null) continue;
            Bounds b = SceneCheck.BoundsOf(p.gameObject);
            string head = s.name + " (" + s.accepts + "): ";
            bool onDesk = s.accepts == PartType.Case || s.accepts == PartType.Mouse
                          || s.accepts == PartType.Keyboard;
            if (onDesk)
            {
                if (!hasDesk) { sb.AppendLine(head + "meja tidak terdeteksi"); continue; }
                float gap = b.min.y - deskY;
                bool outside = !TableSurface.Contains(b.min.x, b.min.z) || !TableSurface.Contains(b.max.x, b.max.z)
                               || !TableSurface.Contains(b.min.x, b.max.z) || !TableSurface.Contains(b.max.x, b.min.z);
                sb.AppendLine(head + "dasar " + gap.ToString("F3") + " m dari meja"
                    + (Mathf.Abs(gap) > 0.01f ? "  [MASALAH] harus 0 (melayang atau tertanam)" : "  OK")
                    + (outside ? "  [MASALAH] sebagian di luar permukaan meja" : ""));
            }
            else if (hasCase)
            {
                float o = Overshoot(b, caseB);
                sb.AppendLine(head + (o > 0.005f
                    ? "[MASALAH] keluar dari casing sejauh " + o.ToString("F3") + " m"
                    : "di dalam casing OK"));
            }
        }
        System.IO.File.WriteAllText("Assets/HasilPratinjau.txt", sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log(sb.ToString());
    }

    // ------------------------------------------------------------ 5. collider
    static Bounds LocalBounds(GameObject go, Transform space)
    {
        bool first = true;
        Bounds lb = new Bounds();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            Bounds b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 sign = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                Vector3 l = space.InverseTransformPoint(b.center + Vector3.Scale(b.extents, sign));
                if (first) { lb = new Bounds(l, Vector3.zero); first = false; }
                else lb.Encapsulate(l);
            }
        }
        return lb;
    }

    [MenuItem("Tools/RakitPC/5. Pratinjau: Pasang Collider Slot", false, 1005)]
    static void FitColliders()
    {
        int n = 0;
        foreach (var t in Object.FindObjectsOfType<Transform>())
        {
            if (t.name != PreviewName) continue;
            var s = t.parent.GetComponent<SocketPoint>();
            var box = t.parent.GetComponent<BoxCollider>();
            if (s == null || box == null) continue;
            Bounds lb = LocalBounds(t.gameObject, s.transform);
            Undo.RecordObject(box, "Collider slot");
            box.isTrigger = true;
            box.center = lb.center;
            box.size = new Vector3(Mathf.Max(lb.size.x + 0.02f, 0.05f),
                                   Mathf.Max(lb.size.y + 0.02f, 0.05f),
                                   Mathf.Max(lb.size.z + 0.02f, 0.05f));
            n++;
        }
        if (n == 0) Debug.LogWarning("Belum ada pratinjau. Jalankan menu 3 dulu.");
        else
        {
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("Collider disesuaikan pada " + n + " slot.");
        }
    }

    // ------------------------------------------------------------ 6. kamera dan panel
    static bool AllVisible(Camera cam, List<Vector3> pts)
    {
        foreach (var p in pts)
        {
            Vector3 v = cam.WorldToViewportPoint(p);
            if (v.z <= 0f || v.x < 0.05f || v.x > 0.95f || v.y < 0.05f || v.y > 0.95f) return false;
        }
        return true;
    }

    [MenuItem("Tools/RakitPC/6. Atur Kamera dan Panel Otomatis", false, 1006)]
    static void PlaceCameraAndPanel()
    {
        Camera cam = Camera.main;
        var panel = Object.FindObjectOfType<PartsPanel>();
        if (cam == null || panel == null) { Debug.LogWarning("MainCamera atau PartsPanel tidak ditemukan."); return; }
        float deskY; string di;
        if (!SceneCheck.FindDeskTop(out deskY, out di)) { Debug.LogWarning(di); return; }

        var all = new List<Vector3>();   // semua titik yang harus terlihat
        var btn = new List<Vector3>();   // titik tombol, dipakai untuk posisi panel
        foreach (var s in Object.FindObjectsOfType<SocketPoint>())
        {
            if (s.accepts == PartType.Case || s.accepts == PartType.Keyboard || s.accepts == PartType.Mouse)
                AddPart(all, s, s.accepts);
        }
        foreach (string n in new[] { "Btn_Parts", "Btn_Hint", "Btn_Reset" }) AddBody(btn, GameObject.Find(n));
        AddBody(all, GameObject.Find("Monitor"));
        all.AddRange(btn);
        if (all.Count == 0) { Debug.LogWarning("Tidak ada titik acuan untuk kamera."); return; }

        Vector3 f = cam.transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) f = Vector3.back;
        f.Normalize();
        Vector3 r = Vector3.Cross(Vector3.up, f);
        float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;

        float umin = float.MaxValue, umax = float.MinValue, wmin = float.MaxValue;
        Vector3 sum = Vector3.zero;
        foreach (var p in all)
        {
            float u = Vector3.Dot(p, r), w = Vector3.Dot(p, f);
            umin = Mathf.Min(umin, u); umax = Mathf.Max(umax, u); wmin = Mathf.Min(wmin, w);
            sum += p;
        }
        float ucam = (umin + umax) * 0.5f;
        Vector3 center = sum / all.Count;
        Vector3 target = new Vector3(center.x, deskY + 0.12f, center.z);

        Undo.RecordObject(cam.transform, "Atur kamera");
        bool fit = false;
        float back = 0.6f;
        for (; back <= 3.01f; back += 0.1f)
        {
            Vector3 pos = r * ucam + f * (wmin - back);
            pos.y = deskY + SeatHeight;
            Vector3 d = target - pos;
            float pitch = Mathf.Clamp(Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, 0f, 50f);
            cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, 0f));
            fit = AllVisible(cam, all);
            if (fit) break;
        }
        if (!fit) Debug.LogWarning("Kamera sudah dijauhkan 3 m tetapi masih ada objek di luar layar. Periksa objek Monitor atau posisi tombol.");

        var look = Object.FindObjectOfType<SeatedLook>();
        if (look != null && !look.useTransformAsStart)
        {
            Undo.RecordObject(look, "SeatedLook");
            look.useTransformAsStart = true;
            EditorUtility.SetDirty(look);
        }
        Canvas pcv = panel.GetComponent<Canvas>();
        if (pcv != null && pcv.renderMode != RenderMode.WorldSpace)
        {
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("Kamera: " + cam.transform.position.ToString("F2") + ", pitch "
                      + cam.transform.eulerAngles.x.ToString("F0") + ". Panel sudah overlay layar, tidak diubah.");
            return;
        }
        
        RectTransform rt = panel.transform as RectTransform;
        if (rt == null) { Debug.LogWarning("PartsPanel tidak punya RectTransform."); return; }
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        float h = (c[1] - c[0]).magnitude;
        float w0 = (c[3] - c[0]).magnitude;
        if (h < 1e-5f) { Debug.LogWarning("Tinggi PartsPanel nol."); return; }

        float dist = 0.95f;
        float viewH = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float targetH = PanelVpHeight * viewH;
        Undo.RecordObject(panel.transform, "Panel");
        panel.transform.localScale *= targetH / h;

        float btnTop = 0.35f;
        foreach (var p in btn) btnTop = Mathf.Max(btnTop, cam.WorldToViewportPoint(p).y);
        float vy = Mathf.Min(btnTop + 0.05f + PanelVpHeight * 0.5f, 0.97f - PanelVpHeight * 0.5f);
        Vector3 ppos = cam.ViewportToWorldPoint(new Vector3(0.78f, vy, dist));
        panel.transform.SetPositionAndRotation(ppos, cam.transform.rotation);

        float pxPerUnit = PanelVpHeight * 1080f / Mathf.Max(1f, rt.rect.height);
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Kamera: " + cam.transform.position.ToString("F2") + ", pitch "
                  + cam.transform.eulerAngles.x.ToString("F0") + ", mundur " + back.ToString("F1") + " m dari titik terdekat. "
                  + "Panel: tinggi dunia sebelumnya " + h.ToString("F2") + " m (lebar " + w0.ToString("F2")
                  + "), sekarang " + targetH.ToString("F2") + " m. Di layar 1080p 1 unit canvas = "
                  + pxPerUnit.ToString("F2") + " piksel, jadi font 18 sekitar " + (18f * pxPerUnit).ToString("F0")
                  + " piksel. Bila di bawah 12, besarkan font di kartu atau naikkan PanelVpHeight di SocketTools.cs.");
    }

    // ------------------------------------------------------------ 8. susun slot otomatis
    static void Put(SocketPoint s, Transform frame, Vector3 pos, Quaternion rot)
    {
        Undo.RecordObject(s.transform, "Susun slot");
        s.transform.SetPositionAndRotation(frame.TransformPoint(pos), frame.rotation * rot);
    }

    static void Report(StringBuilder sb, string name, Bounds b, Quaternion rotC, Vector3 pivotC, Vector3 imn, Vector3 imx)
    {
        Vector3 mn, mx;
        RotMinMax(b, rotC, out mn, out mx);
        mn += pivotC; mx += pivotC;
        float o = 0f;
        for (int i = 0; i < 3; i++) o = Mathf.Max(o, Mathf.Max(imn[i] - mn[i], mx[i] - imx[i]));
        sb.AppendLine(name + " pivot " + pivotC.ToString("F3")
            + (o > 0.003f ? "  [MASALAH] keluar dari bagian dalam casing " + o.ToString("F3") + " m"
                          : "  di dalam casing OK"));
    }

    [MenuItem("Tools/RakitPC/8. Susun Semua Slot Otomatis (titik awal)", false, 1008)]
    static void AutoLayout()
    {
        var map = new Dictionary<string, SocketPoint>();
        foreach (var s in Object.FindObjectsOfType<SocketPoint>()) map[s.name] = s;
        string[] names = { "Case_Socket", "Mobo_Socket", "PSU_Socket", "Fan_Socket", "CPU_Socket", "Cooler_Socket",
                           "RAM1_Socket", "RAM2_Socket", "GPU_Socket", "SSD_Socket", "Power_Socket" };
        foreach (string n in names)
            if (!map.ContainsKey(n)) { Debug.LogWarning("Slot tidak ditemukan: " + n + " (nama harus persis)."); return; }

        Bounds bCase, bMobo, bPsu, bFan, bRam, bGpu, bSsd, bCable;
        if (!TryBounds(PartType.Case, out bCase) || !TryBounds(PartType.Mobo, out bMobo)
            || !TryBounds(PartType.PSU, out bPsu) || !TryBounds(PartType.Fan, out bFan)
            || !TryBounds(PartType.RAM, out bRam) || !TryBounds(PartType.GPU, out bGpu)
            || !TryBounds(PartType.SSD, out bSsd) || !TryBounds(PartType.Cable, out bCable)) return;

        Transform ct = map["Case_Socket"].transform;
        Quaternion I = Quaternion.identity;
        Vector3 mn, mx;

        // bagian dalam casing (bingkai casing). Belakang = +X, baki mobo = dinding -Z
        Vector3 imn = bCase.min + Vector3.one * Wall;
        Vector3 imx = bCase.max - Vector3.one * Wall;
        float rearX = imx.x;
        float trayZ = imn.z;
        float midZ = (imn.z + imx.z) * 0.5f;

        // PSU: di dasar, rapat ke belakang
        RotMinMax(bPsu, I, out mn, out mx);
        Vector3 psuP = new Vector3(rearX - 0.002f - mx.x, imn.y - mn.y, midZ - (mn.z + mx.z) * 0.5f);
        float psuTop = psuP.y + mx.y;

        // Mobo: berdiri (rotasi X 90), menempel di baki, di atas PSU, menyisakan ruang kipas
        Quaternion qM = Quaternion.Euler(90f, 0f, 0f);
        RotMinMax(bMobo, qM, out mn, out mx);
        Vector3 moboP = new Vector3(rearX - RearGap - mx.x, psuTop + 0.012f - mn.y, trayZ + 0.004f - mn.z);

        // Kipas: di dinding belakang bagian atas, sisi tipis menempel dinding
        Quaternion qF = Quaternion.Euler(0f, 0f, 90f);
        RotMinMax(bFan, qF, out mn, out mx);
        Vector3 fanP = new Vector3(rearX - 0.002f - mx.x, imx.y - 0.01f - mx.y, midZ - (mn.z + mx.z) * 0.5f);

        // Komponen di permukaan mobo (bingkai mobo: X = lebar, Y = keluar dari papan, Z = ke bawah)
        Vector3 cpuM = new Vector3(0.045f, 0.006f, -0.045f);
        Vector3 coolM = new Vector3(0.045f, 0.010f, -0.045f);

        Quaternion qRl = Quaternion.Euler(0f, 90f, 0f);
        RotMinMax(bRam, qRl, out mn, out mx);
        Quaternion qRam = qM * qRl;
        Vector3 ram1M = new Vector3(-0.080f - (mn.x + mx.x) * 0.5f, 0.004f - mn.y, -(mn.z + mx.z) * 0.5f);
        Vector3 ram2M = new Vector3(-0.0375f - (mn.x + mx.x) * 0.5f, 0.004f - mn.y, ram1M.z);

        Quaternion qGl = Quaternion.Euler(-90f, 0f, 0f);
        RotMinMax(bGpu, qGl, out mn, out mx);
        Quaternion qGpu = qM * qGl;
        Vector3 gpuM = new Vector3(-(mn.x + mx.x) * 0.5f, 0.006f - mn.y, 0.117f - mx.z);

        RotMinMax(bSsd, I, out mn, out mx);
        Vector3 ssdM = new Vector3(0.045f - (mn.x + mx.x) * 0.5f, 0.004f - mn.y, 0.037f - (mn.z + mx.z) * 0.5f);

        RotMinMax(bCable, I, out mn, out mx);
        Vector3 cabM = new Vector3(0.100f - mx.x, 0.012f - mn.y, -0.094f - (mn.z + mx.z) * 0.5f);

        Put(map["Mobo_Socket"], ct, moboP, qM);
        Put(map["PSU_Socket"], ct, psuP, I);
        Put(map["Fan_Socket"], ct, fanP, qF);
        Put(map["CPU_Socket"], ct, moboP + qM * cpuM, qM);
        Put(map["Cooler_Socket"], ct, moboP + qM * coolM, qM);
        Put(map["RAM1_Socket"], ct, moboP + qM * ram1M, qRam);
        Put(map["RAM2_Socket"], ct, moboP + qM * ram2M, qRam);
        Put(map["GPU_Socket"], ct, moboP + qM * gpuM, qGpu);
        Put(map["SSD_Socket"], ct, moboP + qM * ssdM, qM);
        Put(map["Power_Socket"], ct, moboP + qM * cabM, qM);

        var sb = new StringBuilder("=== HASIL SUSUN SLOT (bingkai casing) ===\n");
        sb.AppendLine("Bagian dalam casing: " + imn.ToString("F3") + " sampai " + imx.ToString("F3"));
        Report(sb, "PSU   ", bPsu, I, psuP, imn, imx);
        Report(sb, "Mobo  ", bMobo, qM, moboP, imn, imx);
        Report(sb, "Fan   ", bFan, qF, fanP, imn, imx);
        Report(sb, "RAM1  ", bRam, qRam, moboP + qM * ram1M, imn, imx);
        Report(sb, "RAM2  ", bRam, qRam, moboP + qM * ram2M, imn, imx);
        Report(sb, "GPU   ", bGpu, qGpu, moboP + qM * gpuM, imn, imx);
        Report(sb, "SSD   ", bSsd, qM, moboP + qM * ssdM, imn, imx);
        Report(sb, "Kabel ", bCable, qM, moboP + qM * cabM, imn, imx);
        System.IO.File.WriteAllText("Assets/HasilSusun.txt", sb.ToString());
        AssetDatabase.Refresh();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log(sb.ToString());
    }

    // ------------------------------------------------------------ 9. teks 3D di dalam Canvas
    [MenuItem("Tools/RakitPC/9. Perbaiki Teks 3D di dalam Canvas", false, 1009)]
    static void FixCanvasTexts()
    {
        int n = 0;
        foreach (var t in Object.FindObjectsOfType<TMPro.TextMeshPro>())
        {
            if (t.GetComponentInParent<Canvas>() == null) continue;
            Transform parent = t.transform.parent;
            string nm = t.gameObject.name;
            var go = new GameObject(nm, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Perbaiki teks");
            go.layer = t.gameObject.layer;
            go.transform.SetParent(parent, false);
            var ui = go.AddComponent<TMPro.TextMeshProUGUI>();
            ui.font = t.font;
            ui.text = t.text;
            ui.fontSize = t.fontSize;
            ui.color = nm == "Footer" ? (Color)new Color32(0x9A, 0xA5, 0xB1, 255) : t.color;
            ui.alignment = TMPro.TextAlignmentOptions.Center;
            ui.raycastTarget = false;
            RectTransform r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = new Vector2(0f, 40f);
            Undo.DestroyObjectImmediate(t.gameObject);
            n++;
        }
        if (n > 0) EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Teks 3D di dalam Canvas diganti menjadi teks UI: " + n);
    }

    // ------------------------------------------------------------ 10. mouse di samping keyboard
    [MenuItem("Tools/RakitPC/10. Rapikan Mouse di Samping Keyboard", false, 1010)]
    static void PlaceMouse()
    {
        SocketPoint kb = FindSlot(PartType.Keyboard), ms = FindSlot(PartType.Mouse);
        Bounds bk, bm;
        if (kb == null || ms == null || !TryBounds(PartType.Keyboard, out bk) || !TryBounds(PartType.Mouse, out bm))
        {
            Debug.LogWarning("Slot atau PartData Keyboard dan Mouse tidak lengkap.");
            return;
        }
        Camera cam = Camera.main;
        Vector3 f = cam != null ? cam.transform.forward : Vector3.back;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) f = Vector3.back;
        Vector3 r = Vector3.Cross(Vector3.up, f.normalized);

        Vector3 kbCenter = kb.transform.TransformPoint(bk.center);
        Vector3 c = kbCenter + r * (bk.extents.x + bm.extents.x + 0.05f);
        Vector3 pivot = c - ms.transform.rotation * bm.center;
        pivot.y = kb.transform.position.y;
        Undo.RecordObject(ms.transform, "Rapikan mouse");
        ms.transform.position = pivot;
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Mouse dipindah ke sisi kanan keyboard (dari sudut kamera): " + pivot.ToString("F2")
                  + ". Jalankan menu 1 lagi agar posisinya dipastikan di atas meja.");
    }

    // ------------------------------------------------------------ 11. sembunyikan casing pratinjau
    [MenuItem("Tools/RakitPC/11. Pratinjau: Sembunyikan atau Tampilkan Casing", false, 1011)]
    static void ToggleCasePreview()
    {
        var list = new List<GameObject>();
        foreach (var t in Object.FindObjectsOfType<Transform>())
        {
            if (t.name != PreviewName || t.parent == null) continue;
            var s = t.parent.GetComponent<SocketPoint>();
            if (s != null && s.accepts == PartType.Case) list.Add(t.gameObject);
        }
        if (list.Count == 0) { Debug.LogWarning("Pratinjau casing tidak ada. Jalankan menu 3 dulu."); return; }
        bool hide = !SceneVisibilityManager.instance.IsHidden(list[0], true);
        foreach (var g in list)
        {
            if (hide) SceneVisibilityManager.instance.Hide(g, true);
            else SceneVisibilityManager.instance.Show(g, true);
        }
        Debug.Log(hide ? "Casing pratinjau disembunyikan. Jalankan menu ini lagi untuk menampilkan." : "Casing pratinjau ditampilkan.");
    }
// ------------------------------------------------------------ 11. sembunyikan casing pratinjau
        [MenuItem("Tools/RakitPC/12. Terapkan Pengaturan Otomatis (Hold Lift, Teks Monitor, Skala Kabel)", false, 1012)]
    static void ApplyAutoSettings()
    {
        var m = Object.FindObjectOfType<AssemblyManager>();
        if (m != null && m.holdLift < 0.28f)
        {
            Undo.RecordObject(m, "Hold Lift");
            m.holdLift = 0.28f;
            EditorUtility.SetDirty(m);
            Debug.Log("Hold Lift diatur 0.28.");
        }

        foreach (var t in Object.FindObjectsOfType<TMPro.TextMeshPro>())
        {
            if (t.name != "Monitor_Text") continue;
            Undo.RecordObject(t, "Monitor text");
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.05f;
            t.fontSizeMax = 2f;
            EditorUtility.SetDirty(t);
            Debug.Log("Monitor_Text: Auto Size aktif (0.05 sampai 2).");
        }

        PartData d = FindPartData(PartType.Cable);
        Bounds bc;
        if (d != null && d.prefab != null && TryBounds(PartType.Cable, out bc))
        {
            float max = Mathf.Max(bc.size.x, Mathf.Max(bc.size.y, bc.size.z));
            if (max > 0.4f)
            {
                var mf = d.prefab.GetComponentInChildren<MeshFilter>();
                string path = (mf != null && mf.sharedMesh != null) ? AssetDatabase.GetAssetPath(mf.sharedMesh) : null;
                var imp = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null)
                    Debug.LogWarning("FBX kabel tidak ditemukan otomatis. Kalikan Scale Factor FBX kabel dengan 0.33 secara manual.");
                else
                {
                    imp.globalScale *= 0.33f;
                    imp.SaveAndReimport();
                    Debug.Log("Skala FBX kabel dikalikan 0.33 (" + path + "). Panjang sebelumnya " + max.ToString("F2") + " m.");
                }
            }
        }
        EditorSceneManager.MarkAllScenesDirty();
    }
}
#endif