using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla el flujo entre escenas reaccionando únicamente a GameEvents.
/// </summary>
public class SceneFlowController : MonoBehaviour
{
    [SerializeField] private float restartDelay = 2f; // Tiempo para mostrar el Game Over antes de reiniciar
    [SerializeField] private float levelCompleteDelay = 2f; // Tiempo para mostrar "Nivel completado"
    [SerializeField] private int mainMenuBuildIndex = 0;

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
            // Último nivel superado: volvemos al menú principal
            nextIndex = mainMenuBuildIndex;
        }

        StartCoroutine(LoadSceneAfterDelay(nextIndex, levelCompleteDelay));
    }

    private IEnumerator LoadSceneAfterDelay(int buildIndex, float delay)
    {
        if (isLoading) yield break;
        isLoading = true;

        yield return new WaitForSecondsRealtime(delay);
        Time.timeScale = 1f;
        SceneManager.LoadScene(buildIndex);
    }
}
