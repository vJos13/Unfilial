using UnityEngine;

/// <summary>
/// Sella la salida del nivel hasta que el jefe es derrotado (escucha GameEvents.OnBossDefeated).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ExitGate : MonoBehaviour
{
    [SerializeField] private Color sealedColor = new Color(0.35f, 0.35f, 0.45f, 0.6f);

    private Collider2D trigger;
    private SpriteRenderer spriteRenderer;
    private Color openColor;

    private void Awake()
    {
        trigger = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) openColor = spriteRenderer.color;
        SetOpen(false);
    }

    private void OnEnable()
    {
        GameEvents.OnBossDefeated += Open;
    }

    private void OnDisable()
    {
        GameEvents.OnBossDefeated -= Open;
    }

    private void Open()
    {
        SetOpen(true);
        if (UIManager.Instance != null) UIManager.Instance.ShowToast("¡El portal se ha abierto!");
    }

    private void SetOpen(bool open)
    {
        trigger.enabled = open;
        if (spriteRenderer != null) spriteRenderer.color = open ? openColor : sealedColor;
    }
}
