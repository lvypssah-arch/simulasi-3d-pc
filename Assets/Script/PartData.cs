using UnityEngine;
 
[CreateAssetMenu(fileName = "PD_New", menuName = "RakitPC/Part Data")]
public class PartData : ScriptableObject
{
    public PartType type;
    public string displayName;
    [TextArea(2, 4)] public string description;
    [TextArea(2, 6)] public string specs;   // baris 1 tampil di kartu, semua baris di info
    public Sprite icon;
    public GameObject prefab;
    public int copies = 1;                  // RAM = 2
}
