using UnityEngine;

public class PlayerObstacleBounce : MonoBehaviour
{
    [Header("Knockback")]
    public float knockbackSpeedX = 10f;   // sideways speed during knockback
    public float knockbackLiftY = 2f;    // small upward lift
    public float knockbackTime = 0.18f; // how long we force the bounce

    [Header("Direction")]
    [Tooltip("If |normal.x| >= this, use contact normal for left/right.")]
    public float normalXThreshold = 0.25f;

    [Header("Tags")]
    public string obstacleTag = "Obstacle";

    Rigidbody2D rb;
    float knockTimer = 0f;
    float knockDir = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (knockTimer > 0f)
        {
            // Force sideways motion so other movement code can't cancel it
            rb.linearVelocity = new Vector2(knockDir * knockbackSpeedX, Mathf.Max(rb.linearVelocity.y, knockbackLiftY));
            knockTimer -= Time.fixedDeltaTime;
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!IsObstacle(col.collider)) return;

        if (LivesManager.Instance != null)
            LivesManager.Instance.TakeHit();

        // Pick direction: prefer contact normal (works for left/right hits)
        float dir = 0f;
        if (col.contactCount > 0)
        {
            var n = col.GetContact(0).normal; // normal points from obstacle -> player
            if (Mathf.Abs(n.x) >= normalXThreshold)
                dir = Mathf.Sign(n.x); // if player is left of obstacle, n.x is negative -> bounce left
        }

        // If hit from TOP (normal mostly up), still bounce sideways
        if (dir == 0f)
        {
            float dx = transform.position.x - col.collider.transform.position.x;
            dir = (Mathf.Abs(dx) > 0.001f) ? Mathf.Sign(dx) : (Random.value < 0.5f ? -1f : 1f);
        }

        knockDir = dir;
        knockTimer = knockbackTime;

        Debug.Log($"BOUNCE start dir={knockDir} hit={col.collider.name}");
    }

    bool IsObstacle(Collider2D c)
    {
        return c.CompareTag(obstacleTag) ||
               (c.transform.parent != null && c.transform.parent.CompareTag(obstacleTag));
    }
}
