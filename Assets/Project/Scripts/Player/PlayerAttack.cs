using UnityEngine;

[RequireComponent(typeof(PlayerInput), typeof(Rigidbody2D))]
public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float attackCooldown = 0.35f;
    [SerializeField] private float attackRadius = 0.6f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Attack Points")]
    [SerializeField] private Transform attackPoint; // Ataque frontal (gira con el Flip del jugador)
    [SerializeField] private float verticalAttackOffset = 1f; // Distancia para ataques arriba/abajo

    [Header("Game Feel")]
    [SerializeField] private float pogoForce = 18f; // Rebote al golpear hacia abajo (estilo Hollow Knight)

    private PlayerInput playerInput;
    private Rigidbody2D rb;
    private float nextAttackTime;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerDeath += HandleDeath;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDeath -= HandleDeath;
    }

    private void Update()
    {
        if (!playerInput.AttackTriggered) return;

        // Consumimos siempre para que el input no se quede guardado durante el cooldown
        playerInput.ConsumeAttack();

        if (Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + attackCooldown;
        PerformAttack();
    }

    private void PerformAttack()
    {
        Vector2 direction = GetAttackDirection();
        Vector2 origin = GetAttackOrigin(direction);

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius, enemyLayer);
        bool hitSomething = false;

        foreach (Collider2D hit in hits)
        {
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            damageable.TakeDamage(damage, direction);
            hitSomething = true;
        }

        // Pogo: golpear hacia abajo nos impulsa hacia arriba
        if (hitSomething && direction == Vector2.down)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, pogoForce);
        }
    }

    private Vector2 GetAttackDirection()
    {
        float vertical = playerInput.MoveInput.y;

        if (vertical > 0.5f) return Vector2.up;
        if (vertical < -0.5f) return Vector2.down;

        // El Flip invierte localScale.x, así que su signo indica hacia dónde miramos
        return transform.localScale.x >= 0f ? Vector2.right : Vector2.left;
    }

    private Vector2 GetAttackOrigin(Vector2 direction)
    {
        if (direction == Vector2.up || direction == Vector2.down)
        {
            return (Vector2)transform.position + direction * verticalAttackOffset;
        }

        return attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position + direction;
    }

    private void HandleDeath()
    {
        enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * verticalAttackOffset, attackRadius);
        Gizmos.DrawWireSphere(transform.position + Vector3.down * verticalAttackOffset, attackRadius);
    }
}
