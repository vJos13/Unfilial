using UnityEngine;

/// <summary>
/// Movimiento del enemigo: patrulla entre bordes/paredes y persigue al jugador cuando lo detecta.
/// Nunca se tira por un borde persiguiendo.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyPatrol : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float detectionRadius = 6f;
    [SerializeField] private float loseInterestRadius = 9f; // Histéresis: una vez te ve, cuesta despistarlo
    [SerializeField] private float maxVerticalChaseDistance = 3f; // No persigue si estás muy arriba/abajo
    [SerializeField] private LayerMask playerLayer;

    [Header("Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float edgeCheckDistance = 1f;  // Raycast hacia abajo (detecta bordes)
    [SerializeField] private float wallCheckDistance = 0.6f; // Raycast hacia el frente (detecta paredes)
    [SerializeField] private Vector2 edgeCheckOffset = new Vector2(0.5f, 0f);

    [Header("Hit Reaction")]
    [SerializeField] private float stunDuration = 0.25f; // Deja que el knockback se vea antes de volver a moverse

    public bool IsFacingRight { get; private set; } = true;
    public float FacingSign => IsFacingRight ? 1f : -1f;
    public bool IsChasing => target != null;

    private Rigidbody2D rb;
    private EnemyHealth health;
    private Transform target;
    private float stunTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        if (health != null) health.Damaged += HandleDamaged;
    }

    private void OnDestroy()
    {
        if (health != null) health.Damaged -= HandleDamaged;
    }

    private void FixedUpdate()
    {
        if (stunTimer > 0f)
        {
            stunTimer -= Time.fixedDeltaTime;
            return;
        }

        UpdateTarget();

        if (target != null)
        {
            Chase();
        }
        else
        {
            Patrol();
        }
    }

    private void Patrol()
    {
        if (ShouldTurnAround())
        {
            Flip();
        }

        SetHorizontalSpeed(moveSpeed);
    }

    private void Chase()
    {
        float deltaX = target.position.x - transform.position.x;

        if (Mathf.Abs(deltaX) > 0.3f && Mathf.Sign(deltaX) != FacingSign)
        {
            Flip();
        }

        // Si hay un borde o pared delante, espera en el sitio en vez de caerse
        bool canAdvance = Mathf.Abs(deltaX) > 0.3f && !ShouldTurnAround();
        SetHorizontalSpeed(canAdvance ? chaseSpeed : 0f);
    }

    private void UpdateTarget()
    {
        if (target != null)
        {
            Vector2 offset = target.position - transform.position;
            bool lost = offset.magnitude > loseInterestRadius || Mathf.Abs(offset.y) > maxVerticalChaseDistance + 1f || !target.gameObject.activeInHierarchy;
            if (lost) target = null;
            return;
        }

        Collider2D found = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        if (found != null && Mathf.Abs(found.transform.position.y - transform.position.y) <= maxVerticalChaseDistance)
        {
            target = found.transform;
        }
    }

    private void SetHorizontalSpeed(float speed)
    {
        rb.linearVelocity = new Vector2(FacingSign * speed, rb.linearVelocity.y);
    }

    public void FaceTowards(float worldX)
    {
        float deltaX = worldX - transform.position.x;
        if (Mathf.Abs(deltaX) > 0.1f && Mathf.Sign(deltaX) != FacingSign)
        {
            Flip();
        }
    }

    public bool ShouldTurnAround()
    {
        Vector2 forward = new Vector2(FacingSign, 0f);
        Vector2 edgeOrigin = (Vector2)transform.position + new Vector2(edgeCheckOffset.x * FacingSign, edgeCheckOffset.y);

        bool groundAhead = Physics2D.Raycast(edgeOrigin, Vector2.down, edgeCheckDistance, groundLayer);
        bool wallAhead = Physics2D.Raycast(transform.position, forward, wallCheckDistance, groundLayer);

        return !groundAhead || wallAhead;
    }

    private void HandleDamaged(Vector2 hitDirection)
    {
        stunTimer = stunDuration;
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
        Gizmos.color = new Color(1f, 0.6f, 0f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
