using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 100% event-driven: no usa Update(), solo reacciona a GameEvents.
/// Singleton de escena: punto único de acceso a la interfaz (UIManager.Instance).
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("HUD")]
    [SerializeField] private Image healthFill; // Image Type = Filled, Horizontal, origen Left
    [SerializeField] private TextMeshProUGUI orbsText;
    [SerializeField] private TextMeshProUGUI powerText;

    [Header("Mensajes")]
    [SerializeField] private TextMeshProUGUI levelTitleText; // Nombre del nivel al empezar
    [SerializeField] private TextMeshProUGUI toastText;      // Avisos cortos (subida de poder)
    [SerializeField] private float titleHoldTime = 2f;
    [SerializeField] private float fadeTime = 1f;

    [Header("Jefe")]
    [SerializeField] private GameObject bossBar;
    [SerializeField] private Image bossHealthFill;
    [SerializeField] private TextMeshProUGUI bossNameText;

    [Header("Paneles")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject levelCompletePanel;

    private Coroutine toastRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        GameEvents.OnHealthChanged += UpdateHealth;
        GameEvents.OnOrbsChanged += UpdateOrbs;
        GameEvents.OnPowerLevelChanged += HandlePowerLevelChanged;
        GameEvents.OnPlayerDeath += ShowGameOver;
        GameEvents.OnLevelCompleted += ShowLevelComplete;
        GameEvents.OnBossEncounterStarted += ShowBossBar;
        GameEvents.OnBossHealthChanged += UpdateBossHealth;
        GameEvents.OnBossDefeated += HideBossBar;
    }

    private void OnDisable()
    {
        GameEvents.OnHealthChanged -= UpdateHealth;
        GameEvents.OnOrbsChanged -= UpdateOrbs;
        GameEvents.OnPowerLevelChanged -= HandlePowerLevelChanged;
        GameEvents.OnPlayerDeath -= ShowGameOver;
        GameEvents.OnLevelCompleted -= ShowLevelComplete;
        GameEvents.OnBossEncounterStarted -= ShowBossBar;
        GameEvents.OnBossHealthChanged -= UpdateBossHealth;
        GameEvents.OnBossDefeated -= HideBossBar;
    }

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        if (toastText != null) toastText.alpha = 0f;
        if (bossBar != null) bossBar.SetActive(false);

        // Los orbes persisten entre escenas (GameManager es DontDestroyOnLoad),
        // así que sincronizamos el valor inicial al cargar.
        bool hasManager = GameManager.Instance != null;
        UpdateOrbs(hasManager ? GameManager.Instance.CurrentOrbs : 0);
        UpdatePower(hasManager ? GameManager.Instance.PowerLevel : 0);

        if (levelTitleText != null) StartCoroutine(FadeOut(levelTitleText, titleHoldTime));
    }

    private void UpdateHealth(int currentHealth, int maxHealth)
    {
        if (healthFill == null) return;

        healthFill.fillAmount = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    }

    private void UpdateOrbs(int orbs)
    {
        if (orbsText == null) return;

        orbsText.text = $"Orbes: {orbs}";
    }

    private void HandlePowerLevelChanged(int powerLevel)
    {
        UpdatePower(powerLevel);
        ShowToast(powerLevel > 0 ? "¡Tu poder divino aumenta!" : "Tu poder se debilita...");
    }

    private void UpdatePower(int powerLevel)
    {
        if (powerText == null) return;

        powerText.text = $"Poder Nv. {powerLevel + 1}";
    }

    public void ShowToast(string message)
    {
        if (toastText == null) return;

        toastText.text = message;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(FadeOut(toastText, 1.2f));
    }

    private IEnumerator FadeOut(TextMeshProUGUI text, float holdTime)
    {
        text.alpha = 1f;
        yield return new WaitForSecondsRealtime(holdTime);

        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            text.alpha = 1f - elapsed / fadeTime;
            yield return null;
        }

        text.alpha = 0f;
    }

    private void ShowBossBar(string bossName)
    {
        if (bossNameText != null) bossNameText.text = bossName.ToUpperInvariant();
        if (bossBar != null) bossBar.SetActive(true);
    }

    private void UpdateBossHealth(int currentHealth, int maxHealth)
    {
        if (bossHealthFill != null) bossHealthFill.fillAmount = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    }

    private void HideBossBar()
    {
        if (bossBar != null) bossBar.SetActive(false);
        ShowToast("¡Jefe derrotado!");
    }

    private void ShowGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    private void ShowLevelComplete()
    {
        if (levelCompletePanel != null) levelCompletePanel.SetActive(true);
    }
}
