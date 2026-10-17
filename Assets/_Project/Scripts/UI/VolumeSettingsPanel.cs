using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de sonido reutilizable (menú principal y pausa): Música y SFX.
/// </summary>
public class VolumeSettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TextMeshProUGUI musicValueText;
    [SerializeField] private TextMeshProUGUI sfxValueText;

    private void Awake()
    {
        musicSlider.onValueChanged.AddListener(OnMusicChanged);
        sfxSlider.onValueChanged.AddListener(OnSfxChanged);
    }

    private void OnEnable()
    {
        // Sin notificar para no disparar guardados al abrir el panel
        musicSlider.SetValueWithoutNotify(VolumeSettings.Music);
        sfxSlider.SetValueWithoutNotify(VolumeSettings.Sfx);
        RefreshLabels();
    }

    private void OnDisable()
    {
        VolumeSettings.Save();
    }

    private void OnMusicChanged(float value)
    {
        VolumeSettings.SetMusic(value);
        RefreshLabels();
    }

    private void OnSfxChanged(float value)
    {
        VolumeSettings.SetSfx(value);
        RefreshLabels();
        GameEvents.OnPlaySfx?.Invoke(SfxType.UIClick); // Previsualiza el volumen de efectos
    }

    private void RefreshLabels()
    {
        if (musicValueText != null) musicValueText.text = $"{Mathf.RoundToInt(musicSlider.value * 100f)}%";
        if (sfxValueText != null) sfxValueText.text = $"{Mathf.RoundToInt(sfxSlider.value * 100f)}%";
    }
}
