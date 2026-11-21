using UnityEngine;
using UnityEngine.EventSystems;
 
public static class Util
{
    // true jika pointer (mouse / jari) sedang berada di atas UI
    public static bool PointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }
 
    public static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }
}
