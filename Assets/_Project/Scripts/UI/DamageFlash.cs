using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Parpadeo rojo a pantalla completa cuando el jugador pierde vida.
/// </summary>
[RequireComponent(typeof(Image))]
public class DamageFlash : MonoBehaviour
{
    [SerializeField] private float peakAlpha = 0.4f;
    [SerializeField] private float duration = 0.3f;

    private Image flashImage;
    private int lastHealth = int.MaxValue;
    private Coroutine flashRoutine;

    private void Awake()
    {
        flashImage = GetComponent<Image>();
        flashImage.raycastTarget = false;
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        GameEvents.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        // Solo parpadea si la vida bajó (no al iniciar ni al curarse)
        if (currentHealth < lastHealth && lastHealth != int.MaxValue)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        lastHealth = currentHealth;
    }

    private IEnumerator FlashRoutine()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Subida instantánea y caída suave
            float t = elapsed / duration;
            SetAlpha(peakAlpha * (1f - t) * (1f - t));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SetAlpha(0f);
    }

    private void SetAlpha(float alpha)
    {
        Color color = flashImage.color;
        color.a = alpha;
        flashImage.color = color;
    }
}
