using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo a distancia (Vigía): estático, se gira hacia el jugador y dispara proyectiles del ProjectilePool.
/// </summary>
[RequireComponent(typeof(ProjectilePool))]
public class EnemyShooter : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float range = 10f;
    [SerializeField] private float maxVerticalDistance = 5f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Attack")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private float windUpTime = 0.4f;
    [SerializeField] private float fireCooldown = 2.2f;
    [SerializeField] private Color windUpColor = new Color(1f, 0.9f, 0.5f);

    private ProjectilePool pool;
    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private bool isFiring;
    private float nextFireTime;

    private void Awake()
    {
        pool = GetComponent<ProjectilePool>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
        nextFireTime = Time.time + 1f; // Margen al empezar el nivel
    }

    private void Update()
    {
        if (isFiring) return;

        Collider2D target = Physics2D.OverlapCircle(transform.position, range, playerLayer);
        if (target == null || Mathf.Abs(target.transform.position.y - transform.position.y) > maxVerticalDistance) return;

        FaceTowards(target.transform.position.x);

        if (Time.time >= nextFireTime)
        {
            StartCoroutine(FireRoutine(target.transform));
        }
    }

    private IEnumerator FireRoutine(Transform target)
    {
        isFiring = true;

        if (spriteRenderer != null) spriteRenderer.color = windUpColor;
        yield return new WaitForSeconds(windUpTime);
        if (spriteRenderer != null) spriteRenderer.color = baseColor;

        if (target != null)
        {
            Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
            Vector2 aim = (Vector2)target.position + Vector2.up * 0.3f - origin;
            pool.Fire(origin, aim);
        }

        nextFireTime = Time.time + fireCooldown;
        isFiring = false;
    }

    private void FaceTowards(float worldX)
    {
        float sign = Mathf.Sign(worldX - transform.position.x);
        Vector3 scale = transform.localScale;
        if (Mathf.Sign(scale.x) != sign)
        {
            transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isFiring = false;
        if (spriteRenderer != null) spriteRenderer.color = baseColor;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
