using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;

    [Header("Rewards")]
    [SerializeField] private int orbReward = 5;

    [Header("Game Feel")]
    [SerializeField] private float knockbackForce = 6f;

    private int currentHealth;
    private bool isDead;
    private Rigidbody2D rb;

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
    }

    public void TakeDamage(int damageAmount, Vector2 hitDirection)
    {
        if (isDead) return;

        currentHealth -= damageAmount;

        if (rb != null)
        {
            rb.linearVelocity = hitDirection.normalized * knockbackForce;
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        // El GameManager se encarga de notificar a la UI mediante GameEvents.OnOrbsChanged
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddOrbs(orbReward);
        }

        Destroy(gameObject);
    }
}
