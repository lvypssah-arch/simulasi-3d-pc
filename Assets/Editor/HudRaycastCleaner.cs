#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class HudRaycastCleaner
{
    [MenuItem("Tools/RakitPC/Matikan Raycast Target HUD")]
    static void Clean()
    {
        var hud = Object.FindObjectOfType<HUDController>();
        if (hud == null) { Debug.LogWarning("HUDController tidak ditemukan."); return; }
        int n = 0;
        foreach (var g in hud.GetComponentsInChildren<Graphic>(true))
        {
            if (g.GetComponentInParent<Selectable>() != null) continue;
            if (Under(g.transform, hud.pausePanel) || Under(g.transform, hud.resultPanel)) continue;
            if (!g.raycastTarget) continue;
            Undo.RecordObject(g, "Raycast Target");
            g.raycastTarget = false;
            n++;
        }
        Debug.Log("Raycast Target dimatikan pada " + n + " elemen HUD.");
    }

    static bool Under(Transform t, GameObject root)
    {
        return root != null && t.IsChildOf(root.transform);
    }
}
#endif