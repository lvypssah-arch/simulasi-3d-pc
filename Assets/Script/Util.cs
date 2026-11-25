using System.Collections.Generic;
using System.Text;
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

    // Daftar objek UI yang ada di bawah pointer (untuk mencari penyebab klik terblokir)
    public static string UnderPointerNames()
    {
        if (EventSystem.current == null) return "(tidak ada EventSystem)";
        var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        var sb = new StringBuilder();
        foreach (var r in results) sb.Append(r.gameObject.name).Append(" | ");
        return sb.Length == 0 ? "(tidak ada)" : sb.ToString();
    }

    public static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }
}