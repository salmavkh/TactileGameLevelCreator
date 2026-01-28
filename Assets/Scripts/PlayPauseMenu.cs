using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayPauseMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pauseOverlay; // your PauseOverlay panel

    [Header("Scene Names")]
    [SerializeField] private string customizeSceneName = "Customize";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool isPaused = false;

    private void Start()
    {
        // Ensure Play never starts paused
        Time.timeScale = 1f;

        if (pauseOverlay != null)
            pauseOverlay.SetActive(false);
    }

    private void Update()
    {
        // Press Esc to toggle pause (PC)
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    public void TogglePause()
    {
        if (isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (pauseOverlay != null)
            pauseOverlay.SetActive(true);
    }

    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseOverlay != null)
            pauseOverlay.SetActive(false);
    }

    public void BackToEditor()
    {
        // IMPORTANT: restore time scale before leaving
        Time.timeScale = 1f;
        isPaused = false;

        SceneManager.LoadScene(customizeSceneName);
    }

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;
        isPaused = false;

        // Optional: reset session values if you want a fresh run next time
        // SessionManager.PlannedItemPositions.Clear();
        // SessionManager.PlannedObstaclePositions.Clear();

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
