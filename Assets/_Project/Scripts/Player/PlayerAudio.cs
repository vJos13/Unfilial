using UnityEngine;

/// <summary>
/// SRP: único módulo del jugador que sabe de sonido. Traduce los eventos locales
/// de los demás módulos a peticiones globales de SFX (el AudioManager las reproduce).
/// </summary>
public class PlayerAudio : MonoBehaviour
{
    private PlayerMovement movement;
    private PlayerAttack attack;
    private PlayerHealth health;
    private PlayerShield shield;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        attack = GetComponent<PlayerAttack>();
        health = GetComponent<PlayerHealth>();
        shield = GetComponent<PlayerShield>();
    }

    private void OnEnable()
    {
        if (movement != null) { movement.Jumped += PlayJump; movement.Dashed += PlayDash; }
        if (attack != null) attack.Attacked += PlayAttack;
        if (health != null) health.Damaged += PlayHurt;
        if (shield != null) { shield.Blocked += PlayBlock; shield.Broken += PlayShieldBreak; }
    }

    private void OnDisable()
    {
        if (movement != null) { movement.Jumped -= PlayJump; movement.Dashed -= PlayDash; }
        if (attack != null) attack.Attacked -= PlayAttack;
        if (health != null) health.Damaged -= PlayHurt;
        if (shield != null) { shield.Blocked -= PlayBlock; shield.Broken -= PlayShieldBreak; }
    }

    private static void Play(SfxType type) => GameEvents.OnPlaySfx?.Invoke(type);

    private void PlayJump() => Play(SfxType.Jump);
    private void PlayDash() => Play(SfxType.Dash);
    private void PlayAttack(Vector2 direction) => Play(SfxType.Attack);
    private void PlayHurt() => Play(SfxType.PlayerHurt);
    private void PlayBlock() => Play(SfxType.Block);
    private void PlayShieldBreak() => Play(SfxType.ShieldBreak);
}
