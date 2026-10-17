using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla el flujo entre escenas reaccionando únicamente a GameEvents.
/// </summary>
public class SceneFlowController : MonoBehaviour
{
    [SerializeField] private float restartDelay = 2f; // Tiempo para mostrar el Game Over antes de reiniciar

    private bool isLoading;

    private void OnEnable()
    {
        GameEvents.OnPlayerDeath += HandlePlayerDeath;
        GameEvents.OnLevelCompleted += HandleLevelCompleted;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDeath -= HandlePlayerDeath;
        GameEvents.OnLevelCompleted -= HandleLevelCompleted;
    }

    private void HandlePlayerDeath()
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        StartCoroutine(LoadSceneAfterDelay(currentIndex, restartDelay));
    }

    private void HandleLevelCompleted()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log("No hay más niveles en Build Settings. ¡Juego completado!");
            return;
        }

        StartCoroutine(LoadSceneAfterDelay(nextIndex, 0f));
    }

    private IEnumerator LoadSceneAfterDelay(int buildIndex, float delay)
    {
        if (isLoading) yield break;
        isLoading = true;

        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(buildIndex);
    }
}
