using System;
using UnityEngine;

/// <summary>
/// Herramienta de Editor: sintetiza los SFX y la música como muestras PCM.
/// UnfilialBuilder las exporta a archivos .wav dentro de _Project/Audio.
/// </summary>
public static class ProceduralAudio
{
    private const int SfxSampleRate = 44100;
    private const int MusicSampleRate = 44100;
    private const float TwoPi = Mathf.PI * 2f;

    public struct Samples
    {
        public float[] Data;
        public int SampleRate;
    }

    public static Samples CreateSfx(SfxType type)
    {
        var random = new System.Random((int)type + 1);
        float Noise() => (float)(random.NextDouble() * 2.0 - 1.0);

        switch (type)
        {
            case SfxType.Jump:
                return Build("sfx_jump", 0.14f, SfxSampleRate, t =>
                {
                    float freq = Mathf.Lerp(280f, 620f, t / 0.14f);
                    return Mathf.Sin(TwoPi * freq * t) * Mathf.Exp(-t * 18f) + Noise() * 0.15f * Mathf.Exp(-t * 40f);
                });
            case SfxType.Dash:
            {
                float filtered = 0f;
                return Build("sfx_dash", 0.22f, SfxSampleRate, t =>
                {
                    float cutoff = Mathf.Lerp(0.08f, 0.35f, t / 0.22f);
                    filtered += cutoff * (Noise() - filtered);
                    return filtered * Envelope(t, 0.03f, 0.22f);
                });
            }
            case SfxType.Attack:
            {
                // Barrido de ruido filtrado: "whoosh" de espada
                float filtered = 0f;
                return Build("sfx_attack", 0.18f, SfxSampleRate, t =>
                {
                    float cutoff = Mathf.Lerp(0.5f, 0.05f, t / 0.18f);
                    filtered += cutoff * (Noise() - filtered);
                    return filtered * Envelope(t, 0.01f, 0.18f);
                });
            }
            case SfxType.EnemyHit:
                return Build("sfx_enemy_hit", 0.16f, SfxSampleRate, t =>
                {
                    float freq = Mathf.Lerp(220f, 70f, t / 0.16f);
                    float body = Mathf.Sin(TwoPi * freq * t) * Mathf.Exp(-t * 25f);
                    float click = t < 0.025f ? Noise() * (1f - t / 0.025f) : 0f;
                    return body + click * 0.6f;
                });
            case SfxType.EnemyDeath:
                return Build("sfx_enemy_death", 0.45f, SfxSampleRate, t =>
                {
                    float freq = Mathf.Lerp(420f, 50f, t / 0.45f);
                    float tone = SoftSquare(TwoPi * freq * t) * Mathf.Exp(-t * 6f);
                    return tone * 0.7f + Noise() * 0.3f * Mathf.Exp(-t * 12f);
                });
            case SfxType.PlayerHurt:
                return Build("sfx_player_hurt", 0.3f, SfxSampleRate, t =>
                {
                    float vibrato = 1f + 0.08f * Mathf.Sin(TwoPi * 30f * t);
                    float tone = SoftSquare(TwoPi * 110f * vibrato * t);
                    return (tone * 0.7f + Noise() * 0.4f) * Mathf.Exp(-t * 10f);
                });
            case SfxType.PlayerDeath:
                return Build("sfx_player_death", 1.1f, SfxSampleRate, t =>
                {
                    float freq = Mathf.Lerp(220f, 55f, t / 1.1f);
                    float tone = Mathf.Sin(TwoPi * freq * t) + 0.5f * Mathf.Sin(TwoPi * freq * 1.19f * t);
                    return tone * Envelope(t, 0.02f, 1.1f) + Noise() * 0.15f * Mathf.Exp(-t * 3f);
                });
            case SfxType.Block:
                return Build("sfx_block", 0.4f, SfxSampleRate, t =>
                {
                    // Parciales inarmónicos = sonido metálico
                    float metal = Mathf.Sin(TwoPi * 1250f * t) + 0.7f * Mathf.Sin(TwoPi * 1870f * t) + 0.5f * Mathf.Sin(TwoPi * 2730f * t);
                    float click = t < 0.01f ? Noise() : 0f;
                    return metal * Mathf.Exp(-t * 14f) * 0.5f + click * 0.5f;
                });
            case SfxType.ShieldBreak:
                return Build("sfx_shield_break", 0.5f, SfxSampleRate, t =>
                {
                    float metal = Mathf.Sin(TwoPi * 900f * t) + 0.6f * Mathf.Sin(TwoPi * 1410f * t);
                    return (metal * 0.4f + Noise() * 0.6f) * Mathf.Exp(-t * 7f);
                });
            case SfxType.OrbPickup:
                return Build("sfx_orb", 0.3f, SfxSampleRate, t =>
                {
                    float freq = t < 0.07f ? 1318.5f : 1760f;
                    float local = t < 0.07f ? t : t - 0.07f;
                    return Mathf.Sin(TwoPi * freq * t) * Mathf.Exp(-local * 14f) * 0.8f;
                });
            case SfxType.PowerUp:
                return Arpeggio("sfx_powerup", new[] { 440f, 554.37f, 659.25f, 880f, 1108.73f }, 0.09f, 0.5f);
            case SfxType.UIClick:
                return Build("sfx_click", 0.06f, SfxSampleRate, t => Mathf.Sin(TwoPi * 880f * t) * Mathf.Exp(-t * 70f));
            case SfxType.LevelComplete:
                return Arpeggio("sfx_level_complete", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.14f, 0.9f);
            default:
                return Build("sfx_silence", 0.05f, SfxSampleRate, t => 0f);
        }
    }

