using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Penyesuaian tampilan HUD gameplay (Entry & Advance):
//  1. Sembunyikan "Langkah x / y" dan "Salah x". Skor dan Waktu tetap. Logika skor tidak disentuh.
//  2. Tombol MENU diubah menjadi ikon Pause (II). Tombol yang sama, onClick yang sama
//     (HUDController.OnMenuButton -> PauseMenu.Open), jadi tetap satu sistem Pause dengan ESC.
//  3. Panel KOMPONEN diturunkan sedikit agar ada jarak dari Top Bar.
public class GameplayTopBar : MonoBehaviour
{
    public float komponenDropY = 32f;       // seberapa jauh panel KOMPONEN diturunkan (satuan canvas)
    public float pauseButtonWidth = 72f;
    public float iconBarWidth = 7f, iconBarHeight = 26f, iconGap = 14f;

    IEnumerator Start()
    {
        // tunggu GameplayUISkin selesai membangun (ia menunggu 2 frame)
        for (int i = 0; i < 4; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.3f);

        var hud = FindObjectOfType<HUDController>(true);
        if (hud == null) yield break;

        HideStatus(hud);
        ConvertMenuToPauseIcon();
        LowerKomponenPanel();
    }

    // ------------------------------------------------------------ 1. Top Bar
    void HideStatus(HUDController hud)
    {
        if (hud.stepText != null) hud.stepText.gameObject.SetActive(false);
        if (hud.mistakeText != null) hud.mistakeText.gameObject.SetActive(false);

        // Kalau Top Bar tidak memakai Layout Group, rapikan Skor dan Waktu secara manual.
        if (hud.scoreText == null || hud.timeText == null) return;
        RectTransform bar = FindBar(hud.scoreText.transform);
        if (bar == null || bar.GetComponent<LayoutGroup>() != null) return;

        Place(hud.scoreText.rectTransform, -120f);
        Place(hud.timeText.rectTransform, 120f);
    }

    static void Place(RectTransform rt, float x)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, rt.sizeDelta.y > 1f ? rt.sizeDelta.y : 40f);
        rt.anchoredPosition = new Vector2(x, 0f);
    }

    // ------------------------------------------------------------ 3. Ikon Pause
    void ConvertMenuToPauseIcon()
    {
        Button menu = null;
        foreach (var b in FindObjectsOfType<Button>(true))
        {
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null && t.text.Trim().ToUpper() == "MENU") { menu = b; break; }
        }
        if (menu == null) { Debug.LogWarning("[GameplayTopBar] Tombol MENU tidak ditemukan."); return; }
        if (menu.transform.Find("PauseIcon") != null) return;

        // sembunyikan label teks, onClick dan efek hover/press dari skin tetap dipakai
        var label = menu.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.enabled = false;

        var rt = (RectTransform)menu.transform;
        var le = menu.GetComponent<LayoutElement>();
        if (le == null) le = menu.gameObject.AddComponent<LayoutElement>();
        le.minWidth = pauseButtonWidth; le.preferredWidth = pauseButtonWidth; le.flexibleWidth = 0f;
        if (Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x))
            rt.sizeDelta = new Vector2(pauseButtonWidth, rt.sizeDelta.y);

        var icon = new GameObject("PauseIcon", typeof(RectTransform), typeof(LayoutElement));
        icon.transform.SetParent(menu.transform, false);
        icon.GetComponent<LayoutElement>().ignoreLayout = true;
        var irt = (RectTransform)icon.transform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = new Vector2(0f, 2f);
        irt.sizeDelta = new Vector2(iconGap + iconBarWidth * 2f, iconBarHeight);

        Bar(irt, -(iconGap + iconBarWidth) * 0.5f);
        Bar(irt, (iconGap + iconBarWidth) * 0.5f);
    }

    void Bar(RectTransform parent, float x)
    {
        var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(iconBarWidth, iconBarHeight);
        var im = go.GetComponent<Image>();
        im.color = Color.white;
        im.raycastTarget = false;     // klik tetap diterima tombol
    }

    // ------------------------------------------------------------ 2. Panel KOMPONEN
    void LowerKomponenPanel()
    {
        TMP_Text head = null;
        foreach (var t in FindObjectsOfType<TMP_Text>(true))
            if (t.text.Trim().ToUpper() == "KOMPONEN") { head = t; break; }
        if (head == null) { Debug.LogWarning("[GameplayTopBar] Teks KOMPONEN tidak ditemukan."); return; }

        RectTransform panel = FindPanel(head.transform);
        if (panel == null) { Debug.LogWarning("[GameplayTopBar] Panel KOMPONEN tidak ditemukan."); return; }
        if (panel.parent != null && panel.parent.GetComponent<LayoutGroup>() != null)
        {
            Debug.LogWarning("[GameplayTopBar] Panel KOMPONEN diatur Layout Group, posisi tidak diubah.");
            return;
        }

        if (Mathf.Approximately(panel.anchorMin.y, panel.anchorMax.y))
            panel.anchoredPosition += new Vector2(0f, -komponenDropY);          // posisi tetap: geser turun
        else
            panel.offsetMax = new Vector2(panel.offsetMax.x, panel.offsetMax.y - komponenDropY); // stretch: tepi atas turun
    }

    // ------------------------------------------------------------ helper
    static RectTransform FindBar(Transform start)
    {
        Transform t = start.parent;
        for (int i = 0; i < 4 && t != null; i++, t = t.parent)
        {
            if (t.GetComponent<Canvas>() != null) break;
            if (t.GetComponent<Image>() != null) return t as RectTransform;
        }
        return null;
    }

    static RectTransform FindPanel(Transform start)
    {
        var cv = start.GetComponentInParent<Canvas>();
        if (cv == null) return null;
        var root = cv.rootCanvas.GetComponent<RectTransform>();
        Transform t = start.parent;
        for (int i = 0; i < 4 && t != null; i++, t = t.parent)
        {
            var rt = t as RectTransform;
            if (rt == null || t.GetComponent<Canvas>() != null) break;
            if (rt.rect.width <= root.rect.width * 0.6f && rt.rect.height <= root.rect.height * 0.95f
                && t.GetComponent<Image>() != null) return rt;
        }
        return null;
    }
}