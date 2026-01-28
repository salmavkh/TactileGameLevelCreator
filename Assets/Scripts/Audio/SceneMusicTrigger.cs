using UnityEngine;

public class SceneMusicTrigger : MonoBehaviour
{
    public MusicTrack track = MusicTrack.Menu;

    void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMusic(track);
    }
}
