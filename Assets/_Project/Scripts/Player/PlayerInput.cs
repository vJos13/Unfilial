using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool DashTriggered { get; private set; } // Nueva variable para el Dash
    public bool AttackTriggered { get; private set; }
    public bool BlockHeld { get; private set; } // El escudo se mantiene mientras se pulsa

    private GameInputActions inputActions;

    private void Awake()
    {
        inputActions = new GameInputActions();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Jump.performed += OnJump;
        inputActions.Player.Dash.performed += OnDash; // Suscribimos el Dash
        inputActions.Player.Attack.performed += OnAttack;
        GameEvents.OnGamePaused += HandlePause;
    }

    private void OnDisable()
    {
        inputActions.Player.Jump.performed -= OnJump;
        inputActions.Player.Dash.performed -= OnDash; // Desuscribimos
        inputActions.Player.Attack.performed -= OnAttack;
        GameEvents.OnGamePaused -= HandlePause;
        inputActions.Player.Disable();
    }

    private void Update()
    {
        MoveInput = inputActions.Player.Move.ReadValue<Vector2>();
        BlockHeld = inputActions.Player.Block.IsPressed();
    }

    private void HandlePause(bool isPaused)
    {
        // Mientras el menú de pausa está abierto, los clics no deben llegar al personaje
        if (isPaused)
        {
            inputActions.Player.Disable();
            MoveInput = Vector2.zero;
            BlockHeld = false;
            ConsumeJump();
            ConsumeDash();
            ConsumeAttack();
        }
        else
        {
            inputActions.Player.Enable();
        }
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        JumpTriggered = true;
    }

    private void OnDash(InputAction.CallbackContext context)
    {
        DashTriggered = true;
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        AttackTriggered = true;
    }

    public void ConsumeJump() => JumpTriggered = false;
    public void ConsumeDash() => DashTriggered = false; // Consumir el Dash
    public void ConsumeAttack() => AttackTriggered = false;
}