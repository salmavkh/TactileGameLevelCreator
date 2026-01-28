using UnityEngine;
using UnityEngine.UI;

public class LivesManager : MonoBehaviour
{
    public static LivesManager Instance;
    public PlayEndMenu endMenu;

    [Header("Lives")]
    public int maxLives = 3;
    public int lives;

    [Header("UI")]
    public Image[] heartImages;      // size 3
    public GameObject gameOverText;  // "YOU DIED" object

    public Sprite heartFilled;
    public Sprite heartEmpty;

    [Header("Hit settings")]
    public float hitCooldown = 0.4f;
    float lastHitTime = -999f;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        lives = maxLives;
        RefreshUI();
        if (gameOverText != null) gameOverText.SetActive(false);
    }

    public void TakeHit()
    {
        if (Time.time - lastHitTime < hitCooldown) return;
        lastHitTime = Time.time;

        if (lives <= 0) return;

        lives--;
        RefreshUI();

        if (lives <= 0)
            Die();
    }

    void RefreshUI()
    {
        for (int i = 0; i < heartImages.Length; i++)
        {
            if (heartImages[i] == null) continue;
            heartImages[i].sprite = (i < lives) ? heartFilled : heartEmpty;
            heartImages[i].enabled = true;
        }
    }

    void Die()
    {
        // if (gameOverText != null) gameOverText.SetActive(true);

        // // For now: freeze the game
        // Time.timeScale = 0f;
        // Debug.Log("GAME OVER: YOU DIED");

        if (gameOverText != null) gameOverText.SetActive(false); // you can stop using it

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUI(SFX.LoseHearts);

        if (endMenu != null) endMenu.ShowGameOver();
        else Time.timeScale = 0f;

    }
}
