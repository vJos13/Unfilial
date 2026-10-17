using System.Collections;
using UnityEngine;

/// <summary>
/// Efecto visual del tajo: aparece en el punto de ataque, orientado según la dirección, y se desvanece.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SlashEffect : MonoBehaviour
{
    [SerializeField] private float duration = 0.12f;

    private SpriteRenderer spriteRenderer;
    private Color baseColor;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseColor = spriteRenderer.color;
        spriteRenderer.enabled = false;
    }

    public void Play(Vector2 worldPosition, Vector2 direction)
    {
        transform.position = worldPosition;

        // Rotación local: el Flip del padre (escala X negativa) ya refleja el tajo horizontal
        float angle = direction == Vector2.up ? 90f : direction == Vector2.down ? -90f : 0f;
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        StopAllCoroutines();
        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        spriteRenderer.enabled = true;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - t * t));
            transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.1f, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        spriteRenderer.enabled = false;
    }
}
