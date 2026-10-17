using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// IA de jefe: despierta al acercarse el jugador y alterna movimiento, ataque cuerpo a cuerpo
/// y ataque especial. Cada ataque se telegrafía (pose congelada + tinte) para poder esquivarlo
/// o bloquearlo con el escudo. Al 50% de vida entra en fase 2 (más rápido y agresivo).
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BossHealth), typeof(ProjectilePool))]
public class BossController : MonoBehaviour
{
    public enum SpecialAttack { GroundWave, LavaSpit, LightLances }

    private static readonly int IdleState = Animator.StringToHash("idle");
    private static readonly int MoveState = Animator.StringToHash("move");
    private static readonly int MeleeState = Animator.StringToHash("attack_melee");
    private static readonly int SpecialState = Animator.StringToHash("attack_special");
    private static readonly int HurtState = Animator.StringToHash("hurt");
    private static readonly int PhaseTwoState = Animator.StringToHash("phase2");
    private static readonly int DeathState = Animator.StringToHash("death");

    [Header("Identidad")]
    [SerializeField] private string bossName = "Jefe";
    [SerializeField] private SpecialAttack special = SpecialAttack.GroundWave;

    [Header("Detección")]
    [SerializeField] private float activationRange = 13f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask groundLayer;

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private bool isFlying;
    [SerializeField] private float hoverHeight = 3.5f; // Altura sobre el suelo de la arena (solo voladores)
    [SerializeField] private float phaseTwoSpeedMultiplier = 1.4f;

    [Header("Cuerpo a cuerpo")]
    [SerializeField] private float meleeRange = 3.2f;
    [SerializeField] private int meleeDamage = 25;
    [SerializeField] private float lungeSpeed = 11f;
    [SerializeField] private float lungeDuration = 0.35f;
    [SerializeField] private Vector2 strikeBoxSize = new Vector2(2.6f, 2.6f);

    [Header("Especial")]
    [SerializeField] private float specialCooldown = 4f;
    [SerializeField] private int projectilesPhaseOne = 3;
    [SerializeField] private int projectilesPhaseTwo = 5;
    [SerializeField] private float spreadAngle = 30f;
    [SerializeField] private Transform firePoint;

    [Header("Ritmo")]
    [SerializeField] private float telegraphTime = 0.55f; // Pose de aviso antes de cada ataque
    [SerializeField] private float recoveryTime = 0.7f;
    [SerializeField] private float telegraphFrameTime = 0.2f; // Duración de los 2 frames de aviso del sprite
    [SerializeField] private Color telegraphColor = new Color(1f, 0.85f, 0.5f);

    [Header("Contacto")]
    [SerializeField] private int contactDamage = 15;
    [SerializeField] private float contactInterval = 1f;

    private Rigidbody2D rb;
    private BossHealth health;
    private ProjectilePool pool;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Collider2D body;
    private CinemachineImpulseSource impulse;

    private Transform player;
    private bool isActive;
    private bool isPhaseTwo;
    private bool phaseTwoPending;
    private bool isAttacking;
    private float nextSpecialTime;
    private float nextContactTime;
    private float hoverY;

    private float SpeedFactor => isPhaseTwo ? phaseTwoSpeedMultiplier : 1f;
    private float FacingSign => transform.localScale.x > 0f ? -1f : 1f; // El sprite original mira a la izquierda

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<BossHealth>();
        pool = GetComponent<ProjectilePool>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Collider2D>();
        impulse = GetComponent<CinemachineImpulseSource>();
        hoverY = transform.position.y;

