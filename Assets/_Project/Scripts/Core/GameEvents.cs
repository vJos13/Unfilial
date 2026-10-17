using System;

/// <summary>
/// Canal global de eventos. Desacopla la lógica del juego de la UI y el Audio.
/// </summary>
public static class GameEvents
{
    // Eventos del jugador
    public static Action<int> OnOrbsChanged;
    public static Action<int, int> OnHealthChanged; // vida actual, vida máxima
    public static Action OnPlayerDeath;

    // Eventos de flujo de nivel
    public static Action OnLevelCompleted;
    public static Action<bool> OnGamePaused; // true = pausado

    // Eventos de progresión
    public static Action<int> OnPowerLevelChanged; // nivel de poder (sube cada X orbes)

    // Eventos de audio
    public static Action<SfxType> OnPlaySfx;
    public static Action OnVolumeChanged;
}
