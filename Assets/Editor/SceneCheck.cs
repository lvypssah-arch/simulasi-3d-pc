#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SceneCheck
{
    static StringBuilder sb;
    static void Warn(string m) { sb.AppendLine("[MASALAH] " + m); }
    static void Info(string m) { sb.AppendLine(m); }

    static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    static Bounds BoundsOf(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    [MenuItem("Tools/RakitPC/Cek Scene dan Prefab")]
    static void Run()
    {
        sb = new StringBuilder("=== CEK RAKITPC ===\n");
        CheckCamera(); CheckSockets(); CheckParts(); CheckManager(); CheckTexts();
        Debug.Log(sb.ToString());
    }

    static void CheckCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) { Warn("Tidak ada kamera ber-tag MainCamera."); return; }
        Info("Kamera dunia " + cam.transform.position.ToString("F2") + ", FOV " + cam.fieldOfView
             + ", near " + cam.nearClipPlane);
        if (cam.nearClipPlane > 0.1f) Warn("Near Clipping Plane terlalu besar. Set 0.05.");
        GameObject root = GameObject.Find("AssemblyRoot");
        if (root == null) { Warn("AssemblyRoot tidak ditemukan."); return; }
        float h = cam.transform.position.y - root.transform.position.y;
        Info("AssemblyRoot dunia " + root.transform.position.ToString("F2")
             + ", kamera " + h.ToString("F2") + " m di atas meja (wajar 0.35-0.6)");
        if (h < 0.3f || h > 0.7f) Warn("Tinggi kamera terhadap meja tidak wajar.");
        GameObject desk = GameObject.Find("DeskSet");
        if (desk != null) Info("DeskSet ukuran " + BoundsOf(desk).size.ToString("F2")
                               + " (panjang meja seharusnya sekitar 2.6 x 0.8)");
    }

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
                Warn(s.name + ": collider lebih dari 1 m (periksa Size).");
            if (s.accepts != PartType.Case && (s.requires == null || s.requires.Length == 0))
                Warn(s.name + ": Requires kosong (urutan tidak dicek, hologram selalu cyan).");
            if (s.requires != null)
                foreach (var r in s.requires)
                    if (r == null) Warn(s.name + ": ada elemen Requires bernilai None.");
        }
    }

    static void CheckParts()
    {
        var sockets = Object.FindObjectsOfType<SocketPoint>();
        foreach (string guid in AssetDatabase.FindAssets("t:PartData"))
        {
            var d = AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(guid));
            if (d == null) continue;
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
            if (new Vector2(off.x, off.z).magnitude > 0.25f * Mathf.Max(b.size.x, b.size.z) + 0.01f)
                Warn(d.name + ": pivot jauh dari tengah (periksa, boleh hanya jika memang titik pasang).");
            Object.DestroyImmediate(go);
        }
    }

    static void CheckManager()
    {
        var m = Object.FindObjectOfType<AssemblyManager>();
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
        int ev = Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length;
        if (ev != 1) Warn("Jumlah EventSystem " + ev + " (harus 1).");
        int cl = LayerMask.NameToLayer("Clickable");
        foreach (var c in Object.FindObjectsOfType<Clickable3D>())
            if (c.gameObject.layer != cl) Warn(c.name + ": Layer bukan Clickable.");
    }

    static void CheckTexts()
    {
        foreach (var t in Object.FindObjectsOfType<TMPro.TextMeshPro>())
            Info("Teks 3D: \"" + t.text.Replace("\n", " ") + "\" di " + PathOf(t.transform)
                 + " (ukuran font " + t.fontSize + ")");
    }
}
#endif