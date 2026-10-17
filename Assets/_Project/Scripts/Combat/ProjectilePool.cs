using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Object Pool de proyectiles (UnityEngine.Pool.ObjectPool): reutiliza instancias con Get()/Release()
/// en lugar de Instantiate/Destroy en cada disparo, evitando picos del Garbage Collector.
/// </summary>
public class ProjectilePool : MonoBehaviour
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private int defaultCapacity = 6;
    [SerializeField] private int maxSize = 20;

    private ObjectPool<Projectile> pool;
    private bool isDestroyed;

    private void Awake()
    {
        pool = new ObjectPool<Projectile>(
            createFunc: () => Instantiate(projectilePrefab),
            actionOnGet: projectile => projectile.gameObject.SetActive(true),
            actionOnRelease: projectile => projectile.gameObject.SetActive(false),
            actionOnDestroy: projectile => { if (projectile != null) Destroy(projectile.gameObject); },
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize);
    }

    public void Fire(Vector2 position, Vector2 direction)
    {
        Projectile projectile = pool.Get();
        projectile.Launch(position, direction, Release);
    }

    private void Release(Projectile projectile)
    {
        // Si el dueño murió con proyectiles en vuelo, estos se destruyen al aterrizar
        if (isDestroyed)
        {
            Destroy(projectile.gameObject);
            return;
        }

        pool.Release(projectile);
    }

    private void OnDestroy()
    {
        isDestroyed = true;
        pool.Clear();
    }
}
