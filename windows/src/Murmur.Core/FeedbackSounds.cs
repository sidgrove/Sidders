namespace Murmur.Core;

/// <summary>Distinct, optional cues for recording transitions and successful sending.</summary>
public sealed class FeedbackSounds(Func<SettingsData> settings, Action<byte[]> play)
{
    private bool _recording;
    private static readonly byte[] Start = Tone(440);
    private static readonly byte[] Stop = Tone(330);
    private static readonly byte[] Send = Tone(550);

    /// <summary>Observes state changes; repeated updates never repeat a cue.</summary>
    public void Observe(DictationState state)
    {
        var recording = state == DictationState.Recording;
        if (recording == _recording) return;
        _recording = recording;
        if (settings().RecordingSounds) Play(recording ? Start : Stop);
    }

    /// <summary>Confirms a completed Enter keypress.</summary>
    public void Sent()
    {
        if (settings().SendSound) Play(Send);
    }

    private void Play(byte[] wave)
    {
        try { play(wave); }
        catch (Exception e) { Log.Warn($"feedback sound unavailable: {e.Message}"); }
    }

    private static byte[] Tone(double frequency)
    {
        const int rate = 24000;
        const int samples = 2160;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(36 + samples * 2); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8); writer.Write(samples * 2);
        for (var i = 0; i < samples; i++)
        {
            // A soft 90 ms note, with a smooth fade at both ends and no sharp attack.
            var envelope = Math.Pow(Math.Sin(Math.PI * i / samples), 2);
            writer.Write((short)(short.MaxValue * 0.025 * envelope * Math.Sin(2 * Math.PI * frequency * i / rate)));
        }
        return stream.ToArray();
    }
}
