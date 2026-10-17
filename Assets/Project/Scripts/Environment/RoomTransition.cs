using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Trigger entre salas: cambia a la cámara de la sala destino, o completa el nivel
/// (el SceneFlowController escucha OnLevelCompleted y carga la siguiente escena).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RoomTransition : MonoBehaviour
{
    private enum TransitionMode { SwitchCamera, CompleteLevel }

    [SerializeField] private TransitionMode mode = TransitionMode.SwitchCamera;
    [SerializeField] private CinemachineCamera targetRoomCamera;

    private bool levelCompleted;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        switch (mode)
        {
            case TransitionMode.SwitchCamera:
                if (targetRoomCamera != null) targetRoomCamera.Prioritize();
                break;

            case TransitionMode.CompleteLevel:
                if (levelCompleted) return;
                levelCompleted = true;
                GameEvents.OnLevelCompleted?.Invoke();
                break;
        }
    }
}
