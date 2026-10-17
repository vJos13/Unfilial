using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Progression")]
    [SerializeField] private int orbsPerPowerLevel = 15; // Cada X orbes el semidiós aumenta su daño
    [SerializeField] private int mainMenuBuildIndex = 0;

    public int CurrentOrbs { get; private set; }
    public int PowerLevel => CurrentOrbs / orbsPerPowerLevel;

    // Orbes al entrar al nivel: al morir se restauran para que no se puedan "farmear"
    private int orbsAtLevelStart;

    private void Awake()
    {
        // Patrón Singleton: asegura que solo exista un GameManager
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Sobrevive al cambio de escenas
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerDeath += HandleGameOver;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        // Regla de oro: siempre desuscribirse para evitar memory leaks
        GameEvents.OnPlayerDeath -= HandleGameOver;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void AddOrbs(int amount)
    {
        SetOrbs(CurrentOrbs + amount);
    }

    private void SetOrbs(int amount)
    {
        int previousPowerLevel = PowerLevel;

        CurrentOrbs = amount;
        GameEvents.OnOrbsChanged?.Invoke(CurrentOrbs);

        if (PowerLevel != previousPowerLevel)
        {
            GameEvents.OnPowerLevelChanged?.Invoke(PowerLevel);
            if (PowerLevel > previousPowerLevel) GameEvents.OnPlaySfx?.Invoke(SfxType.PowerUp);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Volver al menú principal termina la partida
        if (scene.buildIndex == mainMenuBuildIndex)
        {
            SetOrbs(0);
        }

        orbsAtLevelStart = CurrentOrbs;
    }

    private void HandleGameOver()
    {
        Debug.Log("El jugador ha muerto. Activando Game Over...");
        SetOrbs(orbsAtLevelStart);
    }
}
