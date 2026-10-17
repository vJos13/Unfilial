using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(EnemyPatrol))]
public class EnemyMeleeAttacker : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Dash Attack")]
    [SerializeField] private float windUpTime = 0.4f; // Telegrafía el ataque para que el jugador pueda reaccionar
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Damage")]
    [SerializeField] private int contactDamage = 10;
    [SerializeField] private int dashDamage = 25;
    [SerializeField] private float damageInterval = 0.75f; // Evita aplicar daño cada frame mientras hay contacto

    [Header("Visual")]
    [SerializeField] private Color windUpColor = new Color(1f, 0.85f, 0.4f);

    private Rigidbody2D rb;
    private EnemyPatrol patrol;
    private Collider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private bool isAttacking;
    private bool isDashing;
    private float nextAttackTime;
    private float nextDamageTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        patrol = GetComponent<EnemyPatrol>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
    }

    private void Update()
    {
        if (isAttacking || Time.time < nextAttackTime) return;

        Collider2D target = Physics2D.OverlapCircle(transform.position, attackRange, playerLayer);
        if (target != null && Mathf.Abs(target.transform.position.y - transform.position.y) < 1.5f)
        {
            StartCoroutine(DashAttackRoutine(target.transform));
        }
    }

    private void FixedUpdate()
    {
        // El jugador y los enemigos no chocan físicamente (se atraviesan como en Hollow Knight),
        // así que el daño por contacto se detecta con una consulta de solapamiento.
        if (bodyCollider == null) return;

        Bounds bounds = bodyCollider.bounds;
        Collider2D hit = Physics2D.OverlapBox(bounds.center, bounds.size, 0f, playerLayer);
        if (hit != null) TryDamage(hit);
    }

    private IEnumerator DashAttackRoutine(Transform target)
    {
        isAttacking = true;
        patrol.enabled = false; // Pausamos la patrulla durante el ataque
        patrol.FaceTowards(target.position.x);

        // Wind-up: se detiene y cambia de color antes de embestir
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (spriteRenderer != null) spriteRenderer.color = windUpColor;
        yield return new WaitForSeconds(windUpTime);
        if (spriteRenderer != null) spriteRenderer.color = baseColor;

        float direction = patrol.FacingSign;
        isDashing = true;

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            // No se lanza al vacío: si hay borde delante corta la embestida
            if (patrol.ShouldTurnAround()) break;

            rb.linearVelocity = new Vector2(direction * dashSpeed, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }

        isDashing = false;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        nextAttackTime = Time.time + attackCooldown;
        patrol.enabled = true;
        isAttacking = false;
    }

    private void TryDamage(Collider2D other)
    {
        if (Time.time < nextDamageTime) return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null) return;

        playerHealth.TakeDamage(isDashing ? dashDamage : contactDamage, transform.position);
        nextDamageTime = Time.time + damageInterval;
    }

    private void OnDisable()
    {
        // Si nos desactivan a mitad del ataque, devolvemos el control a la patrulla
        StopAllCoroutines();
        isAttacking = false;
        isDashing = false;
        if (spriteRenderer != null) spriteRenderer.color = baseColor;
        if (patrol != null) patrol.enabled = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
