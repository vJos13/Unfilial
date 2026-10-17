using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menú de pausa (Esc): Seguir Jugando, Sonido, Salir al menú principal.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject soundPanel;

    [Header("Botones")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button soundButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button soundBackButton;

    [SerializeField] private string mainMenuScene = "MainMenu";

    private GameInputActions inputActions;
    private bool isPaused;
    private bool canPause = true; // Sin pausa tras morir o completar el nivel

    private void Awake()
    {
        inputActions = new GameInputActions();

        resumeButton.onClick.AddListener(Resume);
        soundButton.onClick.AddListener(OpenSound);
        quitButton.onClick.AddListener(QuitToMainMenu);
        soundBackButton.onClick.AddListener(CloseSound);
    }

    private void OnEnable()
    {
        // Solo habilitamos la acción de pausa: este componente no debe leer el resto del input
        inputActions.Player.Pause.Enable();
        inputActions.Player.Pause.performed += OnPausePressed;
        GameEvents.OnPlayerDeath += DisablePause;
        GameEvents.OnLevelCompleted += DisablePause;
    }

    private void OnDisable()
    {
        inputActions.Player.Pause.performed -= OnPausePressed;
        inputActions.Player.Pause.Disable();
        GameEvents.OnPlayerDeath -= DisablePause;
        GameEvents.OnLevelCompleted -= DisablePause;
    }

    private void OnDestroy()
    {
        inputActions.Dispose();
        if (isPaused) Time.timeScale = 1f;
    }

    private void Start()
    {
        pausePanel.SetActive(false);
        soundPanel.SetActive(false);
    }

    private void OnPausePressed(InputAction.CallbackContext context)
    {
        if (!isPaused)
        {
            Pause();
        }
        else if (soundPanel.activeSelf)
        {
            CloseSound(); // Esc dentro de Sonido vuelve al menú de pausa
        }
        else
        {
            Resume();
        }
    }

    private void Pause()
    {
        if (!canPause) return;

        isPaused = true;
        Time.timeScale = 0f;
        pausePanel.SetActive(true);
        soundPanel.SetActive(false);
        Select(resumeButton);
        GameEvents.OnGamePaused?.Invoke(true);
    }

    private void Resume()
    {
        PlayClick();
        isPaused = false;
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
        soundPanel.SetActive(false);
        GameEvents.OnGamePaused?.Invoke(false);
    }

    private void OpenSound()
    {
        PlayClick();
        pausePanel.SetActive(false);
        soundPanel.SetActive(true);
        Select(soundBackButton);
    }

    private void CloseSound()
    {
        PlayClick();
        soundPanel.SetActive(false);
        pausePanel.SetActive(true);
        Select(soundButton);
    }

    private void QuitToMainMenu()
    {
        PlayClick();
        isPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    private void DisablePause()
    {
        canPause = false;
    }

    private static void Select(Button button)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private static void PlayClick() => GameEvents.OnPlaySfx?.Invoke(SfxType.UIClick);
}
