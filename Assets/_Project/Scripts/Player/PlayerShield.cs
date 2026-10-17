using System;
using UnityEngine;

/// <summary>
/// Escudo frontal: se mantiene pulsado, bloquea golpes de frente y tiene cargas limitadas.
/// Si se agotan, se rompe durante unos segundos (evita que sea invencible).
/// </summary>
[RequireComponent(typeof(PlayerInput), typeof(PlayerMovement))]
public class PlayerShield : MonoBehaviour
{
    [Header("Shield Settings")]
    [SerializeField] private int maxCharges = 3;
    [SerializeField] private float rechargeTime = 1.5f;  // Segundos para recuperar una carga sin bloquear
    [SerializeField] private float brokenDuration = 2.5f;
    [SerializeField] private float blockingSpeedMultiplier = 0.4f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer shieldVisual;
    [SerializeField] private Color fullColor = new Color(0.55f, 0.85f, 1f, 0.85f);
    [SerializeField] private Color weakColor = new Color(1f, 0.45f, 0.35f, 0.6f);

    public bool IsBlocking { get; private set; }

    public event Action Blocked;
    public event Action Broken;

    private PlayerInput playerInput;
    private PlayerMovement playerMovement;
    private int charges;
    private float rechargeTimer;
    private float brokenTimer;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        playerMovement = GetComponent<PlayerMovement>();
        charges = maxCharges;
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerDeath += HandleDeath;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDeath -= HandleDeath;
        IsBlocking = false;
        if (playerMovement != null) playerMovement.SpeedMultiplier = 1f;
        if (shieldVisual != null) shieldVisual.enabled = false;
    }

    private void Update()
    {
        if (brokenTimer > 0f)
        {
            brokenTimer -= Time.deltaTime;
            if (brokenTimer <= 0f) charges = maxCharges;
        }

        IsBlocking = playerInput.BlockHeld && brokenTimer <= 0f;
        playerMovement.SpeedMultiplier = IsBlocking ? blockingSpeedMultiplier : 1f;

        if (!IsBlocking && brokenTimer <= 0f && charges < maxCharges)
        {
            rechargeTimer += Time.deltaTime;
            if (rechargeTimer >= rechargeTime)
            {
                rechargeTimer = 0f;
                charges++;
            }
        }

        UpdateVisual();
    }

    /// <summary>
    /// Devuelve true si el golpe que viene desde sourcePosition fue bloqueado.
    /// </summary>
    public bool TryBlock(Vector2 sourcePosition)
    {
        if (!IsBlocking) return false;

        // Solo bloquea lo que viene de frente
        float facingSign = playerMovement.IsFacingRight ? 1f : -1f;
        if ((sourcePosition.x - transform.position.x) * facingSign < -0.1f) return false;

        charges--;
        rechargeTimer = 0f;

        if (charges <= 0)
        {
            brokenTimer = brokenDuration;
            IsBlocking = false;
            Broken?.Invoke();
        }
        else
        {
            Blocked?.Invoke();
        }

        return true;
    }

    private void UpdateVisual()
    {
        if (shieldVisual == null) return;

        shieldVisual.enabled = IsBlocking;
        float strength = maxCharges > 1 ? (charges - 1f) / (maxCharges - 1f) : 1f;
        shieldVisual.color = Color.Lerp(weakColor, fullColor, strength);
    }

    private void HandleDeath()
    {
        enabled = false;
    }
}
