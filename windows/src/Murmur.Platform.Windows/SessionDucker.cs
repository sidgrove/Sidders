using System.Diagnostics;
using System.Runtime.InteropServices;
using Murmur.Abstractions;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Murmur.Platform.Windows;

/// <summary>
/// Ducks other applications through their WASAPI audio sessions.
/// </summary>
/// <remarks>
/// <para>
/// Per-session volumes rather than the endpoint's master volume: this is the same
/// mechanism Windows itself uses for communications ducking, it leaves the user's volume
/// slider exactly where they put it, and a session's volume dies with the session — so
/// even if this process vanished mid-hold, the worst case is one quiet app until it is
/// restarted, never a silent machine.
/// </para>
/// <para>
/// Each <see cref="Duck"/> takes a fresh snapshot of the sessions on the default output
/// device. Sessions that start during a hold are not ducked; that is a short window and
/// not worth a callback registration.
/// </para>
/// </remarks>
public sealed class SessionDucker : IAudioDucker
{
    /// <summary>How much of its own volume each other application keeps while the user talks.</summary>
    public const float DuckedFraction = 0.15f;

    private readonly object _lock = new();
    private readonly List<(AudioSessionControl Session, float Original)> _ducked = [];
    private MMDevice? _device;

    /// <inheritdoc />
    public void Duck()
    {
        lock (_lock)
        {
            if (_ducked.Count > 0) return;

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                if (!enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)) return;

                _device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sessions = _device.AudioSessionManager.Sessions;
                var ownPid = (uint)Environment.ProcessId;

                for (var i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (session.GetProcessID == ownPid || session.State != AudioSessionState.AudioSessionStateActive) continue;

                    var original = session.SimpleAudioVolume.Volume;
                    session.SimpleAudioVolume.Volume = original * DuckedFraction;
                    _ducked.Add((session, original));
                }

                Debug.WriteLine($"ducked {_ducked.Count} audio session(s)");
            }
            catch (Exception e) when (e is COMException or InvalidCastException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"could not duck audio: {e.Message}");
                RestoreLocked();
            }
        }
    }

    /// <inheritdoc />
    public void Restore()
    {
        lock (_lock) RestoreLocked();
    }

    private void RestoreLocked()
    {
        foreach (var (session, original) in _ducked)
        {
            try
            {
                session.SimpleAudioVolume.Volume = original;
            }
            catch (Exception e) when (e is COMException or InvalidCastException)
            {
                // The application has closed; its session is gone and so is the need.
                Debug.WriteLine(e.Message);
            }
        }

        _ducked.Clear();
        _device?.Dispose();
        _device = null;
    }
}