    /// <summary>
    /// Bucle melancólico (Am - F - C - E) con pad, bajo y arpegio. ~16 segundos.
    /// </summary>
    public static Samples CreateMusicLoop()
    {
        const float chordDuration = 4f;
        float[][] chords =
        {
            new[] { 110.00f, 220.00f, 261.63f, 329.63f }, // Am
            new[] { 87.31f, 174.61f, 220.00f, 261.63f },  // F
            new[] { 130.81f, 196.00f, 261.63f, 329.63f }, // C
            new[] { 82.41f, 164.81f, 207.65f, 246.94f },  // E
        };
        int[] arpPattern = { 1, 2, 3, 2, 1, 2, 3, 2 };
        float noteDuration = chordDuration / arpPattern.Length;
        float totalDuration = chordDuration * chords.Length;

        return Build("music_loop", totalDuration, MusicSampleRate, t =>
        {
            int chordIndex = Mathf.Min((int)(t / chordDuration), chords.Length - 1);
            float[] chord = chords[chordIndex];
            float chordTime = t - chordIndex * chordDuration;

            // Pad suave con fundido de entrada/salida por acorde (sin cortes en el bucle)
            float padEnv = Mathf.Clamp01(chordTime / 0.6f) * Mathf.Clamp01((chordDuration - chordTime) / 0.6f);
            float tremolo = 0.85f + 0.15f * Mathf.Sin(TwoPi * 0.5f * t);
            float pad = 0f;
            for (int i = 1; i < chord.Length; i++)
            {
                pad += Mathf.Sin(TwoPi * chord[i] * t) + 0.25f * Mathf.Sin(TwoPi * chord[i] * 2f * t);
            }
            pad *= 0.12f * padEnv * tremolo;

            float bass = Mathf.Sin(TwoPi * chord[0] * t) * 0.3f * padEnv;

            int noteIndex = (int)(chordTime / noteDuration);
            float noteTime = chordTime - noteIndex * noteDuration;
            float arpFreq = chord[arpPattern[noteIndex % arpPattern.Length]] * 2f;
            float arp = Triangle(TwoPi * arpFreq * t) * Mathf.Exp(-noteTime * 5f) * 0.18f * Mathf.Clamp01(noteTime / 0.005f);

            return pad + bass + arp;
        }, 0.7f);
    }

    private static Samples Arpeggio(string name, float[] notes, float step, float tail)
    {
        float duration = notes.Length * step + tail;
        return Build(name, duration, SfxSampleRate, t =>
        {
            float sample = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float local = t - i * step;
                if (local < 0f) break;
                sample += Triangle(TwoPi * notes[i] * t) * Mathf.Exp(-local * 6f);
            }
            return sample * Mathf.Clamp01((duration - t) / 0.05f);
        });
    }

    private static Samples Build(string name, float duration, int sampleRate, Func<float, float> generator, float peak = 0.9f)
    {
        int length = Mathf.CeilToInt(duration * sampleRate);
        var data = new float[length];
        float max = 0.0001f;

        for (int i = 0; i < length; i++)
        {
            data[i] = generator(i / (float)sampleRate);
            max = Mathf.Max(max, Mathf.Abs(data[i]));
        }

        float gain = peak / max;
        for (int i = 0; i < length; i++) data[i] *= gain;

        return new Samples { Data = data, SampleRate = sampleRate };
    }

    private static float Envelope(float t, float attack, float duration)
    {
        float rise = Mathf.Clamp01(t / attack);
        float fall = Mathf.Clamp01(1f - (t - attack) / (duration - attack));
        return rise * fall * fall;
    }

    private static float SoftSquare(float phase) => (float)Math.Tanh(Mathf.Sin(phase) * 4f);

    private static float Triangle(float phase) => Mathf.PingPong(phase / Mathf.PI, 1f) * 2f - 1f;
}

/// <summary>
/// Escritor mínimo de WAV PCM 16-bit mono.
/// </summary>
public static class WavWriter
{
    public static void Write(string path, ProceduralAudio.Samples samples)
    {
        using var stream = new System.IO.FileStream(path, System.IO.FileMode.Create);
        using var writer = new System.IO.BinaryWriter(stream);

        int dataSize = samples.Data.Length * 2;
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);                      // tamaño del bloque fmt
        writer.Write((short)1);                // PCM
        writer.Write((short)1);                // mono
        writer.Write(samples.SampleRate);
        writer.Write(samples.SampleRate * 2);  // byte rate
        writer.Write((short)2);                // block align
        writer.Write((short)16);               // bits por muestra
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        foreach (float sample in samples.Data)
        {
            writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
        }
    }
}