        health.Damaged += HandleDamaged;
        health.PhaseTwoReached += HandlePhaseTwo;
        health.Died += HandleDeath;
    }

    private void OnDestroy()
    {
        health.Damaged -= HandleDamaged;
        health.PhaseTwoReached -= HandlePhaseTwo;
        health.Died -= HandleDeath;
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerDeath += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDeath -= HandlePlayerDeath;
    }

    private void Start()
    {
        if (isFlying) rb.gravityScale = 0f;
        Play(IdleState);
    }

    private void Update()
    {
        if (isActive) return;

        Collider2D found = Physics2D.OverlapCircle(transform.position, activationRange, playerLayer);
        if (found != null)
        {
            player = found.transform;
            isActive = true;
            GameEvents.OnBossEncounterStarted?.Invoke(bossName);
            health.Announce();
            GameEvents.OnPlaySfx?.Invoke(SfxType.BossRoar);
            Shake(1.2f);
            StartCoroutine(BrainRoutine());
        }
    }

    private void FixedUpdate()
    {
        if (!isActive || health.IsDead || body == null) return;

        // Daño por contacto (jugador y enemigos se atraviesan)
        if (Time.time >= nextContactTime)
        {
            Bounds bounds = body.bounds;
            Collider2D hit = Physics2D.OverlapBox(bounds.center, bounds.size * 0.9f, 0f, playerLayer);
            if (hit != null && TryDamagePlayer(hit, contactDamage)) nextContactTime = Time.time + contactInterval;
        }

        if (isFlying && !isAttacking)
        {
            // Flota suavemente a su altura de vuelo
            float targetY = hoverY + Mathf.Sin(Time.time * 2f) * 0.4f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, (targetY - transform.position.y) * 4f);
        }
    }

    // ------------------------------------------------------------------ Cerebro

    private IEnumerator BrainRoutine()
    {
        yield return new WaitForSeconds(0.8f); // Pausa dramática tras el rugido
        nextSpecialTime = Time.time + 1.5f;

        while (!health.IsDead && player != null)
        {
            if (phaseTwoPending)
            {
                yield return PhaseTwoRoutine();
                continue;
            }

            FaceTowards(player.position.x);
            float distance = Mathf.Abs(player.position.x - transform.position.x);

            if (distance <= meleeRange)
            {
                yield return MeleeRoutine();
            }
            else if (Time.time >= nextSpecialTime)
            {
                yield return SpecialRoutine();
                nextSpecialTime = Time.time + specialCooldown / SpeedFactor;
            }
            else
            {
                yield return MoveRoutine(1f);
            }

            StopHorizontal();
            Play(IdleState);
            yield return new WaitForSeconds(recoveryTime / SpeedFactor);
        }
    }

    private IEnumerator MoveRoutine(float maxDuration)
    {
        Play(MoveState);
        float elapsed = 0f;

        while (elapsed < maxDuration && player != null && !phaseTwoPending)
        {
            float deltaX = player.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) <= meleeRange * 0.8f) break;

            FaceTowards(player.position.x);
            bool blocked = !isFlying && EdgeOrWallAhead();
            rb.linearVelocity = new Vector2(blocked ? 0f : Mathf.Sign(deltaX) * moveSpeed * SpeedFactor, rb.linearVelocity.y);

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopHorizontal();
    }

    private IEnumerator MeleeRoutine()
    {
        isAttacking = true;
        yield return Telegraph(MeleeState);

        // Embestida con caja de golpe frontal activa
        bool hasHit = false;
        float elapsed = 0f;
        // Los voladores se lanzan en picado hacia el jugador; los terrestres embisten en horizontal
        Vector2 direction = isFlying && player != null
            ? ((Vector2)player.position - (Vector2)transform.position).normalized
            : new Vector2(FacingSign, 0f);

        while (elapsed < lungeDuration)
        {
            bool blocked = !isFlying && EdgeOrWallAhead();
            Vector2 velocity = direction * lungeSpeed * SpeedFactor;
            rb.linearVelocity = isFlying ? velocity : new Vector2(blocked ? 0f : velocity.x, rb.linearVelocity.y);

            if (!hasHit)
            {
                Vector2 center = (Vector2)transform.position + direction * strikeBoxSize.x * 0.5f;
                Collider2D hit = Physics2D.OverlapBox(center, strikeBoxSize, 0f, playerLayer);
                if (hit != null && TryDamagePlayer(hit, meleeDamage)) hasHit = true;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopHorizontal();
        yield return new WaitForSeconds(0.3f); // Deja terminar los frames de recuperación
        isAttacking = false;
    }

    private IEnumerator SpecialRoutine()
    {
        isAttacking = true;
        yield return Telegraph(SpecialState);

        int count = isPhaseTwo ? projectilesPhaseTwo : projectilesPhaseOne;
        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;

        switch (special)
        {
            case SpecialAttack.GroundWave:
                // Ondas a ras de suelo hacia ambos lados: hay que saltarlas
                Shake(1f);
                Vector2 groundOrigin = new Vector2(transform.position.x, body.bounds.min.y + 0.45f);
                pool.Fire(groundOrigin, Vector2.left);
                pool.Fire(groundOrigin, Vector2.right);
                if (isPhaseTwo)
                {
                    yield return new WaitForSeconds(0.45f);
                    pool.Fire(groundOrigin, Vector2.left);
                    pool.Fire(groundOrigin, Vector2.right);
                }
                break;

            case SpecialAttack.LavaSpit:
            case SpecialAttack.LightLances:
                int volleys = special == SpecialAttack.LightLances && isPhaseTwo ? 2 : 1;
                for (int v = 0; v < volleys; v++)
                {
                    FireFan(origin, count);
                    if (v < volleys - 1) yield return new WaitForSeconds(0.35f);
                }
                break;
        }

        yield return new WaitForSeconds(0.4f);
        isAttacking = false;
    }

    private void FireFan(Vector2 origin, int count)
    {
        if (player == null) return;

        Vector2 aim = ((Vector2)player.position + Vector2.up * 0.3f - origin).normalized;
        float step = count > 1 ? spreadAngle / (count - 1) : 0f;
        float start = -spreadAngle * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle = count > 1 ? start + step * i : 0f;
            pool.Fire(origin, Quaternion.Euler(0f, 0f, angle) * aim);
        }
    }

    private IEnumerator PhaseTwoRoutine()
    {
        phaseTwoPending = false;
        isPhaseTwo = true;
        isAttacking = true;
        health.IsInvulnerable = true;

        StopHorizontal();
        Play(PhaseTwoState);
        GameEvents.OnPlaySfx?.Invoke(SfxType.BossRoar);
        Shake(1.5f);
        if (UIManager.Instance != null) UIManager.Instance.ShowToast($"¡{bossName} se enfurece!");

        yield return new WaitForSeconds(1f);

        health.IsInvulnerable = false;
        isAttacking = false;
    }

    /// <summary>Reproduce los frames de aviso del sprite y congela la pose con un tinte.</summary>
    private IEnumerator Telegraph(int state)
    {
        StopHorizontal();
        if (player != null) FaceTowards(player.position.x);

        Play(state);
        yield return new WaitForSeconds(telegraphFrameTime);

        if (animator != null) animator.speed = 0f;
        if (spriteRenderer != null) spriteRenderer.color = telegraphColor;
        yield return new WaitForSeconds(telegraphTime / SpeedFactor);
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        if (animator != null) animator.speed = 1f;
    }

    // ------------------------------------------------------------------ Reacciones

    private void HandleDamaged()
    {
        // Solo se interrumpe visualmente si no está atacando (los ataques no se cancelan)
        if (!isAttacking && isActive)
        {
            Play(HurtState);
            CancelInvoke(nameof(ResumeLocomotion));
            Invoke(nameof(ResumeLocomotion), 0.2f);
        }
    }

    private void ResumeLocomotion()
    {
        if (isAttacking || health.IsDead) return;
        Play(Mathf.Abs(rb.linearVelocity.x) > 0.1f ? MoveState : IdleState);
    }

    private void HandlePhaseTwo()
    {
        phaseTwoPending = true;
    }

    private void HandleDeath()
    {
        StopAllCoroutines();
        CancelInvoke();
        isActive = false;
        if (animator != null) animator.speed = 1f;
        StopHorizontal();
        rb.gravityScale = isFlying ? 0.5f : rb.gravityScale;
        Play(DeathState);
        Shake(1.5f);
        Destroy(gameObject, 1.6f);
    }

    private void HandlePlayerDeath()
    {
        StopAllCoroutines();
        if (animator != null) animator.speed = 1f;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        StopHorizontal();
        Play(IdleState);
        isAttacking = false;
    }

    // ------------------------------------------------------------------ Utilidades

    private bool TryDamagePlayer(Collider2D hit, int damage)
    {
        PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
        return playerHealth != null && playerHealth.TakeDamage(damage, transform.position);
    }

    private bool EdgeOrWallAhead()
    {
        Bounds bounds = body.bounds;
        float sign = FacingSign;
        Vector2 front = new Vector2(sign > 0f ? bounds.max.x + 0.1f : bounds.min.x - 0.1f, bounds.center.y);
        bool wall = Physics2D.Raycast(new Vector2(bounds.center.x, bounds.min.y + 0.3f), Vector2.right * sign, bounds.extents.x + 0.3f, groundLayer);
        bool ground = Physics2D.Raycast(front, Vector2.down, bounds.extents.y + 0.6f, groundLayer);
        return wall || !ground;
    }

    private void FaceTowards(float worldX)
    {
        float sign = Mathf.Sign(worldX - transform.position.x);
        Vector3 scale = transform.localScale;
        // Escala positiva = mira a la izquierda (orientación original del sprite)
        scale.x = Mathf.Abs(scale.x) * (sign < 0f ? 1f : -1f);
        transform.localScale = scale;
    }

    private void StopHorizontal()
    {
        rb.linearVelocity = new Vector2(0f, isFlying ? 0f : rb.linearVelocity.y);
    }

    private void Play(int state)
    {
        if (animator != null) animator.Play(state, 0, 0f);
    }

    private void Shake(float force)
    {
        if (impulse != null) impulse.GenerateImpulseWithForce(force);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, activationRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
}
