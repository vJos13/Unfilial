using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool DashTriggered { get; private set; } // Nueva variable para el Dash

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
    }

    private void OnDisable()
    {
        inputActions.Player.Jump.performed -= OnJump;
        inputActions.Player.Dash.performed -= OnDash; // Desuscribimos
        inputActions.Player.Disable();
    }

    private void Update()
    {
        MoveInput = inputActions.Player.Move.ReadValue<Vector2>();
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        JumpTriggered = true;
    }

    private void OnDash(InputAction.CallbackContext context)
    {
        DashTriggered = true;
    }

    public void ConsumeJump() => JumpTriggered = false;
    public void ConsumeDash() => DashTriggered = false; // Consumir el Dash
}