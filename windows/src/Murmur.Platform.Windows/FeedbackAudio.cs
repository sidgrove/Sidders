using Murmur.Abstractions;
using NAudio.Wave;

namespace Murmur.Platform.Windows;

/// <summary>Plays short cues through the default Windows output device.</summary>
public sealed class FeedbackAudio : IFeedbackAudio
{
    /// <inheritdoc />
    public void Play(byte[] wave)
    {
        var reader = new WaveFileReader(new MemoryStream(wave, writable: false));
        var output = new WaveOutEvent();
        output.PlaybackStopped += (_, _) => { output.Dispose(); reader.Dispose(); };
        try { output.Init(reader); output.Play(); }
        catch { output.Dispose(); reader.Dispose(); throw; }
    }
}
