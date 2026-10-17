using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Vida del jefe: notifica a la UI vía GameEvents, activa la fase 2 al 50% y entrega la recompensa al morir.
/// La animación de muerte y la IA viven en BossController (SRP).
/// </summary>
public class BossHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 24;
    [SerializeField] private int orbReward = 15;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.45f, 0.45f);
    [SerializeField] private float hitFlashDuration = 0.08f;

    public event Action Damaged;
    public event Action PhaseTwoReached;
    public event Action Died;

    public bool IsInvulnerable { get; set; }
    public bool IsDead { get; private set; }

    private int currentHealth;
    private bool phaseTwo;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>Envía la vida inicial a la UI al empezar el combate.</summary>
    public void Announce() => GameEvents.OnBossHealthChanged?.Invoke(currentHealth, maxHealth);

    public void TakeDamage(int damageAmount, Vector2 hitDirection)
    {
        if (IsDead || IsInvulnerable) return;

        currentHealth = Mathf.Max(0, currentHealth - damageAmount);
        GameEvents.OnBossHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth == 0)
        {
            Die();
            return;
        }

        GameEvents.OnPlaySfx?.Invoke(SfxType.EnemyHit);
        Damaged?.Invoke();
        if (spriteRenderer != null)
        {
            StopAllCoroutines();
            StartCoroutine(HitFlashRoutine());
        }

        if (!phaseTwo && currentHealth <= maxHealth / 2)
        {
            phaseTwo = true;
            PhaseTwoReached?.Invoke();
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(hitFlashDuration);
        spriteRenderer.color = Color.white;
    }

    private void Die()
    {
        IsDead = true;
        StopAllCoroutines();
        if (spriteRenderer != null) spriteRenderer.color = Color.white;

        GameEvents.OnPlaySfx?.Invoke(SfxType.EnemyDeath);
        if (GameManager.Instance != null) GameManager.Instance.AddOrbs(orbReward);

        Died?.Invoke();
        GameEvents.OnBossDefeated?.Invoke();
    }
}
