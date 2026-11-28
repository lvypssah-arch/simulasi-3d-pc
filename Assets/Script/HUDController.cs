using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
 
public class HUDController : MonoBehaviour
{
    public enum Tone { Info, Success, Error }
 
    [Header("Bar atas")]
    public TMP_Text levelText;
    public TMP_Text stepText;
    public TMP_Text scoreText;
    public TMP_Text timeText;
    public TMP_Text mistakeText;
 
    [Header("Toast (pesan singkat)")]
    public CanvasGroup toastGroup;
    public TMP_Text toastText;
    public Image toastBg;
 
    [Header("Kilatan layar")]
    public Image flash;
 
    [Header("Info komponen yang dipegang")]
    public GameObject holdPanel;
    public TMP_Text holdTitle;
    public TMP_Text holdBody;
 
    [Header("Panel")]
    public GameObject pausePanel;
    public GameObject resultPanel;
    public TMP_Text resultTitle;
    public TMP_Text resultBody;
    public Image[] stars;
    public GameObject nextButton;
 
    public Color infoColor = new Color(0.10f, 0.45f, 0.85f, 0.92f);
    public Color successColor = new Color(0.13f, 0.70f, 0.40f, 0.92f);
    public Color errorColor = new Color(0.85f, 0.15f, 0.20f, 0.95f);
 
    [Header("Posisi toast (pesan singkat)")]
    public bool moveToastToBottom = true;
    public Vector2 toastBottomOffset = new Vector2(0f, 250f);   // dari tepi bawah layar, di atas tombol PARTS/HINT/RESET

    Coroutine toastRoutine, flashRoutine;
 
    public void Init(string title, int totalSteps, int maxMistakes)
    {
        if (moveToastToBottom && toastGroup != null)
        {
            var tr = toastGroup.transform as RectTransform;
            if (tr != null)
            {
                tr.anchorMin = new Vector2(0.5f, 0f); tr.anchorMax = new Vector2(0.5f, 0f);
                tr.anchoredPosition = toastBottomOffset;
            }
        }
        levelText.text = title;
        SetProgress(0, totalSteps);
        SetScore(0);
        SetMistakes(0, maxMistakes);
        holdPanel.SetActive(false);
        pausePanel.SetActive(false);
        resultPanel.SetActive(false);
        toastGroup.alpha = 0f;
        flash.color = new Color(1f, 0.1f, 0.1f, 0f);
    }
 
    public void SetProgress(int done, int total) { stepText.text = "Langkah " + done + " / " + total; }
    public void SetScore(int s) { scoreText.text = "Skor " + s; }
    public void SetTime(float seconds) { timeText.text = Util.FormatTime(seconds); }
 
    public void SetMistakes(int m, int max)
    {
        mistakeText.text = max > 0 ? "Salah " + m + " / " + max : "Salah " + m;
    }
 
    public void Toast(string msg, Tone tone = Tone.Info, float seconds = 2.8f)
    {
        toastText.text = msg;
        toastBg.color = tone == Tone.Error ? errorColor
                      : tone == Tone.Success ? successColor : infoColor;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(seconds));
    }
 
    IEnumerator ToastRoutine(float seconds)
    {
        toastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(seconds);
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            toastGroup.alpha = 1f - t / 0.4f;
            yield return null;
        }
        toastGroup.alpha = 0f;
    }
 
    public void FlashRed()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }
 
    IEnumerator FlashRoutine()
    {
        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.unscaledDeltaTime;
            flash.color = new Color(1f, 0.1f, 0.1f, Mathf.Lerp(0.35f, 0f, t / 0.45f));
            yield return null;
        }
        flash.color = new Color(1f, 0.1f, 0.1f, 0f);
    }
 
    public void ShowHoldInfo(PartData d)
    {
        holdPanel.SetActive(true);
        holdTitle.text = d.displayName;
        holdBody.text = d.description + "\n<color=#9EE7FF>" + d.specs + "</color>";
    }
 
    public void ClearHoldInfo() { holdPanel.SetActive(false); }
 
    public void ShowResult(bool win, int score, int mistakes, float time,
                           int starCount, bool hasNext, string reason)
    {
        holdPanel.SetActive(false);
        // Result Screen baru (ResultScreen.cs). Panel lama di scene tidak lagi dipakai.
        if (ResultScreen.I != null)
        {
            ResultScreen.I.Show(this, win, levelText != null ? levelText.text : "", score, mistakes, time,
                                starCount, hasNext, reason);
            return;
        }
        resultPanel.SetActive(true);
        resultTitle.text = win ? "PC MENYALA! SELAMAT" : "LEVEL GAGAL";
        resultBody.text = (win ? "" : reason + "\n")
            + "Skor: " + score + "\nKesalahan: " + mistakes + "\nWaktu: " + Util.FormatTime(time);
        for (int i = 0; i < stars.Length; i++)
            stars[i].color = (win && i < starCount)
                ? new Color(1f, 0.8f, 0.1f) : new Color(0.3f, 0.3f, 0.35f);
        nextButton.SetActive(win && hasNext);
    }
 
    // ---- dipanggil dari OnClick tombol UI ----
    public void OnMenuButton()
    {
        if (PauseMenu.I != null) { PauseMenu.I.Open(); return; }
        pausePanel.SetActive(true); Time.timeScale = 0f;
    }
    public void OnResumeButton()
    {
        if (PauseMenu.I != null) { PauseMenu.I.Close(); return; }
        pausePanel.SetActive(false); Time.timeScale = 1f;
    }
    public void OnRestartButton()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    public void OnMainMenuButton() { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }
    public void OnNextButton()     { Time.timeScale = 1f; AssemblyManager.I.LoadNext(); }
    public void OnCancelButton()   { AssemblyManager.I.CancelHold(); }
}