using UnityEngine;

public class PlayCustomizationApplier : MonoBehaviour
{
    [Header("Backgrounds (size 3)")]
    public GameObject[] backgroundOptions; // BG_0, BG_1, BG_2

    [Header("Player Skin")]
    public SpriteRenderer playerSpriteRenderer;
    public Sprite[] characterOptions; // size 3

    [Header("Collectible Prefabs (size 3)")]
    public GameObject[] itemPrefabs; // Item_0, Item_1, Item_2

    public static GameObject SelectedItemPrefab; // used by spawner later

    void Start()
    {
        ApplyBackground();
        ApplyCharacter();
        SelectItemPrefab();
    }

    void ApplyBackground()
    {
        if (backgroundOptions == null || backgroundOptions.Length == 0) return;

        for (int i = 0; i < backgroundOptions.Length; i++)
            if (backgroundOptions[i] != null)
                backgroundOptions[i].SetActive(i == SessionManager.BackgroundIndex);
    }

    void ApplyCharacter()
    {
        if (playerSpriteRenderer == null) return;
        if (characterOptions == null || characterOptions.Length == 0) return;

        int idx = Mathf.Clamp(SessionManager.CharacterIndex, 0, characterOptions.Length - 1);
        playerSpriteRenderer.sprite = characterOptions[idx];
    }

    void SelectItemPrefab()
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0) return;

        int idx = Mathf.Clamp(SessionManager.ItemIndex, 0, itemPrefabs.Length - 1);
        SelectedItemPrefab = itemPrefabs[idx];
    }
}
