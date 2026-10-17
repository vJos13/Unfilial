using UnityEngine;

/// <summary>
/// Orbe recolectable. Flota suavemente y suma orbes al GameManager al tocarlo.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class OrbPickup : MonoBehaviour
{
    [SerializeField] private int value = 1;

    [Header("Animation")]
    [SerializeField] private float bobAmplitude = 0.12f;
    [SerializeField] private float bobSpeed = 3f;

    private Vector3 startPosition;
    private float phase;
    private bool collected;

    private void Start()
    {
        startPosition = transform.position;
        phase = startPosition.x * 0.7f; // Desfase para que no floten todos sincronizados
    }

    private void Update()
    {
        transform.position = startPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobAmplitude);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collected || !collision.CompareTag("Player")) return;

        collected = true;
        GameEvents.OnPlaySfx?.Invoke(SfxType.OrbPickup);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddOrbs(value);
        }

        Destroy(gameObject);
    }
}
