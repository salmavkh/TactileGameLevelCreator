using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [Header("Scene Names")]
    public string sceneToLoad;

    public void LoadScene()
    {
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogError("Scene name not set in SceneLoader.");
            return;
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}
