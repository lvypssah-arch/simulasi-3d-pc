using UnityEngine;
using UnityEngine.UI;
using TMPro;
 
public class PartCard : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text specText;
    public TMP_Text countText;
    public Button button;
    public CanvasGroup group;
 
    public void Setup(PartData d, AssemblyManager mgr)
    {
        if (d.icon != null) icon.sprite = d.icon;
        nameText.text = d.displayName;
        specText.text = d.specs.Split('\n')[0];
        SetRemaining(d.copies);
        button.onClick.AddListener(() => mgr.PickPart(d));
    }
 
    public void SetRemaining(int n)
    {
        countText.text = n > 1 ? "x" + n : (n == 1 ? "" : "OK");
        button.interactable = n > 0;
        group.alpha = n > 0 ? 1f : 0.45f;
    }
}
