using UnityEngine;

/// <summary>
/// Contrato para cualquier cosa que pueda recibir daño del jugador (enemigos, objetos rompibles...).
/// </summary>
public interface IDamageable
{
    // hitDirection permite aplicar knockback desde quien golpea
    void TakeDamage(int damageAmount, Vector2 hitDirection);
}
