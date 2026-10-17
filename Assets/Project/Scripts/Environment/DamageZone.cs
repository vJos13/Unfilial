using UnityEngine;

public class DamageZone : MonoBehaviour
{
    [SerializeField] private int damage = 25;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Verificamos si lo que entró a la trampa es el jugador
        if (collision.CompareTag("Player"))
        {
            // Buscamos el componente de vida y le aplicamos el daño
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }
        }
    }
}