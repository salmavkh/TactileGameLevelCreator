using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController2D : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float jumpForce = 9f;

    [Tooltip("Include Ground + OneWay layers here.")]
    public LayerMask groundLayer;

    [Header("Double Jump")]
    public int maxAirJumps = 1; // 1 = double jump

    [Header("Obstacle Bounce")]
    public float obstacleBounceForce = 6f;

    [Header("Animation")]
    [Tooltip("Optional. If empty, will auto-find Animator on this object or children.")]
    public Animator animator;
    [Tooltip("Walk/Idle threshold; must match your Animator transition threshold.")]
    public float walkSpeedThreshold = 0.05f;
    [Tooltip("If true, trigger Touch animation when colliding with obstacles.")]
    public bool touchOnObstacleHit = true;

    private Rigidbody2D rb;
    private BoxCollider2D col;

    private float moveX;
    private bool jumpQueued;

    private int airJumpsLeft;

    // Animator hashes (faster + avoids typos)
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int TouchHash = Animator.StringToHash("Touch");

    public bool IsGroundedPublic() => IsGrounded();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();

        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        airJumpsLeft = maxAirJumps;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        moveX = 0f;
        if (Input.GetKey(KeyCode.A)) moveX = -1f;
        if (Input.GetKey(KeyCode.D)) moveX = 1f;

        if (Input.GetKeyDown(KeyCode.Space))
            jumpQueued = true;
    }

    void FixedUpdate()
    {
        // Movement
        rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);

        // Animation: set Speed based on horizontal velocity magnitude
        if (animator != null)
        {
            float speed = Mathf.Abs(rb.linearVelocity.x);
            animator.SetFloat(SpeedHash, speed);
        }

        bool grounded = IsGrounded();
        if (grounded)
            airJumpsLeft = maxAirJumps;

        if (jumpQueued)
        {
            jumpQueued = false;

            if (grounded)
            {
                Jump();
            }
            else if (airJumpsLeft > 0)
            {
                airJumpsLeft--;
                Jump();
            }
        }
    }

    void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

        var sfx = GetComponent<PlayerSFX2D>();
        if (sfx != null) sfx.PlayJump();
    }

    bool IsGrounded()
    {
        float extra = 0.06f;
        RaycastHit2D hit = Physics2D.BoxCast(
            col.bounds.center,
            col.bounds.size,
            0f,
            Vector2.down,
            extra,
            groundLayer
        );
        return hit.collider != null;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Obstacle")) return;

        // Optional: play Touch animation when hitting obstacle
        if (touchOnObstacleHit && animator != null)
            animator.SetTrigger(TouchHash);

        // Direction: away from obstacle
        float dir = Mathf.Sign(transform.position.x - collision.transform.position.x);
        if (dir == 0) dir = 1f;

        Vector2 bounce = new Vector2(dir * obstacleBounceForce, 0f);

        // Reset horizontal velocity so bounce feels clean
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        rb.AddForce(bounce, ForceMode2D.Impulse);
    }

    // Call this from collectibles when picked up
    public void PlayTouch()
    {
        if (animator != null)
            animator.SetTrigger(TouchHash);
    }
}
