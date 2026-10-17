using UnityEngine;

/// <summary>
/// Zona de peligro (pinchos del Hazards_Tilemap). Daña al jugador y lo lanza hacia arriba
/// para que pueda salir; los i-frames de PlayerHealth evitan el daño continuo.
/// </summary>
public class DamageZone : MonoBehaviour
{
    [SerializeField] private int damage = 20;
    [SerializeField] private float bounceForce = 14f;

    private void OnTriggerEnter2D(Collider2D collision) => TryDamage(collision);

    private void OnTriggerStay2D(Collider2D collision) => TryDamage(collision);

    private void TryDamage(Collider2D collision)
    {
        // Verificamos si lo que entró a la trampa es el jugador
        if (!collision.CompareTag("Player")) return;

        // Buscamos el componente de vida y le aplicamos el daño
        PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
        if (playerHealth == null || !playerHealth.TakeDamage(damage)) return;

        Rigidbody2D body = collision.attachedRigidbody;
        if (body != null)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, bounceForce);
        }
    }
}
