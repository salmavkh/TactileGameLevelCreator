using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerSFX2D : MonoBehaviour
{
    [Header("Refs")]
    public PlayerController2D controller; // optional; will auto-find
    Rigidbody2D rb;

    [Header("Footsteps")]
    public float minSpeedForSteps = 0.2f;
    public float stepInterval = 0.28f;

    [Header("Landing")]
    public float minFallSpeedForLand = 2.5f;

    float stepTimer = 0f;
    bool wasGrounded = false;
    float lastYVel = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (controller == null) controller = GetComponent<PlayerController2D>();
    }

    void Update()
    {
        if (AudioManager.Instance == null || controller == null) return;

        bool grounded = controller.IsGroundedPublic();
        float vx = Mathf.Abs(rb.linearVelocity.x);
        float vy = rb.linearVelocity.y;

        // Footsteps: only when grounded and actually moving
        if (grounded && vx >= minSpeedForSteps)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                AudioManager.Instance.PlayPlayer(SFX.Footstep);
                stepTimer = stepInterval;
            }
        }
        else
        {
            stepTimer = 0f;
        }

        // Landing: airborne -> grounded with enough downward speed
        if (!wasGrounded && grounded)
        {
            if (Mathf.Abs(lastYVel) >= minFallSpeedForLand && lastYVel < 0f)
                AudioManager.Instance.PlayPlayer(SFX.Land);
        }

        wasGrounded = grounded;
        lastYVel = vy;
    }

    // Call this from PlayerController2D when jump happens
    public void PlayJump()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayPlayer(SFX.Jump);
    }
}
