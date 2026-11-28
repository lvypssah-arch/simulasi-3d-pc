using UnityEngine;
using UnityEngine.SceneManagement;

// Membuat Pause Menu, Result Screen, Panduan, dan penyesuaian Top Bar otomatis di setiap scene level
// (scene yang punya HUDController). Tidak perlu menambah apa pun di scene.
public static class GameplayUIBootstrap
{
    static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => Spawn(); }
        Spawn();
    }

    static void Spawn()
    {
        if (Object.FindObjectOfType<HUDController>() == null) return;
        if (Object.FindObjectOfType<PauseMenu>() != null) return;
        UIKit.UseGameplayFonts();
        var go = new GameObject("GP_UIExtras");
        go.AddComponent<ResultScreen>();
        go.AddComponent<PauseMenu>();
        go.AddComponent<GuidePanel>();
        go.AddComponent<GameplayTopBar>();
    }
}