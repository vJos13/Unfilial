using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(EnemyPatrol))]
public class EnemyMeleeAttacker : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRadius = 4f;
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

    private Rigidbody2D rb;
    private EnemyPatrol patrol;
    private bool isAttacking;
    private bool isDashing;
    private float nextAttackTime;
    private float nextDamageTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        patrol = GetComponent<EnemyPatrol>();
    }

    private void Update()
    {
        if (isAttacking || Time.time < nextAttackTime) return;

        Collider2D target = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        if (target != null)
        {
            StartCoroutine(DashAttackRoutine(target.transform.position));
        }
    }

    private IEnumerator DashAttackRoutine(Vector2 targetPosition)
    {
        isAttacking = true;
        patrol.enabled = false; // Pausamos la patrulla durante el ataque

        // Wind-up: se detiene antes de embestir
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        yield return new WaitForSeconds(windUpTime);

        float direction = Mathf.Sign(targetPosition.x - transform.position.x);
        isDashing = true;

        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
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

    private void OnCollisionEnter2D(Collision2D collision) => TryDamage(collision.collider);

    private void OnCollisionStay2D(Collision2D collision) => TryDamage(collision.collider);

    private void TryDamage(Collider2D other)
    {
        if (Time.time < nextDamageTime) return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null) return;

        playerHealth.TakeDamage(isDashing ? dashDamage : contactDamage);
        nextDamageTime = Time.time + damageInterval;
    }

    private void OnDisable()
    {
        // Si nos desactivan a mitad del ataque, devolvemos el control a la patrulla
        StopAllCoroutines();
        isAttacking = false;
        isDashing = false;
        if (patrol != null) patrol.enabled = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
