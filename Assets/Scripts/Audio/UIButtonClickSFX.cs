using UnityEngine;
using UnityEngine.UI;

public class UIButtonClickSFX : MonoBehaviour
{
    void Start()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            b.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayUI(SFX.ButtonClick);
            });
        }
    }
}
