using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

/// <summary>
/// Singleton de audio persistente. Reproduce música y SFX reaccionando solo a GameEvents.
/// El volumen se controla con los parámetros expuestos del MainMixer (MusicVol / SFXVol).
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Serializable]
    private struct SfxEntry
    {
        public SfxType type;
        public AudioClip clip;
    }

    public static AudioManager Instance { get; private set; }

    private const string MusicVolumeParam = "MusicVol";
    private const string SfxVolumeParam = "SFXVol";

    [Header("Mixer")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Clips")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private SfxEntry[] sfxClips;

    [Header("Variación")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.1f);

    private AudioSource musicSource;
    private AudioSource sfxSource;
    private readonly Dictionary<SfxType, AudioClip> clipLookup = new Dictionary<SfxType, AudioClip>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.outputAudioMixerGroup = musicGroup;
        musicSource.clip = musicClip;
        musicSource.loop = true;
        musicSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.outputAudioMixerGroup = sfxGroup;
        sfxSource.playOnAwake = false;

        foreach (SfxEntry entry in sfxClips)
        {
            if (entry.clip != null) clipLookup[entry.type] = entry.clip;
        }
    }

    private void Start()
    {
        // AudioMixer.SetFloat no tiene efecto en Awake: se aplica a partir de Start
        ApplyVolumes();
        if (musicSource.clip != null) musicSource.Play();
    }

    private void OnEnable()
    {
        GameEvents.OnPlaySfx += PlaySfx;
        GameEvents.OnVolumeChanged += ApplyVolumes;
        GameEvents.OnPlayerDeath += HandlePlayerDeath;
        GameEvents.OnLevelCompleted += HandleLevelCompleted;
    }

    private void OnDisable()
    {
        GameEvents.OnPlaySfx -= PlaySfx;
        GameEvents.OnVolumeChanged -= ApplyVolumes;
        GameEvents.OnPlayerDeath -= HandlePlayerDeath;
        GameEvents.OnLevelCompleted -= HandleLevelCompleted;
    }

    private void PlaySfx(SfxType type)
    {
        if (sfxSource == null || !clipLookup.TryGetValue(type, out AudioClip clip)) return;

        // Variación de tono para que los sonidos repetidos no resulten mecánicos
        sfxSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        sfxSource.PlayOneShot(clip);
    }

    private void ApplyVolumes()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL no procesa el AudioMixer: el volumen se aplica directamente a las fuentes
        if (musicSource != null) musicSource.volume = VolumeSettings.Music;
        if (sfxSource != null) sfxSource.volume = VolumeSettings.Sfx;
#else
        if (mixer == null) return;

        mixer.SetFloat(MusicVolumeParam, ToDecibels(VolumeSettings.Music));
        mixer.SetFloat(SfxVolumeParam, ToDecibels(VolumeSettings.Sfx));
#endif
    }

    // Slider lineal (0-1) a decibelios: el oído percibe el volumen de forma logarítmica
    private static float ToDecibels(float linear) => Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f;

    private void HandlePlayerDeath() => PlaySfx(SfxType.PlayerDeath);

    private void HandleLevelCompleted() => PlaySfx(SfxType.LevelComplete);
}
