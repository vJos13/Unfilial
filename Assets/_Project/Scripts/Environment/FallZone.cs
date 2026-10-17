using UnityEngine;

/// <summary>
/// Vacío bajo el nivel: caer aquí mata al jugador al instante y elimina a los enemigos que caigan.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FallZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        PlayerHealth playerHealth = collision.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.Kill();
            return;
        }

        if (collision.GetComponentInParent<EnemyHealth>() != null)
        {
            Destroy(collision.attachedRigidbody != null ? collision.attachedRigidbody.gameObject : collision.gameObject);
        }
    }
}
