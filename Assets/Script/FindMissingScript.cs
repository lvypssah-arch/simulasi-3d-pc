#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FindMissingScripts
{
    [MenuItem("Tools/RakitPC/22. Cari Script Hilang di Scene")]
    static void Find()
    {
        int found = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (n > 0)
                {
                    found += n;
                    Debug.LogWarning("Script hilang (" + n + ") pada: " + GetPath(t), t.gameObject);
                }
            }
        Debug.Log("Pencarian selesai. Total script hilang: " + found);
    }

    [MenuItem("Tools/RakitPC/23. Hapus Script Hilang di Scene")]
    static void Remove()
    {
        int removed = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Script hilang dihapus: " + removed + ". Simpan scene dengan Ctrl+S.");
    }

    static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
#endif