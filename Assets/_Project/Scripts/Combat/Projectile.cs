using System;
using UnityEngine;

/// <summary>
/// Proyectil reciclable: lo entrega un ProjectilePool y vuelve a él al impactar o caducar.
/// Puede bloquearse con el escudo del jugador.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 7f;
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private int damage = 15;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private Action<Projectile> releaseToPool;
    private float timeLeft;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 position, Vector2 direction, Action<Projectile> release)
    {
        transform.position = position;
        transform.right = direction;
        rb.position = position;
        rb.linearVelocity = direction.normalized * speed;
        timeLeft = lifetime;
        releaseToPool = release;
    }

    private void Update()
    {
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) Despawn();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if ((groundLayer.value & (1 << other.gameObject.layer)) != 0)
        {
            Despawn();
            return;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null) return;

        // El origen del golpe es "detrás" del proyectil: así el escudo frontal puede bloquearlo
        playerHealth.TakeDamage(damage, (Vector2)transform.position - rb.linearVelocity.normalized);
        Despawn();
    }

    private void Despawn()
    {
        if (releaseToPool == null) return; // Ya devuelto (evita doble Release)

        Action<Projectile> release = releaseToPool;
        releaseToPool = null;
        rb.linearVelocity = Vector2.zero;
        release(this);
    }
}
