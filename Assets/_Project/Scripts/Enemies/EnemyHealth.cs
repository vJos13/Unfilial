using System;
using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;

    [Header("Rewards")]
    [SerializeField] private int orbReward = 2;

    [Header("Game Feel")]
    [SerializeField] private float knockbackForce = 6f;
    [SerializeField] private float hitFlashDuration = 0.08f;

    // Evento local: otros módulos del mismo enemigo (patrulla) reaccionan al golpe sin acoplarse a la vida
    public event Action<Vector2> Damaged;

    private int currentHealth;
    private bool isDead;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Color baseColor;

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
    }

    public void TakeDamage(int damageAmount, Vector2 hitDirection)
    {
        if (isDead) return;

        currentHealth -= damageAmount;

        if (rb != null)
        {
            // Empuje horizontal con un pequeño salto para que se note el impacto
            Vector2 knockback = new Vector2(hitDirection.x, Mathf.Max(hitDirection.y, 0.4f)).normalized;
            rb.linearVelocity = knockback * knockbackForce;
        }

        Damaged?.Invoke(hitDirection);

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        GameEvents.OnPlaySfx?.Invoke(SfxType.EnemyHit);
        if (spriteRenderer != null)
        {
            StopAllCoroutines();
            StartCoroutine(HitFlashRoutine());
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(hitFlashDuration);
        spriteRenderer.color = baseColor;
    }

    private void Die()
    {
        isDead = true;
        GameEvents.OnPlaySfx?.Invoke(SfxType.EnemyDeath);

        // El GameManager se encarga de notificar a la UI mediante GameEvents.OnOrbsChanged
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddOrbs(orbReward);
        }

        Destroy(gameObject);
    }
}
