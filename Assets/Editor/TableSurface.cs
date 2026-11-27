#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Mengukur permukaan meja dari segitiga mesh yang MENGHADAP ATAS saja.
public static class TableSurface
{
    struct Tri { public Vector2 a, b, c; public float y, area; }

    public static bool Ready;
    public static float Y;
    public static float MinX, MaxX, MinZ, MaxZ;
    public static string Info = "";

    static readonly List<Tri> main = new List<Tri>();

    static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    static bool In(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    public static bool Contains(float x, float z)
    {
        if (!Ready) return false;
        if (main.Count == 0) return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
        Vector2 p = new Vector2(x, z);
        for (int i = 0; i < main.Count; i++)
        {
            Tri t = main[i];
            if (In(p, t.a, t.b, t.c)) return true;
        }
        return false;
    }

    public static bool ContainsMargin(float x, float z, float m)
    {
        return Contains(x, z) && Contains(x + m, z) && Contains(x - m, z)
            && Contains(x, z + m) && Contains(x, z - m);
    }

    public static bool Build()
    {
        Ready = false;
        main.Clear();
        Info = "";
        var all = new List<Tri>();
        int meshes = 0;
        bool usedNormals = false;

        foreach (var mf in Object.FindObjectsOfType<MeshFilter>())
        {
            string p = SceneCheck.PathOf(mf.transform).ToLower();
            if (p.Contains("_pratinjau") || p.Contains("monitor")) continue;
            if (!p.Contains("table") && !p.Contains("desk")) continue;
            Mesh m = mf.sharedMesh;
            if (m == null) continue;
            Vector3[] v, nrm; int[] t;
            try { v = m.vertices; t = m.triangles; nrm = m.normals; }
            catch { continue; }
            bool hasN = nrm != null && nrm.Length == v.Length;
            if (hasN) usedNormals = true;
            meshes++;
            Matrix4x4 w = mf.transform.localToWorldMatrix;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector3 a = w.MultiplyPoint3x4(v[t[i]]);
                Vector3 b = w.MultiplyPoint3x4(v[t[i + 1]]);
                Vector3 c = w.MultiplyPoint3x4(v[t[i + 2]]);
                Vector3 n = Vector3.Cross(b - a, c - a);
                float len = n.magnitude;
                if (len < 1e-10f) continue;

                float up;
                if (hasN)
                {
                    Vector3 nn = w.MultiplyVector(nrm[t[i]] + nrm[t[i + 1]] + nrm[t[i + 2]]);
                    if (nn.sqrMagnitude < 1e-10f) continue;
                    up = nn.normalized.y;            // hanya yang menghadap ATAS
                }
                else up = Mathf.Abs(n.y) / len;      // tanpa normal: terpaksa dua sisi

                if (up < 0.95f) continue;
                all.Add(new Tri
                {
                    a = new Vector2(a.x, a.z),
                    b = new Vector2(b.x, b.z),
                    c = new Vector2(c.x, c.z),
                    y = (a.y + b.y + c.y) / 3f,
                    area = 0.5f * Mathf.Abs(n.y)
                });
            }
        }
        if (all.Count == 0) return Fallback();

        var area = new Dictionary<int, float>();
        foreach (var t in all)
        {
            int k = Mathf.RoundToInt(t.y * 200f);
            float a;
            area.TryGetValue(k, out a);
            area[k] = a + t.area;
        }
        int best = 0; float bestA = -1f;
        foreach (var kv in area) if (kv.Value > bestA) { bestA = kv.Value; best = kv.Key; }

        // ambil lapisan TERTINGGI yang luasnya cukup besar (>= 50% lapisan terbesar), maks 0,5 m di atasnya
        int chosen = best;
        foreach (var kv in area)
            if (kv.Key > chosen && kv.Key <= best + 100 && kv.Value >= 0.5f * bestA) chosen = kv.Key;

        float sumY = 0f, sumA = 0f;
        MinX = float.MaxValue; MaxX = float.MinValue; MinZ = float.MaxValue; MaxZ = float.MinValue;
        foreach (var t in all)
        {
            if (Mathf.Abs(Mathf.RoundToInt(t.y * 200f) - chosen) > 1) continue;
            main.Add(t);
            sumY += t.y * t.area; sumA += t.area;
            foreach (var q in new[] { t.a, t.b, t.c })
            {
                MinX = Mathf.Min(MinX, q.x); MaxX = Mathf.Max(MaxX, q.x);
                MinZ = Mathf.Min(MinZ, q.y); MaxZ = Mathf.Max(MaxZ, q.y);
            }
        }
        Y = sumA > 0f ? sumY / sumA : chosen / 200f;
        Ready = true;

        float bbox = (MaxX - MinX) * (MaxZ - MinZ);
        var list = new List<KeyValuePair<int, float>>(area);
        list.Sort((p, q) => q.Value.CompareTo(p.Value));
        var sb = new StringBuilder();
        sb.Append("Permukaan meja: y = " + Y.ToString("F3") + ", luas " + sumA.ToString("F2") + " m2 (kotak x*z "
            + bbox.ToString("F2") + " m2), x " + MinX.ToString("F2") + " sampai " + MaxX.ToString("F2")
            + ", z " + MinZ.ToString("F2") + " sampai " + MaxZ.ToString("F2") + " (" + meshes + " mesh, "
            + (usedNormals ? "hanya sisi atas" : "TANPA normal, dua sisi") + "). Lapisan terbesar: ");
        for (int i = 0; i < list.Count && i < 3; i++)
            sb.Append("y " + (list[i].Key / 200f).ToString("F3") + " = " + list[i].Value.ToString("F2") + " m2; ");
        Info = sb.ToString();
        return true;
    }

    static bool Fallback()
    {
        Renderer best = null;
        float bestArea = 0f;
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            string p = SceneCheck.PathOf(r.transform).ToLower();
            if (p.Contains("_pratinjau") || p.Contains("monitor")) continue;
            if (!p.Contains("table") && !p.Contains("desk")) continue;
            float a = r.bounds.size.x * r.bounds.size.z;
            if (a > bestArea) { bestArea = a; best = r; }
        }
        if (best == null)
        {
            Info = "Tidak ada renderer meja (nama objek harus mengandung table atau desk).";
            return false;
        }
        Y = best.bounds.max.y;
        MinX = best.bounds.min.x; MaxX = best.bounds.max.x;
        MinZ = best.bounds.min.z; MaxZ = best.bounds.max.z;
        Ready = true;
        Info = "[PERHATIAN] Mesh meja tidak bisa dibaca, memakai kotak pembatas: y = " + Y.ToString("F3")
             + ", x " + MinX.ToString("F2") + " sampai " + MaxX.ToString("F2")
             + ", z " + MinZ.ToString("F2") + " sampai " + MaxZ.ToString("F2")
             + ". Aktifkan Read/Write pada FBX meja agar pengukuran akurat.";
        return true;
    }
}
#endif