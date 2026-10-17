using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 100% event-driven: no usa Update(), solo reacciona a GameEvents.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI orbsText;

    [Header("Game Over")]
    [SerializeField] private GameObject gameOverPanel;

    private void OnEnable()
    {
        GameEvents.OnHealthChanged += UpdateHealth;
        GameEvents.OnOrbsChanged += UpdateOrbs;
        GameEvents.OnPlayerDeath += ShowGameOver;
    }

    private void OnDisable()
    {
        GameEvents.OnHealthChanged -= UpdateHealth;
        GameEvents.OnOrbsChanged -= UpdateOrbs;
        GameEvents.OnPlayerDeath -= ShowGameOver;
    }

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // Los orbes persisten entre escenas (GameManager es DontDestroyOnLoad),
        // así que sincronizamos el valor inicial al cargar.
        UpdateOrbs(GameManager.Instance != null ? GameManager.Instance.CurrentOrbs : 0);
    }

    private void UpdateHealth(int currentHealth, int maxHealth)
    {
        if (healthSlider == null) return;

        healthSlider.maxValue = maxHealth;
        healthSlider.value = currentHealth;
    }

    private void UpdateOrbs(int orbs)
    {
        if (orbsText == null) return;

        orbsText.text = $"Orbes: {orbs}";
    }

    private void ShowGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }
}
