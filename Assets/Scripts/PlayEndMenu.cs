using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class PlayEndMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject endOverlay;   // Panel
    [SerializeField] private TMP_Text titleText;      // "YOU WIN" / "GAME OVER"

    [Header("Scene Names")]
    [SerializeField] private string playSceneName = "Play";
    [SerializeField] private string customizeSceneName = "Customize";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool shown = false;

    private void Start()
    {
        Time.timeScale = 1f;
        if (endOverlay != null) endOverlay.SetActive(false);
    }

    public void ShowWin()
    {
        Show("YOU WIN!");
    }

    public void ShowGameOver()
    {
        Show("GAME OVER");
    }

    private void Show(string title)
    {
        if (shown) return;           // prevent double-trigger
        shown = true;

        if (titleText != null) titleText.text = title;
        if (endOverlay != null) endOverlay.SetActive(true);

        // Freeze the game on BOTH win and lose (recommended for menu usability)
        Time.timeScale = 0f;
    }

    public void PlayAgain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(playSceneName);
    }

    public void BackToEditor()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(customizeSceneName);
    }

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
