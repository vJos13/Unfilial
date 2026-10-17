using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Screen shake: emite impulsos de Cinemachine al recibir daño y al conectar un golpe.
/// La CM vcam1 lleva un CinemachineImpulseListener que los recibe.
/// </summary>
[RequireComponent(typeof(CinemachineImpulseSource))]
public class PlayerCameraShake : MonoBehaviour
{
    [SerializeField] private float hurtForce = 1f;
    [SerializeField] private float hitForce = 0.35f;

    private CinemachineImpulseSource impulseSource;
    private PlayerHealth health;
    private PlayerAttack attack;

    private void Awake()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        health = GetComponent<PlayerHealth>();
        attack = GetComponent<PlayerAttack>();
    }

    private void OnEnable()
    {
        if (health != null) health.Damaged += ShakeHurt;
        if (attack != null) attack.HitLanded += ShakeHit;
    }

    private void OnDisable()
    {
        if (health != null) health.Damaged -= ShakeHurt;
        if (attack != null) attack.HitLanded -= ShakeHit;
    }

    private void ShakeHurt() => impulseSource.GenerateImpulseWithForce(hurtForce);

    private void ShakeHit() => impulseSource.GenerateImpulseWithForce(hitForce);
}
