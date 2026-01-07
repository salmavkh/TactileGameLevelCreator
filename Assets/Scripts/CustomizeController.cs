using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CustomizeController : MonoBehaviour
{
    [Header("Preview UI Images")]
    public Image backgroundPreview;
    public Image characterPreview;
    public Image itemPreview;

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
        // Load any existing selection (default 0 if new run)
        RefreshAll();
    }

    int Wrap(int i, int len) => (i % len + len) % len;

    void RefreshAll()
    {
        if (backgroundOptions != null && backgroundOptions.Length > 0 && backgroundPreview != null)
            backgroundPreview.sprite = backgroundOptions[Wrap(SessionManager.BackgroundIndex, backgroundOptions.Length)];

        if (characterOptions != null && characterOptions.Length > 0 && characterPreview != null)
            characterPreview.sprite = characterOptions[Wrap(SessionManager.CharacterIndex, characterOptions.Length)];

        if (itemOptions != null && itemOptions.Length > 0 && itemPreview != null)
            itemPreview.sprite = itemOptions[Wrap(SessionManager.ItemIndex, itemOptions.Length)];

        if (backgroundLabel != null) backgroundLabel.text = $"Background {SessionManager.BackgroundIndex + 1}";
        if (characterLabel != null) characterLabel.text = $"Character {SessionManager.CharacterIndex + 1}";
        if (itemLabel != null) itemLabel.text = $"Item {SessionManager.ItemIndex + 1}";
    }

    // --- Background arrows ---
    public void BackgroundLeft()
    {
        SessionManager.BackgroundIndex = Wrap(SessionManager.BackgroundIndex - 1, backgroundOptions.Length);
        RefreshAll();
    }

    public void BackgroundRight()
    {
        SessionManager.BackgroundIndex = Wrap(SessionManager.BackgroundIndex + 1, backgroundOptions.Length);
        RefreshAll();
    }

    // --- Character arrows ---
    public void CharacterLeft()
    {
        SessionManager.CharacterIndex = Wrap(SessionManager.CharacterIndex - 1, characterOptions.Length);
        RefreshAll();
    }

    public void CharacterRight()
    {
        SessionManager.CharacterIndex = Wrap(SessionManager.CharacterIndex + 1, characterOptions.Length);
        RefreshAll();
    }

    // --- Item arrows ---
    public void ItemLeft()
    {
        SessionManager.ItemIndex = Wrap(SessionManager.ItemIndex - 1, itemOptions.Length);
        RefreshAll();
    }

    public void ItemRight()
    {
        SessionManager.ItemIndex = Wrap(SessionManager.ItemIndex + 1, itemOptions.Length);
        RefreshAll();
    }

    // --- Navigation buttons ---
    public void BackToCapture()
    {
        SceneManager.LoadScene("Capture");
    }

    public void NextToPlay()
    {
        SceneManager.LoadScene("Play");
    }
}
