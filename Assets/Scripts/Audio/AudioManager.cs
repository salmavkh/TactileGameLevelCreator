using UnityEngine;

public enum MusicTrack { Menu, Play }
public enum SFX { ButtonClick, Footstep, Jump, Land, Hit, Collect, Win, LoseHearts, LoseTimeout }

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Sources")]
    public AudioSource musicSource;     // looped music
    public AudioSource uiSfxSource;     // button clicks etc (2D)
    public AudioSource playerSfxSource; // footsteps/jump/land (2D)

    [Header("Clips")]
    public AudioClip menuMusic;
    public AudioClip playMusic;

    public AudioClip buttonClick;
    public AudioClip footstep;
    public AudioClip jump;
    public AudioClip land;
    public AudioClip hit;
    public AudioClip collect;
    public AudioClip win;
    public AudioClip loseHearts;
    public AudioClip loseTimeout;

    [Header("Volumes")]
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Range(0f, 1f)] public float uiSfxVolume = 0.8f;
    [Range(0f, 1f)] public float playerSfxVolume = 0.8f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicSource != null)
        {
            musicSource.loop = true;
            musicSource.volume = musicVolume;
        }
        if (uiSfxSource != null) uiSfxSource.volume = uiSfxVolume;
        if (playerSfxSource != null) playerSfxSource.volume = playerSfxVolume;
    }

    public void PlayMusic(MusicTrack track)
    {
        if (musicSource == null) return;

        AudioClip clip = track == MusicTrack.Menu ? menuMusic : playMusic;
        if (clip == null) return;

        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void PlayUI(SFX sfx)
    {
        if (uiSfxSource == null) return;
        AudioClip clip = GetClip(sfx);
        if (clip == null) return;
        uiSfxSource.PlayOneShot(clip);
    }

    public void PlayPlayer(SFX sfx)
    {
        if (playerSfxSource == null) return;
        AudioClip clip = GetClip(sfx);
        if (clip == null) return;
        playerSfxSource.PlayOneShot(clip);
    }

    AudioClip GetClip(SFX sfx)
    {
        switch (sfx)
        {
            case SFX.ButtonClick: return buttonClick;
            case SFX.Footstep: return footstep;
            case SFX.Jump: return jump;
            case SFX.Land: return land;
            case SFX.Hit: return hit;
            case SFX.Collect: return collect;
            case SFX.Win: return win;
            case SFX.LoseHearts: return loseHearts;
            case SFX.LoseTimeout: return loseTimeout;
            default: return null;
        }
    }
}
