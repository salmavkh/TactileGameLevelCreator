using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CustomizeController : MonoBehaviour
{
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
    }

    public void ItemRight()
    {
        SessionManager.ItemIndex =
            Wrap(SessionManager.ItemIndex + 1, itemOptions.Length);
        RefreshAll();
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
}
