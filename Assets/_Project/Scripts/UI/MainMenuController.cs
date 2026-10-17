using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menú de inicio: Jugar, Menú (selección de nivel), Sonido y Salir.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject levelsPanel;
    [SerializeField] private GameObject soundPanel;

    [Header("Botones principales")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button levelsButton;
    [SerializeField] private Button soundButton;
    [SerializeField] private Button quitButton;

    [Header("Selección de nivel")]
    [SerializeField] private Button[] levelButtons;
    [SerializeField] private string[] levelScenes = { "Level_01", "Level_02", "Level_03" };
    [SerializeField] private Button levelsBackButton;

    [Header("Sonido")]
    [SerializeField] private Button soundBackButton;

    private void Awake()
    {
        playButton.onClick.AddListener(() => LoadLevel(0));
        levelsButton.onClick.AddListener(() => ShowPanel(levelsPanel, levelButtons[0]));
        soundButton.onClick.AddListener(() => ShowPanel(soundPanel, soundBackButton));
        quitButton.onClick.AddListener(Quit);
        levelsBackButton.onClick.AddListener(() => ShowPanel(mainPanel, levelsButton));
        soundBackButton.onClick.AddListener(() => ShowPanel(mainPanel, soundButton));

        for (int i = 0; i < levelButtons.Length; i++)
        {
            int levelIndex = i; // Copia local para la lambda
            levelButtons[i].onClick.AddListener(() => LoadLevel(levelIndex));
        }
    }

    private void Start()
    {
        Time.timeScale = 1f;
        ShowPanel(mainPanel, playButton, playSound: false);
    }

    private void ShowPanel(GameObject panel, Button selected, bool playSound = true)
    {
        if (playSound) GameEvents.OnPlaySfx?.Invoke(SfxType.UIClick);

        mainPanel.SetActive(panel == mainPanel);
        levelsPanel.SetActive(panel == levelsPanel);
        soundPanel.SetActive(panel == soundPanel);

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selected.gameObject);
    }

    private void LoadLevel(int index)
    {
        if (index < 0 || index >= levelScenes.Length) return;

        GameEvents.OnPlaySfx?.Invoke(SfxType.UIClick);
        SceneManager.LoadScene(levelScenes[index]);
    }

    private void Quit()
    {
        GameEvents.OnPlaySfx?.Invoke(SfxType.UIClick);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
