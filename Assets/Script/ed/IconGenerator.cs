#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
 
public static class IconGenerator
{
    [MenuItem("Tools/RakitPC/Generate Part Icons")]
    static void Generate()
    {
        string outDir = "Assets/Icons";
        Directory.CreateDirectory(outDir);
 
        foreach (string guid in AssetDatabase.FindAssets("t:PartData"))
        {
            string dataPath = AssetDatabase.GUIDToAssetPath(guid);
            PartData data = AssetDatabase.LoadAssetAtPath<PartData>(dataPath);
            if (data == null || data.prefab == null) continue;
 
            Texture2D tex = null;
            for (int i = 0; i < 100 && tex == null; i++)
            {
                tex = AssetPreview.GetAssetPreview(data.prefab);
                if (tex == null) System.Threading.Thread.Sleep(50);
            }
            if (tex == null) { Debug.LogWarning("Ikon gagal: " + data.name); continue; }
 
            // salin ke tekstur yang bisa dibaca
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height);
            Graphics.Blit(tex, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
 
            string path = outDir + "/Icon_" + data.type + ".png";
            File.WriteAllBytes(path, readable.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
 
            data.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Selesai membuat ikon di " + outDir);
    }
}
#endif
