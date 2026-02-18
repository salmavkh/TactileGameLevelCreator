using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SliderValueText : MonoBehaviour
{
    public Slider slider;
    public TMP_Text valueText;
    public CustomizeHUD hud;

    public string suffix = "s";   // e.g. "s" or "min"

    void Start()
    {
        UpdateText(slider.value);
        slider.onValueChanged.AddListener(UpdateText);
    }

    void UpdateText(float value)
    {
        valueText.text = Mathf.RoundToInt(value).ToString() + suffix;
    }

    // Hook this to the Slider OnValueChanged(float)
    public void OnTimerSliderChanged(float v)
    {
        SessionManager.TimerSeconds = v;
        if (hud != null)
            hud.Refresh();
    }
}
