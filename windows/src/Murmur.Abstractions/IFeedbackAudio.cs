namespace Murmur.Abstractions;

/// <summary>Nonblocking playback of a short PCM wave cue.</summary>
public interface IFeedbackAudio
{
    /// <summary>Plays a wave file without changing the foreground window.</summary>
    void Play(byte[] wave);
}
