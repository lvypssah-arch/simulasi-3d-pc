#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ModelCheckTools
{
    static string SourcePath(GameObject go, GameObject prefab)
    {
        Object src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
        string path = src != null ? AssetDatabase.GetAssetPath(src) : "";
        return string.IsNullOrEmpty(path) ? AssetDatabase.GetAssetPath(prefab) : path;
    }

    static string Fix(string path)
    {
        if (string.IsNullOrEmpty(path)) return "path tidak ditemukan";
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp != null)
        {
            if (!imp.importCameras)
                return path + " (Import Cameras sudah mati, tetapi komponen masih ada: periksa manual)";
            imp.importCameras = false;
            imp.SaveAndReimport();
            return "Import Cameras dimatikan pada " + path;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
                if (comp != null && comp.GetType().Name == "UniversalAdditionalCameraData") Object.DestroyImmediate(comp);
            foreach (var a in root.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(a);
            foreach (var c in root.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return "Camera dan AudioListener dihapus dari prefab " + path;
    }

    [MenuItem("Tools/RakitPC/16. Cek dan Matikan Camera Bawaan Model", false, 1016)]
    static void CheckModelCameras()
    {
        var mgr = Object.FindObjectOfType<AssemblyManager>();
        if (mgr == null || mgr.parts == null) { Debug.LogWarning("AssemblyManager atau daftar Parts tidak ditemukan."); return; }

        var sb = new StringBuilder("=== CAMERA DAN AUDIOLISTENER BAWAAN MODEL ===\n");
        var done = new HashSet<string>();
        int bad = 0;
        foreach (var p in mgr.parts)
        {
            if (p == null || p.prefab == null) continue;
            var cams = p.prefab.GetComponentsInChildren<Camera>(true);
            var auds = p.prefab.GetComponentsInChildren<AudioListener>(true);
            if (cams.Length == 0 && auds.Length == 0) { sb.AppendLine(p.name + ": bersih"); continue; }
            bad++;
            sb.AppendLine("[MASALAH] " + p.name + ": " + cams.Length + " Camera, " + auds.Length + " AudioListener");
            var paths = new List<string>();
            foreach (var c in cams) paths.Add(SourcePath(c.gameObject, p.prefab));
            foreach (var a in auds) paths.Add(SourcePath(a.gameObject, p.prefab));
            foreach (string path in paths)
                if (done.Add(path)) sb.AppendLine("   " + Fix(path));
        }

        sb.AppendLine("--- Kamera di scene ---");
        foreach (var c in Object.FindObjectsOfType<Camera>(true))
            sb.AppendLine(SceneCheck.PathOf(c.transform)
                + (c.enabled && c.gameObject.activeInHierarchy ? " (aktif)" : " (mati)")
                + ", depth " + c.depth + (c.CompareTag("MainCamera") ? " [MainCamera]" : ""));
        int al = 0;
        foreach (var a in Object.FindObjectsOfType<AudioListener>()) if (a.enabled) al++;
        sb.AppendLine("AudioListener aktif di scene: " + al + " (harus 1)");
        sb.AppendLine(bad == 0 ? "Semua prefab Parts bersih dari Camera bawaan."
                               : bad + " prefab bermasalah, sudah diperbaiki di atas.");

        System.IO.File.WriteAllText("Assets/HasilKamera.txt", sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log(sb.ToString());
    }
}
#endif