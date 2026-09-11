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
        // NAudio's default is 300 ms of buffering, which put the "listening" cue a third of
        // a second after the microphone was already live; users wait for the cue.
        var output = new WaveOutEvent { DesiredLatency = 60 };
        output.PlaybackStopped += (_, _) => { output.Dispose(); reader.Dispose(); };
        try { output.Init(reader); output.Play(); }
        catch { output.Dispose(); reader.Dispose(); throw; }
    }
}
