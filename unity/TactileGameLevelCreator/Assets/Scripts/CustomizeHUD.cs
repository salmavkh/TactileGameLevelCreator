using UnityEngine;
using TMPro;

public class CustomizeHUD : MonoBehaviour
{
    public TMP_Text scoreText;
    public TMP_Text timerText;

    void Start()
    {
        Refresh();
    }

    // Call this after user changes sliders/inputs (items, timer, etc.)
    public void Refresh()
    {
        // Score preview: always starts at 0 in Customize
        int target = Mathf.Max(0, SessionManager.TargetCollectCount);
        if (scoreText != null)
            scoreText.text = $"0/{target}";

        // Timer preview: based on user input stored in SessionManager
        float secs = Mathf.Max(0f, SessionManager.TimerSeconds);
        if (timerText != null)
            timerText.text = FormatMMSS(secs);
    }

    static string FormatMMSS(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        int m = s / 60;
        int r = s % 60;
        return $"{m:00}:{r:00}";
    }
}
