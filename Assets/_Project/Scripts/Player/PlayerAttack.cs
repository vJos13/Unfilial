using System;
using UnityEngine;

[RequireComponent(typeof(PlayerInput), typeof(Rigidbody2D))]
public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private int baseDamage = 1;
    [SerializeField] private int damagePerPowerLevel = 1; // Daño extra por cada nivel de poder (cada 15 orbes)
    [SerializeField] private float attackCooldown = 0.35f;
    [SerializeField] private float attackRadius = 0.7f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Attack Points")]
    [SerializeField] private Transform attackPoint; // Ataque frontal (gira con el Flip del jugador)
    [SerializeField] private float verticalAttackOffset = 1.3f; // Distancia para ataques arriba/abajo

    [Header("Game Feel")]
    [SerializeField] private float pogoForce = 18f; // Rebote al golpear hacia abajo (estilo Hollow Knight)
    [SerializeField] private SlashEffect slashEffect;
    [SerializeField] private ParticleSystem hitImpactVfx; // VFX_HitImpact (Stop Action = Destroy)

    [Header("Optional")]
    [SerializeField] private PlayerShield shield; // No se puede atacar con el escudo levantado

    public int CurrentDamage => baseDamage + powerLevel * damagePerPowerLevel;

    public event Action<Vector2> Attacked; // dirección del ataque
    public event Action HitLanded;

    private PlayerInput playerInput;
    private Rigidbody2D rb;
    private float nextAttackTime;
    private int powerLevel;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerDeath += HandleDeath;
        GameEvents.OnPowerLevelChanged += HandlePowerLevelChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDeath -= HandleDeath;
        GameEvents.OnPowerLevelChanged -= HandlePowerLevelChanged;
    }

    private void Start()
    {
        // El poder persiste entre niveles (GameManager es DontDestroyOnLoad)
        if (GameManager.Instance != null) powerLevel = GameManager.Instance.PowerLevel;
    }

    private void Update()
    {
        if (!playerInput.AttackTriggered) return;

        // Consumimos siempre para que el input no se quede guardado durante el cooldown
        playerInput.ConsumeAttack();

        if (Time.time < nextAttackTime) return;
        if (shield != null && shield.IsBlocking) return;

        nextAttackTime = Time.time + attackCooldown;
        PerformAttack();
    }

    private void PerformAttack()
    {
        Vector2 direction = GetAttackDirection();
        Vector2 origin = GetAttackOrigin(direction);

        if (slashEffect != null) slashEffect.Play(origin, direction);
        Attacked?.Invoke(direction);

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius, enemyLayer);
        bool hitSomething = false;

        foreach (Collider2D hit in hits)
        {
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            damageable.TakeDamage(CurrentDamage, direction);
            if (hitImpactVfx != null) Instantiate(hitImpactVfx, hit.ClosestPoint(origin), Quaternion.identity).Play();
            hitSomething = true;
        }

        if (hitSomething) HitLanded?.Invoke();

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

    private void HandlePowerLevelChanged(int newPowerLevel)
    {
        powerLevel = newPowerLevel;
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
