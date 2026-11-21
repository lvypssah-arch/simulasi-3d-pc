using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
 
public class MainMenuController : MonoBehaviour
{
    public GameObject mainPanel, levelPanel, settingsPanel;
    public Button advanceButton;
    public TMP_Text advanceLockText, entryBestText, advanceBestText;
    public Slider volumeSlider;
    public bool lockAdvance = true;            // false = Advance langsung terbuka
    public string entryScene = "Level_Entry";
    public string advanceScene = "Level_Advance";
 
    void Start()
    {
        Time.timeScale = 1f;
        float vol = PlayerPrefs.GetFloat("volume", 0.8f);
        AudioListener.volume = vol;
        if (volumeSlider != null)
        {
            volumeSlider.value = vol;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        bool unlocked = !lockAdvance || PlayerPrefs.GetInt("unlock_advance", 0) == 1;
        advanceButton.interactable = unlocked;
        advanceLockText.gameObject.SetActive(!unlocked);
        entryBestText.text = "Skor terbaik: " + PlayerPrefs.GetInt("best_Entry", 0);
        advanceBestText.text = "Skor terbaik: " + PlayerPrefs.GetInt("best_Advance", 0);
        ShowMain();
    }
 
    public void ShowMain()     { Show(true, false, false); }
    public void ShowLevels()   { Show(false, true, false); }
    public void ShowSettings() { Show(false, false, true); }
 
    void Show(bool main, bool level, bool settings)
    {
        mainPanel.SetActive(main);
        levelPanel.SetActive(level);
        settingsPanel.SetActive(settings);
    }
 
    public void PlayEntry()    { SceneManager.LoadScene(entryScene); }
    public void PlayAdvance()  { SceneManager.LoadScene(advanceScene); }
 
    public void SetVolume(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat("volume", v);
    }
 
    public void ResetProgress()
    {
        PlayerPrefs.DeleteAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
 
    public void QuitGame() { Application.Quit(); }
}