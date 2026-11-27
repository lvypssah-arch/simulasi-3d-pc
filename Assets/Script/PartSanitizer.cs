using System.Collections.Generic;
using UnityEngine;

// Mematikan Camera dan AudioListener bawaan model (FBX) pada objek tiruan atau yang dipegang,
// supaya tidak menimpa tampilan kamera utama.
public static class PartSanitizer
{
    static readonly HashSet<string> warned = new HashSet<string>();

    public static int Strip(GameObject go, bool disableLights)
    {
        if (go == null) return 0;
        int n = 0;
        foreach (var c in go.GetComponentsInChildren<Camera>(true))
        {
            if (c.enabled) { c.enabled = false; n++; }
        }
        foreach (var a in go.GetComponentsInChildren<AudioListener>(true)) a.enabled = false;
        if (disableLights)
            foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;

        if (n > 0 && warned.Add(go.name))
            Debug.LogWarning("[CEK] " + go.name + " memuat " + n + " Camera bawaan model dan sudah dimatikan. "
                             + "Jalankan menu 16 untuk mematikan Import Cameras di FBX-nya.");
        return n;
    }
}