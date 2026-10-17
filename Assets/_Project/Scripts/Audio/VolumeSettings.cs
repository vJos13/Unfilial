using UnityEngine;

/// <summary>
/// Volúmenes persistentes (PlayerPrefs). La UI escribe aquí y el AudioManager reacciona a OnVolumeChanged.
/// </summary>
public static class VolumeSettings
{
    private const string MusicKey = "volume_music";
    private const string SfxKey = "volume_sfx";

    public static float Music => PlayerPrefs.GetFloat(MusicKey, 0.6f);
    public static float Sfx => PlayerPrefs.GetFloat(SfxKey, 0.8f);

    public static void SetMusic(float value)
    {
        PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
        GameEvents.OnVolumeChanged?.Invoke();
    }

    public static void SetSfx(float value)
    {
        PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
        GameEvents.OnVolumeChanged?.Invoke();
    }

    public static void Save() => PlayerPrefs.Save();
}
