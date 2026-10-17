using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;

    private bool isDead = false;

    private void Start()
    {
        currentHealth = maxHealth;
        // Avisamos a la UI (cuando la hagamos) de cuánta vida tenemos al iniciar
        GameEvents.OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead) return;

        currentHealth -= damageAmount;
        
        // Evitamos que la vida baje de cero
        if (currentHealth < 0) currentHealth = 0;

        GameEvents.OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Debug.Log($"El jugador recibió daño. Vida actual: {currentHealth}");

        if (currentHealth == 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        
        // Desactivamos el movimiento para que no siga caminando muerto
        GetComponent<PlayerMovement>().enabled = false;
        GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;

        // Disparamos el evento global. El GameManager lo escuchará y lanzará el Game Over.
        GameEvents.OnPlayerDeath?.Invoke();
    }
}