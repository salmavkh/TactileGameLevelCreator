using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class CustomizeController : MonoBehaviour
{

    [Header("Gameplay Inputs")]
    public TMP_InputField itemsInput;
    public TMP_InputField obstaclesInput;
    public Slider timerSlider;

    [Header("Preview UI Images (Canvas)")]
    public Image backgroundPreview;
    public Image characterPreview;
    public Image itemPreview;

    [Header("World Background (behind platform)")]
    public SpriteRenderer worldBackgroundRenderer;

    [Header("Optional Labels")]
    public Text backgroundLabel;
    public Text characterLabel;
    public Text itemLabel;

    [Header("Options (size must be 3 each)")]
    public Sprite[] backgroundOptions; // 3
    public Sprite[] characterOptions;  // 3
    public Sprite[] itemOptions;       // 3

    public CustomizePreviewSpawner previewSpawner;
    public CustomizeHUD hud;
    public int MaxObstacles = 999; // will be overwritten by preview spawner


    void Start()
    {
        RefreshAll();
    }

    int Wrap(int i, int len)
    {
        if (len <= 0) return 0;
        return (i % len + len) % len;
    }

    void RefreshAll()
    {
        // ---- Background ----
        if (backgroundOptions != null && backgroundOptions.Length > 0)
        {
            Sprite bg = backgroundOptions[Wrap(SessionManager.BackgroundIndex, backgroundOptions.Length)];

            // UI preview (thumbnail / selector)
            if (backgroundPreview != null)
                backgroundPreview.sprite = bg;

            // World background (behind platform)
            if (worldBackgroundRenderer != null)
                worldBackgroundRenderer.sprite = bg;
        }

        // ---- Character ----
        if (characterOptions != null && characterOptions.Length > 0 && characterPreview != null)
            characterPreview.sprite =
                characterOptions[Wrap(SessionManager.CharacterIndex, characterOptions.Length)];

        // ---- Item ----
        if (itemOptions != null && itemOptions.Length > 0 && itemPreview != null)
            itemPreview.sprite =
                itemOptions[Wrap(SessionManager.ItemIndex, itemOptions.Length)];

        // ---- Labels ----
        if (backgroundLabel != null)
            backgroundLabel.text = $"Background {SessionManager.BackgroundIndex + 1}";

        if (characterLabel != null)
            characterLabel.text = $"Character {SessionManager.CharacterIndex + 1}";

        if (itemLabel != null)
            itemLabel.text = $"Item {SessionManager.ItemIndex + 1}";
    }

    // -------- Background arrows --------
    public void BackgroundLeft()
    {
        SessionManager.BackgroundIndex =
            Wrap(SessionManager.BackgroundIndex - 1, backgroundOptions.Length);
        RefreshAll();
    }

    public void BackgroundRight()
    {
        SessionManager.BackgroundIndex =
            Wrap(SessionManager.BackgroundIndex + 1, backgroundOptions.Length);
        RefreshAll();
    }

    // -------- Character arrows --------
    public void CharacterLeft()
    {
        SessionManager.CharacterIndex =
            Wrap(SessionManager.CharacterIndex - 1, characterOptions.Length);
        RefreshAll();
    }

    public void CharacterRight()
    {
        SessionManager.CharacterIndex =
            Wrap(SessionManager.CharacterIndex + 1, characterOptions.Length);
        RefreshAll();
    }

    // -------- Item arrows --------
    public void ItemLeft()
    {
        SessionManager.ItemIndex =
            Wrap(SessionManager.ItemIndex - 1, itemOptions.Length);
        RefreshAll();
        if (previewSpawner != null) previewSpawner.RegeneratePreview();
    }

    public void ItemRight()
    {
        SessionManager.ItemIndex =
            Wrap(SessionManager.ItemIndex + 1, itemOptions.Length);
        RefreshAll();
        if (previewSpawner != null) previewSpawner.RegeneratePreview();
    }

    // -------- Navigation --------
    public void BackToCapture()
    {
        SceneManager.LoadScene("Capture");
    }

    public void NextToPlay()
    {
        SceneManager.LoadScene("Play");
    }

    public void ApplyGameplaySettings()
    {
        // Items: blank -> 0, and DISPLAY 0
        if (itemsInput != null)
        {
            string t = (itemsInput.text ?? "").Trim();
            int items = 0;

            if (!string.IsNullOrEmpty(t))
                int.TryParse(t, out items);

            items = Mathf.Max(0, items);
            SessionManager.NumItems = items;

            // force UI to show value
            itemsInput.text = items.ToString();
        }

        // Obstacles: blank -> 0, clamp to MaxObstacles, and DISPLAY value
        if (obstaclesInput != null)
        {
            string t = (obstaclesInput.text ?? "").Trim();
            int obs = 0;

            if (!string.IsNullOrEmpty(t))
                int.TryParse(t, out obs);

            obs = Mathf.Clamp(obs, 0, MaxObstacles);
            SessionManager.NumObstacles = obs;

            // force UI to show value
            obstaclesInput.text = obs.ToString();
        }

        // Timer
        if (timerSlider != null)
            SessionManager.TimerSeconds = Mathf.Max(5f, timerSlider.value);

        // Hearts (fixed for now)
        SessionManager.MaxHearts = 3;

        // New seed so preview + play match
        SessionManager.RandomSeed = Random.Range(int.MinValue, int.MaxValue);

        Debug.Log(
            $"Customize Apply → Items:{SessionManager.NumItems}, " +
            $"Obstacles:{SessionManager.NumObstacles}, " +
            $"Timer:{SessionManager.TimerSeconds}, " +
            $"Seed:{SessionManager.RandomSeed}"
        );

        if (previewSpawner != null)
            previewSpawner.RegeneratePreview();

        if (obstaclesInput != null)
            obstaclesInput.text = SessionManager.NumObstacles.ToString();

    }

    public void OnItemsChanged(int n)
    {
        SessionManager.NumItems = n;
        SessionManager.TargetCollectCount = n; // so preview shows 0/n
    }


}
