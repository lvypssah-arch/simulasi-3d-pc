#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SceneCheck
{
    static StringBuilder sb;
    static void Warn(string m) { sb.AppendLine("[MASALAH] " + m); }
    static void Info(string m) { sb.AppendLine(m); }

    public static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    public static Bounds BoundsOf(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    public static bool FindDeskTop(out float topY, out string info)
    {
        bool ok = TableSurface.Build();
        topY = TableSurface.Y;
        info = TableSurface.Info;
        return ok;
    }

    [MenuItem("Tools/RakitPC/0. Cek Scene dan Prefab", false, 1000)]
    static void Run()
    {
        sb = new StringBuilder("=== CEK RAKITPC ===\n");
        AssemblyManager mgr = Object.FindObjectOfType<AssemblyManager>();
        CheckTable();
        CheckCamera();
        CheckPanel();
        CheckSockets();
        CheckParts(mgr);
        CheckManager(mgr);
        CheckTexts();
        System.IO.File.WriteAllText("Assets/HasilCek.txt", sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log("Hasil lengkap disimpan di Assets/HasilCek.txt");
    }

    // ---------------------------------------------------------------- meja
       static void CheckFootprint(string name, List<Vector3> pts, float sy, bool centerOnly)
    {
        float minY = float.MaxValue;
        foreach (var p in pts) minY = Mathf.Min(minY, p.y);
        int outside = 0, bottom = 0;
        float cx = 0f, cz = 0f;
        Vector3 firstOut = Vector3.zero;
        foreach (var p in pts)
        {
            if (p.y > minY + 0.01f) continue;
            bottom++; cx += p.x; cz += p.z;
            if (!TableSurface.Contains(p.x, p.z))
            {
                if (outside == 0) firstOut = p;
                outside++;
            }
        }
        float gap = minY - sy;
        Info(name + ": dasar " + gap.ToString("F3") + " m dari permukaan meja");
        bool bad = centerOnly ? (bottom > 0 && !TableSurface.Contains(cx / bottom, cz / bottom)) : outside > 0;
        if (bad)
            Warn(name + ": " + outside + " dari " + bottom + " sudut dasar di luar permukaan meja (contoh x "
                 + firstOut.x.ToString("F2") + ", z " + firstOut.z.ToString("F2") + "; meja x "
                 + TableSurface.MinX.ToString("F2") + ".." + TableSurface.MaxX.ToString("F2") + ", z "
                 + TableSurface.MinZ.ToString("F2") + ".." + TableSurface.MaxZ.ToString("F2") + "). Jalankan menu 1.");
        if (Mathf.Abs(gap) > 0.01f)
            Warn(name + ": " + (gap > 0f ? "melayang " : "tertanam ") + Mathf.Abs(gap).ToString("F3")
                 + " m dari permukaan meja. Jalankan menu 1.");
    }

    static void CheckTable()
    {
        if (!TableSurface.Build()) { Warn(TableSurface.Info); return; }
        Info(TableSurface.Info);
        float sy = TableSurface.Y;

        foreach (var s in Object.FindObjectsOfType<SocketPoint>())
        {
            if (s.accepts != PartType.Case && s.accepts != PartType.Keyboard && s.accepts != PartType.Mouse) continue;
            Bounds b;
            if (!SocketTools.TryBounds(s.accepts, out b)) continue;
            var pts = new List<Vector3>();
            SocketTools.AddCorners(pts, b, s.transform);
            CheckFootprint(s.name + " (" + s.accepts + ")", pts, sy, false);
        }
        foreach (string n in new[] { "Btn_Parts", "Btn_Hint", "Btn_Reset", "Monitor" })
        {
            GameObject g = GameObject.Find(n);
            if (g == null) { Warn(n + " tidak ditemukan."); continue; }
            Bounds b;
            if (!SocketTools.BodyBounds(g, out b)) continue;
            var pts = new List<Vector3>();
            SocketTools.AddCorners(pts, b, null);
            Info(n + ": ukuran " + b.size.ToString("F3") + " m, pusat " + b.center.ToString("F2"));
            CheckFootprint(n, pts, sy, n == "Monitor");
        }

        SocketPoint kb = null, ms = null;
        foreach (var s in Object.FindObjectsOfType<SocketPoint>())
        {
            if (s.accepts == PartType.Keyboard) kb = s;
            if (s.accepts == PartType.Mouse) ms = s;
        }
        if (kb != null && ms != null)
        {
            float d = Vector3.Distance(kb.transform.position, ms.transform.position);
            Info("Jarak Keyboard ke Mouse: " + d.ToString("F2") + " m");
            if (d > 0.9f) Warn("Mouse " + d.ToString("F2") + " m dari keyboard. Jalankan menu 10.");
        }
    }

    // ---------------------------------------------------------------- kamera
    static void CheckCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) { Warn("Tidak ada kamera ber-tag MainCamera."); return; }
        Info("Kamera dunia " + cam.transform.position.ToString("F2") + ", rotasi "
             + cam.transform.eulerAngles.ToString("F0") + ", FOV " + cam.fieldOfView
             + ", near " + cam.nearClipPlane);
        if (cam.nearClipPlane > 0.1f) Warn("Near Clipping Plane terlalu besar. Set 0.05.");

        if (TableSurface.Ready)
        {
            float h = cam.transform.position.y - TableSurface.Y;
            Info("Tinggi kamera di atas meja: " + h.ToString("F2") + " m (wajar 0.45 - 0.9)");
            if (h < 0.4f) Warn("Kamera terlalu rendah, komponen yang dipegang tidak terjangkau.");
            if (h > 1.2f) Warn("Kamera terlalu tinggi di atas meja. Jalankan menu 6.");
        }

        foreach (string n in new[] { "AssemblyRoot", "Btn_Parts", "Btn_Hint", "Btn_Reset" })
        {
            GameObject g = GameObject.Find(n);
            if (g == null) { Warn(n + " tidak ditemukan."); continue; }
            Vector3 v = cam.WorldToViewportPoint(g.transform.position);
            Info(n + ": dunia " + g.transform.position.ToString("F2") + ", jarak " + v.z.ToString("F2")
                 + " m, posisi layar (" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ")");
            if (!(v.z > 0f && v.x > 0.05f && v.x < 0.95f && v.y > 0.05f && v.y < 0.95f))
                Warn(n + " tidak terlihat penuh dari kamera (x dan y harus antara 0.05 dan 0.95). Jalankan menu 6.");
        }

        foreach (string n in new[] { "AssemblyStation", "Player", "AssemblyRoot" })
        {
            GameObject g = GameObject.Find(n);
            if (g != null && Vector3.Distance(g.transform.lossyScale, Vector3.one) > 0.01f)
                Warn(n + ": scale dunia " + g.transform.lossyScale.ToString("F2") + " (harus 1, 1, 1).");
        }
    }

       static void CheckPanel()
    {
        var p = Object.FindObjectOfType<PartsPanel>();
        if (p == null) return;
        Canvas pcv = p.GetComponent<Canvas>();
        if (pcv != null && pcv.renderMode != RenderMode.WorldSpace)
        {
            Info("PartsPanel: overlay layar (" + pcv.renderMode + "), ukuran dunia tidak diperiksa.");
            return;
        }
        RectTransform rt = p.transform as RectTransform;
        if (rt == null) return;
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        float h = (c[1] - c[0]).magnitude, w = (c[3] - c[0]).magnitude;
        Info("PartsPanel ukuran dunia " + w.ToString("F2") + " x " + h.ToString("F2") + " m (unit canvas "
             + rt.rect.width.ToString("F0") + " x " + rt.rect.height.ToString("F0") + ")");
        if (h > 1.0f || w > 1.0f)
            Warn("PartsPanel terlalu besar di dunia (" + w.ToString("F2") + " x " + h.ToString("F2") + " m). Jalankan menu 6.");
    }

    // ---------------------------------------------------------------- slot
    static void CheckSockets()
    {
        int layer = LayerMask.NameToLayer("Socket");
        if (layer < 0) Warn("Layer Socket belum dibuat.");
        var all = Object.FindObjectsOfType<SocketPoint>();
        Info("SocketPoint di scene: " + all.Length);
        foreach (var s in all)
        {
            Info(" - " + s.name + " (" + s.accepts + ") dunia " + s.transform.position.ToString("F2"));
            if (s.gameObject.layer != layer) Warn(s.name + ": Layer bukan Socket.");
            var bc = s.GetComponent<BoxCollider>();
            if (bc == null) Warn(s.name + ": tidak ada Box Collider.");
            else if (bc.size.x > 1f || bc.size.y > 1f || bc.size.z > 1f)
                Warn(s.name + ": collider lebih dari 1 m. Jalankan menu 5 setelah posisi slot final.");
            if (Vector3.Distance(s.transform.lossyScale, Vector3.one) > 0.01f)
                Warn(s.name + ": scale dunia " + s.transform.lossyScale.ToString("F2") + " (sebaiknya 1, 1, 1).");
            bool free = s.accepts == PartType.Case || s.accepts == PartType.Mouse
                        || s.accepts == PartType.Keyboard;
            if (!free && (s.requires == null || s.requires.Length == 0))
                Warn(s.name + ": Requires kosong. Jalankan menu 2.");
            if (s.requires != null)
                foreach (var r in s.requires)
                    if (r == null) Warn(s.name + ": ada elemen Requires bernilai None.");
        }
        for (int i = 0; i < all.Length; i++)
            for (int j = i + 1; j < all.Length; j++)
                if (all[i].accepts == all[j].accepts &&
                    Vector3.Distance(all[i].transform.position, all[j].transform.position) < 0.01f)
                    Warn(all[i].name + " dan " + all[j].name + " berada di posisi yang sama, komponen akan bertumpuk. Jalankan menu 8.");

        int pv = 0;
        foreach (var t in Object.FindObjectsOfType<Transform>()) if (t.name == "_PRATINJAU") pv++;
        if (pv > 0) Warn("Masih ada " + pv + " objek _PRATINJAU di scene. Jalankan menu 7 sebelum Save atau Play.");
    }

    static void CheckParts(AssemblyManager m)
    {
        if (m == null || m.parts == null) return;
        var sockets = Object.FindObjectsOfType<SocketPoint>();
        foreach (var d in m.parts)
        {
            if (d == null) { Warn("Ada elemen kosong di Parts."); continue; }
            if (d.prefab == null) { Warn(d.name + ": Prefab kosong."); continue; }
            int slots = 0;
            foreach (var s in sockets) if (s.accepts == d.type) slots++;
            if (slots < d.copies)
                Warn(d.name + ": butuh " + d.copies + " slot " + d.type + ", di scene hanya " + slots
                     + " (level tidak bisa selesai).");

            var go = (GameObject)PrefabUtility.InstantiatePrefab(d.prefab);
            Vector3 rot = go.transform.eulerAngles, scl = go.transform.localScale;
            Bounds b = BoundsOf(go);
            Vector3 off = b.center - go.transform.position;
            Info(d.name + ": ukuran " + b.size.ToString("F3") + " m | pivot ke pusat " + off.ToString("F3")
                 + " | dasar " + (b.min.y - go.transform.position.y).ToString("F3")
                 + " | rot " + rot.ToString("F0") + " | scale " + scl.ToString("F2"));
            if (Vector3.Distance(scl, Vector3.one) > 0.01f) Warn(d.name + ": scale root bukan 1.");
            if (rot.sqrMagnitude > 1f) Warn(d.name + ": rotation root bukan 0.");
            float max = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (max > 1.2f) Warn(d.name + ": terlalu besar (" + max.ToString("F2") + " m).");
            if (max < 0.03f) Warn(d.name + ": terlalu kecil (" + max.ToString("F3") + " m).");
            if (d.type == PartType.Cable && max > 0.4f)
                Warn("Kabel " + max.ToString("F2") + " m lebih panjang dari bagian dalam casing. "
                     + "Kalikan Scale Factor FBX kabel dengan 0.33, lalu Apply.");
            Object.DestroyImmediate(go);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:PartData"))
        {
            var p = AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(guid));
            if (p != null && System.Array.IndexOf(m.parts, p) < 0)
                Info("(info) " + p.name + " ada di project tetapi tidak dipakai di Parts, diabaikan.");
        }
    }

    static void CheckManager(AssemblyManager m)
    {
        if (m == null) { Warn("AssemblyManager tidak ada di scene."); return; }
        int layer = LayerMask.NameToLayer("Socket");
        if (layer >= 0 && (m.socketMask.value & (1 << layer)) == 0)
            Warn("Socket Mask belum mencakup layer Socket.");
        if (m.hologramMaterial == null) Warn("Hologram Material kosong.");
        if (m.panel == null || m.hud == null) Warn("Panel atau Hud belum diisi.");
        int total = 0;
        if (m.parts != null) foreach (var p in m.parts) if (p != null) total += p.copies;
        int sc = Object.FindObjectsOfType<SocketPoint>().Length;
        Info("Total langkah (jumlah copies) " + total + ", jumlah slot " + sc);
        if (total != sc) Warn("Total langkah tidak sama dengan jumlah slot.");
        if (m.allSockets == null || m.allSockets.Length != sc) Warn("All Sockets tidak lengkap.");
        if (m.holdLift < 0.25f) Warn("Hold Lift " + m.holdLift.ToString("F2") + " terlalu rendah, casing yang dipegang bisa menembus meja. Set 0.28.");
        int ev = Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length;
        if (ev != 1) Warn("Jumlah EventSystem " + ev + " (harus 1).");
        int cl = LayerMask.NameToLayer("Clickable");
        foreach (var c in Object.FindObjectsOfType<Clickable3D>())
            if (c.gameObject.layer != cl) Warn(c.name + ": Layer bukan Clickable.");
    }

    static void CheckTexts()
    {
        foreach (var t in Object.FindObjectsOfType<TMPro.TextMeshPro>())
        {
            Info("Teks 3D: \"" + t.text.Replace("\n", " ") + "\" di " + PathOf(t.transform)
                 + " (ukuran font " + t.fontSize + ")");
            if (t.GetComponentInParent<Canvas>() != null)
                Warn("Teks 3D berada di dalam Canvas: " + PathOf(t.transform)
                     + ". Jalankan menu 9.");
        }
    }
}
#endif