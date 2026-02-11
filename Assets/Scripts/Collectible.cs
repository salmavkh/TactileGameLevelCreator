using UnityEngine;

public class Collectible : MonoBehaviour
{
    public static int Collected = 0;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        Collected++;

        // Trigger Touch animation on the player
        PlayerController2D player = other.GetComponent<PlayerController2D>();
        if (player != null)
            player.PlayTouch();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayPlayer(SFX.Collect);

        Destroy(gameObject);
    }
}
