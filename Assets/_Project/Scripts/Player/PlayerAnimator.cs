using UnityEngine;

/// <summary>
/// Alimenta la FSM del Animator: Speed, IsGrounded, yVelocity e IsBlocking (continuos),
/// AttackDir (int: 1 arriba, 0 frente, -1 abajo) y los triggers Attack / Hurt / Dash / Death.
/// </summary>
[RequireComponent(typeof(Animator), typeof(Rigidbody2D))]
public class PlayerAnimator : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int YVelocityHash = Animator.StringToHash("yVelocity");
    private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking");
    private static readonly int AttackDirHash = Animator.StringToHash("AttackDir");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HurtHash = Animator.StringToHash("Hurt");
    private static readonly int DashHash = Animator.StringToHash("Dash");
    private static readonly int DeathHash = Animator.StringToHash("Death");

    private Animator animator;
    private Rigidbody2D rb;
    private PlayerMovement movement;
    private PlayerAttack attack;
    private PlayerHealth health;
    private PlayerShield shield;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement>();
        attack = GetComponent<PlayerAttack>();
        health = GetComponent<PlayerHealth>();
        shield = GetComponent<PlayerShield>();
    }

    private void OnEnable()
    {
        if (attack != null) attack.Attacked += HandleAttack;
        if (health != null) health.Damaged += HandleHurt;
        if (movement != null) movement.Dashed += HandleDash;
        GameEvents.OnPlayerDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (attack != null) attack.Attacked -= HandleAttack;
        if (health != null) health.Damaged -= HandleHurt;
        if (movement != null) movement.Dashed -= HandleDash;
        GameEvents.OnPlayerDeath -= HandleDeath;
    }

    private void Update()
    {
        animator.SetFloat(SpeedHash, Mathf.Abs(rb.linearVelocity.x));
        animator.SetFloat(YVelocityHash, rb.linearVelocity.y);
        animator.SetBool(IsGroundedHash, movement == null || movement.IsGrounded);
        animator.SetBool(IsBlockingHash, shield != null && shield.IsBlocking);
    }

    private void HandleAttack(Vector2 direction)
    {
        animator.SetInteger(AttackDirHash, Mathf.RoundToInt(direction.y));
        animator.SetTrigger(AttackHash);
    }

    private void HandleHurt() => animator.SetTrigger(HurtHash);

    private void HandleDash() => animator.SetTrigger(DashHash);

    private void HandleDeath()
    {
        animator.ResetTrigger(HurtHash);
        animator.SetTrigger(DeathHash);
    }
}
