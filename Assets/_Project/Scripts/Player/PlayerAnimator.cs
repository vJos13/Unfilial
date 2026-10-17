using UnityEngine;

/// <summary>
/// Alimenta la FSM del Animator: Speed, IsGrounded, yVelocity (floats/bool continuos)
/// y Attack / Hurt (triggers de un solo pulso).
/// </summary>
[RequireComponent(typeof(Animator), typeof(Rigidbody2D))]
public class PlayerAnimator : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int YVelocityHash = Animator.StringToHash("yVelocity");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HurtHash = Animator.StringToHash("Hurt");

    private Animator animator;
    private Rigidbody2D rb;
    private PlayerMovement movement;
    private PlayerAttack attack;
    private PlayerHealth health;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement>();
        attack = GetComponent<PlayerAttack>();
        health = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (attack != null) attack.Attacked += HandleAttack;
        if (health != null) health.Damaged += HandleHurt;
    }

    private void OnDisable()
    {
        if (attack != null) attack.Attacked -= HandleAttack;
        if (health != null) health.Damaged -= HandleHurt;
    }

    private void Update()
    {
        animator.SetFloat(SpeedHash, Mathf.Abs(rb.linearVelocity.x));
        animator.SetFloat(YVelocityHash, rb.linearVelocity.y);
        animator.SetBool(IsGroundedHash, movement == null || movement.IsGrounded);
    }

    private void HandleAttack(Vector2 direction) => animator.SetTrigger(AttackHash);

    private void HandleHurt() => animator.SetTrigger(HurtHash);
}
