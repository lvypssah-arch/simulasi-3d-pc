#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MissingScriptTools
{
    static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    [MenuItem("Tools/RakitPC/17. Cari Missing Script (hanya laporan)", false, 1017)]
    static void Find()
    {
        var sb = new StringBuilder("=== MISSING SCRIPT ===\n--- Di scene ---\n");
        int total = 0;

        foreach (var go in Object.FindObjectsOfType<GameObject>(true))
        {
            if (EditorUtility.IsPersistent(go)) continue;
            int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (n == 0) continue;
            total += n;
            sb.AppendLine("[MASALAH] " + PathOf(go.transform) + ": " + n + " komponen missing");
        }

        sb.AppendLine("--- Di prefab ---");
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (n == 0) continue;
                total += n;
                sb.AppendLine("[MASALAH] " + path + " -> " + PathOf(t) + ": " + n + " komponen missing");
            }
        }

        sb.AppendLine(total == 0 ? "Tidak ada missing script." : total + " komponen missing ditemukan.");
        System.IO.File.WriteAllText("Assets/HasilMissing.txt", sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log(sb.ToString());
    }
}
#endif