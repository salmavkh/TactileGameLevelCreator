using UnityEngine;
using TMPro;


public class CollectUI : MonoBehaviour
{
    public TMP_Text counterText; // or TMP later
    public PlayEndMenu endMenu;
    bool winTriggered = false;


    void Start()
    {
        Collectible.Collected = 0; // reset each run
        Refresh();
    }

    void Update()
    {
        Refresh();
    }

    void Refresh()
    {
        int target = Mathf.Max(0, SessionManager.TargetCollectCount);
        int got = Collectible.Collected;
        if (counterText != null) counterText.text = $"{got}/{target}";

        // if (target > 0 && got >= target)
        // {
        //     counterText.text = $"WIN! {got}/{target}";
        //     // later: show win panel, stop player, etc.
        // }

        if (!winTriggered && target > 0 && got >= target)
        {
            winTriggered = true;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUI(SFX.Win);

            if (endMenu != null) endMenu.ShowWin();
        }

    }
}
