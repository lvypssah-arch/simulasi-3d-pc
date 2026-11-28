using UnityEngine;

// Satu-satunya tempat daftar level. Level Select membaca daftar ini.
// Nama scene HARUS sama persis dengan yang ada di File > Build Settings.
public static class GameLevels
{
    public class Info
    {
        public int number;
        public string title, description, scene, levelId;
        public Info(int n, string t, string d, string s, string id) { number = n; title = t; description = d; scene = s; levelId = id; }
    }

    // true = Level Advance terkunci sampai Level Entry selesai (memakai PlayerPrefs "unlock_advance"
    // yang sudah disimpan AssemblyManager). false = semua level langsung bisa dipilih.
    public const bool UseProgressionLock = false;

    public static readonly Info[] All =
    {
        new Info(1, "LEVEL ENTRY",   "Rakit PC langkah demi langkah dengan panduan hologram.", "Level_Entry",  "Entry"),
        new Info(2, "LEVEL ADVANCE", "Rakit PC tanpa hologram panduan. Petunjuk tersedia lewat tombol HINT.", "LevelAdvance", "Advance"),
    };

    public static bool IsLocked(Info i)
    {
        if (UseProgressionLock && i.levelId == "Advance") return PlayerPrefs.GetInt("unlock_advance", 0) != 1;
        return false;
    }
}
