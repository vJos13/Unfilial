using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;

    [Header("Game Feel")]
    [SerializeField] private float invulnerabilityTime = 1f; // i-frames tras recibir daño
    [SerializeField] private float blinkInterval = 0.08f;

    // Evento local (audio, animación, cámara). El evento global sigue siendo OnHealthChanged.
    public event Action Damaged;

    private bool isDead = false;
    private bool isInvulnerable;
    private PlayerShield shield;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        shield = GetComponent<PlayerShield>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        // Avisamos a la UI de cuánta vida tenemos al iniciar
        GameEvents.OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // Daño ambiental (trampas): no se puede bloquear con el escudo
    public bool TakeDamage(int damageAmount)
    {
        return ApplyDamage(damageAmount);
    }

    /// <summary>
    /// Daño de un atacante situado en sourcePosition. Devuelve true si el daño se aplicó.
    /// </summary>
    public bool TakeDamage(int damageAmount, Vector2 sourcePosition)
    {
        if (isDead || isInvulnerable) return false;

        if (shield != null && shield.TryBlock(sourcePosition)) return false;

        return ApplyDamage(damageAmount);
    }

    // Caídas al vacío: muerte instantánea, ignora escudo e invulnerabilidad
    public void Kill()
    {
        if (isDead) return;

        currentHealth = 0;
        GameEvents.OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Die();
    }

    private bool ApplyDamage(int damageAmount)
    {
        if (isDead || isInvulnerable) return false;

        currentHealth -= damageAmount;

        // Evitamos que la vida baje de cero
        if (currentHealth < 0) currentHealth = 0;

        GameEvents.OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Damaged?.Invoke();

        if (currentHealth == 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(InvulnerabilityRoutine());
        }

        return true;
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;

        // Parpadeo del sprite mientras dura la invulnerabilidad
        float elapsed = 0f;
        while (elapsed < invulnerabilityTime)
        {
            if (spriteRenderer != null) spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        if (spriteRenderer != null) spriteRenderer.enabled = true;
        isInvulnerable = false;
    }

    private void Die()
    {
        isDead = true;
        StopAllCoroutines();
        if (spriteRenderer != null) spriteRenderer.enabled = true;

        // Desactivamos el movimiento para que no siga caminando muerto
        GetComponent<PlayerMovement>().enabled = false;
        GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;

        // Disparamos el evento global. El GameManager lo escuchará y lanzará el Game Over.
        GameEvents.OnPlayerDeath?.Invoke();
    }
}
