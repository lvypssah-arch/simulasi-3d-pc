#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplayUISkinInstaller
{
    static TMP_FontAsset FindFont(params string[] names)
    {
        foreach (string n in names)
            foreach (string g in AssetDatabase.FindAssets("t:TMP_FontAsset " + n))
            {
                var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g));
                if (f != null && f.name.ToLower().Contains(n)) return f;
            }
        return null;
    }

    [MenuItem("Tools/RakitPC/30. Pasang Skin UI Gameplay (buka scene gameplay dulu)", false, 1030)]
    static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name == "MainMenu")
        {
            Debug.LogWarning("Buka scene gameplay (mis. Level_Entry) dulu, lalu jalankan menu ini.");
            return;
        }

        var existing = Object.FindObjectOfType<GameplayUISkin>();
        GameObject go = existing != null ? existing.gameObject : new GameObject("GP_UISkin");
        var skin = go.GetComponent<GameplayUISkin>();
        if (skin == null) skin = go.AddComponent<GameplayUISkin>();

        skin.titleFont = FindFont("orbitron", "rajdhani", "chakra", "oxanium", "audiowide");
        skin.bodyFont = FindFont("rajdhani", "chakra", "oxanium", "orbitron");

        EditorUtility.SetDirty(skin);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = go;
        Debug.Log("GP_UISkin terpasang di scene '" + scene.name + "'. Simpan (Ctrl+S) lalu Play. Font: "
                  + (skin.titleFont != null ? skin.titleFont.name : "bawaan"));
    }
}
#endif
