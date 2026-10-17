using System;
using UnityEngine;
using System.Collections; // Necesario para las Corrutinas

[RequireComponent(typeof(Rigidbody2D), typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpForce = 24f; // Tu salto ajustado
    [SerializeField] private int maxJumps = 2;

    [Header("Game Feel")]
    [SerializeField] private float fallGravityMultiplier = 2.5f;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 24f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Ground Detection")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    // Otros módulos (p. ej. el escudo) pueden ralentizar al jugador sin conocer sus físicas
    public float SpeedMultiplier { get; set; } = 1f;
    public bool IsFacingRight => isFacingRight;
    public bool IsGrounded { get; private set; }

    // Eventos locales: audio, animación y feedback reaccionan sin conocer las físicas
    public event Action Jumped;
    public event Action Dashed;

    private Rigidbody2D rb;
    private PlayerInput playerInput;
    
    private bool isFacingRight = true;
    private int jumpsRemaining;
    private float defaultGravity;

    // Estados del Dash
    private bool isDashing;
    private bool canDash = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();
        defaultGravity = rb.gravityScale;
    }

    private void Update()
    {
        if (isDashing) return; // Si hace dash, no puede girar ni hacer otras cosas

        HandleFlip();
        CheckDashExecution();
    }

    private void FixedUpdate()
    {
        if (isDashing) return; // Si hace dash, las físicas normales se ignoran

        ApplyMovement();
        CheckAndApplyJump();
        ApplyDynamicGravity();
    }

    private void CheckDashExecution()
    {
        if (playerInput.DashTriggered && canDash)
        {
            playerInput.ConsumeDash();
            StartCoroutine(DashRoutine());
            Dashed?.Invoke();
        }
        else if (playerInput.DashTriggered && !canDash)
        {
            playerInput.ConsumeDash(); // Lo consumimos si está en cooldown para que no se guarde
        }
    }

    private IEnumerator DashRoutine()
    {
        canDash = false;
        isDashing = true;

        // Guardamos la gravedad y la apagamos para un vuelo recto
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        // Disparamos al personaje hacia donde mira
        float dashDirection = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);

        // Esperamos la duración del dash (ej: 0.2s)
        yield return new WaitForSeconds(dashDuration);

        // Restauramos el estado normal
        rb.gravityScale = originalGravity;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // Frenamos en seco el impulso horizontal
        isDashing = false;

        // Esperamos el tiempo de enfriamiento (ej: 1s)
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private void ApplyMovement()
    {
        rb.linearVelocity = new Vector2(playerInput.MoveInput.x * moveSpeed * SpeedMultiplier, rb.linearVelocity.y);
    }

    private void CheckAndApplyJump()
    {
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (IsGrounded && rb.linearVelocity.y <= 0.1f)
        {
            jumpsRemaining = maxJumps;
        }

        if (playerInput.JumpTriggered && jumpsRemaining > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpsRemaining--;
            playerInput.ConsumeJump();
            Jumped?.Invoke();
        }
        else if (playerInput.JumpTriggered && jumpsRemaining == 0)
        {
            playerInput.ConsumeJump();
        }
    }

    private void ApplyDynamicGravity()
    {
        if (rb.linearVelocity.y < 0f)
        {
            rb.gravityScale = defaultGravity * fallGravityMultiplier;
        }
        else
        {
            rb.gravityScale = defaultGravity;
        }
    }

    private void HandleFlip()
    {
        if (playerInput.MoveInput.x > 0 && !isFacingRight)
            Flip();
        else if (playerInput.MoveInput.x < 0 && isFacingRight)
            Flip();
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
    }
}