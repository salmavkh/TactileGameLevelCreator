using UnityEngine;
using TMPro;


public class CollectUI : MonoBehaviour
{
    public TMP_Text counterText; // or TMP later

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

        if (target > 0 && got >= target)
        {
            counterText.text = $"WIN! {got}/{target}";
            // later: show win panel, stop player, etc.
        }
    }
}
