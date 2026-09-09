using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Murmur.Abstractions;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Murmur.Platform.Windows;

/// <summary>Mutes other apps temporarily, with a durable recovery record for interrupted recordings.</summary>
public sealed class SessionDucker : IAudioDucker
{
    private readonly object _lock = new();
    private readonly List<(AudioSessionControl Session, string Id)> _ducked = [];
    private readonly string _recoveryPath = Path.Combine(AppPaths.Root, "audio-restore.json");
    private MMDevice? _device;

    /// <summary>Restores sessions left muted by a previous interrupted app process.</summary>
    public SessionDucker()
    {
        lock (_lock) RecoverInterruptedMute();
    }

    /// <inheritdoc />
    public void Duck()
    {
        lock (_lock)
        {
            if (_device is not null) return;
            try
            {
                // Never overwrite an outstanding restoration record.
                RecoverInterruptedMute();
                if (File.Exists(_recoveryPath)) return;
                using var enumerator = new MMDeviceEnumerator();
                if (!enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)) return;
                _device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sessions = _device.AudioSessionManager.Sessions;
                for (var i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (session.GetProcessID == (uint)Environment.ProcessId ||
                        session.State == AudioSessionState.AudioSessionStateExpired ||
                        session.SimpleAudioVolume.Mute) continue;
                    _ducked.Add((session, session.GetSessionInstanceIdentifier));
                }

                // Save BEFORE touching Windows audio. If persistence fails, do not mute.
                if (_ducked.Count > 0)
                {
                    Directory.CreateDirectory(AppPaths.Root);
                    var pending = new Dictionary<string, string[]>
                    {
                        [_device.ID] = _ducked.Select(x => x.Id).ToArray(),
                    };
                    var temporary = _recoveryPath + ".tmp";
                    File.WriteAllText(temporary, JsonSerializer.Serialize(pending, AudioRecoveryJson.Default.DictionaryStringStringArray));
                    File.Move(temporary, _recoveryPath, overwrite: true);
                    foreach (var (session, _) in _ducked) session.SimpleAudioVolume.Mute = true;
                }
            }
            catch (Exception e) when (IsAudioOrFileError(e))
            {
                Debug.WriteLine($"could not mute audio: {e.Message}");
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
        foreach (var (session, _) in _ducked)
        {
            try { session.SimpleAudioVolume.Mute = false; }
            catch (Exception e) when (IsAudioOrFileError(e)) { Debug.WriteLine(e.Message); }
        }
        _ducked.Clear();
        _device?.Dispose();
        _device = null;
        RecoverInterruptedMute();
    }

    private void RecoverInterruptedMute()
    {
        if (!File.Exists(_recoveryPath)) return;
        try
        {
            var pending = JsonSerializer.Deserialize(File.ReadAllText(_recoveryPath), AudioRecoveryJson.Default.DictionaryStringStringArray);
            if (pending is null) return;
            using var enumerator = new MMDeviceEnumerator();
            foreach (var (deviceId, ids) in pending)
            {
                // Match exact session instances, never a recycled process id or a new app session.
                using var device = enumerator.GetDevice(deviceId);
                var sessions = device.AudioSessionManager.Sessions;
                for (var i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (session.State != AudioSessionState.AudioSessionStateExpired &&
                        ids.Contains(session.GetSessionInstanceIdentifier, StringComparer.Ordinal))
                        session.SimpleAudioVolume.Mute = false;
                }
            }
            File.Delete(_recoveryPath);
        }
        catch (Exception e) when (IsAudioOrFileError(e) || e is JsonException)
        {
            // Keep the record and retry on the next start/recording if an endpoint is unavailable.
            Debug.WriteLine($"audio restoration pending: {e.Message}");
        }
    }

    private static bool IsAudioOrFileError(Exception e) =>
        e is COMException or InvalidCastException or UnauthorizedAccessException or IOException;
}

[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string[]>))]
internal sealed partial class AudioRecoveryJson : System.Text.Json.Serialization.JsonSerializerContext;