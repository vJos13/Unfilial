using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    public int CurrentOrbs { get; private set; }

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
    }

    private void OnDisable()
    {
        // Regla de oro: siempre desuscribirse para evitar memory leaks
        GameEvents.OnPlayerDeath -= HandleGameOver;
    }

    public void AddOrbs(int amount)
    {
        CurrentOrbs += amount;
        GameEvents.OnOrbsChanged?.Invoke(CurrentOrbs);
    }

    private void HandleGameOver()
    {
        Debug.Log("El jugador ha muerto. Activando Game Over...");
        // Más adelante aquí detendremos el tiempo y mostraremos la UI de derrota
    }
}