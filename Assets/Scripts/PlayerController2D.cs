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

    private Rigidbody2D rb;
    private BoxCollider2D col;

    private float moveX;
    private bool jumpQueued;

    private int airJumpsLeft;
    public float obstacleBounceForce = 6f;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();

        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        airJumpsLeft = maxAirJumps;
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
        rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);

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

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) return;

        // Direction: away from obstacle
        float dir = Mathf.Sign(transform.position.x - collision.transform.position.x);
        if (dir == 0) dir = 1f;

        Vector2 bounce = new Vector2(dir * obstacleBounceForce, 0f);

        // Reset horizontal velocity so bounce feels clean
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        rb.AddForce(bounce, ForceMode2D.Impulse);
    }

}
