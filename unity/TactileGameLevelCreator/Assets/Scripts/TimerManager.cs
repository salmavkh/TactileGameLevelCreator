using UnityEngine;
using TMPro;

public class TimerManager : MonoBehaviour
{
    public float seconds = 60f;
    public TMP_Text timerText;

    bool ended = false;

    void Start()
    {
        // read from SessionManager if you have it
        seconds = SessionManager.TimerSeconds;
        UpdateUI();
    }

    void Update()
    {
        if (ended) return;
        if (Time.timeScale == 0f) return;

        seconds -= Time.deltaTime;
        if (seconds <= 0f)
        {
            seconds = 0f;
            ended = true;
            UpdateUI();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUI(SFX.LoseTimeout);

            // Timer game over
            if (LivesManager.Instance != null)
            {
                // force game over text + freeze
                LivesManager.Instance.SendMessage("Die", SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                Debug.Log("TIME UP");
                Time.timeScale = 0f;
            }
            return;
        }

        UpdateUI();
    }

    void UpdateUI()
    {
        if (timerText == null) return;
        int s = Mathf.CeilToInt(seconds);
        int m = s / 60;
        int r = s % 60;
        timerText.text = $"{m:00}:{r:00}";
    }
}
