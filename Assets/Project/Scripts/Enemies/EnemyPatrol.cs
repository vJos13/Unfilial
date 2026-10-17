using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyPatrol : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float edgeCheckDistance = 1f;  // Raycast hacia abajo (detecta bordes)
    [SerializeField] private float wallCheckDistance = 0.6f; // Raycast hacia el frente (detecta paredes)
    [SerializeField] private Vector2 edgeCheckOffset = new Vector2(0.5f, 0f);

    public bool IsFacingRight { get; private set; } = true;
    public float FacingSign => IsFacingRight ? 1f : -1f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (ShouldTurnAround())
        {
            Flip();
        }

        rb.linearVelocity = new Vector2(FacingSign * moveSpeed, rb.linearVelocity.y);
    }

    private bool ShouldTurnAround()
    {
        Vector2 forward = new Vector2(FacingSign, 0f);
        Vector2 edgeOrigin = (Vector2)transform.position + new Vector2(edgeCheckOffset.x * FacingSign, edgeCheckOffset.y);

        bool groundAhead = Physics2D.Raycast(edgeOrigin, Vector2.down, edgeCheckDistance, groundLayer);
        bool wallAhead = Physics2D.Raycast(transform.position, forward, wallCheckDistance, groundLayer);

        return !groundAhead || wallAhead;
    }

    private void Flip()
    {
        IsFacingRight = !IsFacingRight;
        transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
    }

    private void OnDrawGizmosSelected()
    {
        float sign = Application.isPlaying ? FacingSign : 1f;
        Vector3 edgeOrigin = transform.position + new Vector3(edgeCheckOffset.x * sign, edgeCheckOffset.y);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(edgeOrigin, edgeOrigin + Vector3.down * edgeCheckDistance);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * sign * wallCheckDistance);
    }
}
