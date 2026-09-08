using System.Runtime.InteropServices;
using Murmur.Abstractions;
using NAudio.CoreAudioApi;

namespace Murmur.Platform.Windows;

/// <summary>Enumerates capture endpoints through WASAPI.</summary>
/// <remarks>
/// Only <i>active</i> devices: unplugged and disabled endpoints stay enumerable in Windows
/// for years, and listing them means offering the user a microphone that cannot open.
/// </remarks>
public sealed class WasapiDeviceCatalog : IAudioDeviceCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<AudioDevice> ListCaptureDevices()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();

            string? defaultId = null;
            if (enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Communications))
            {
                using var fallback = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                defaultId = fallback.ID;
            }

            var devices = new List<AudioDevice>();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                using (device)
                {
                    devices.Add(new AudioDevice(device.ID, device.FriendlyName, device.ID == defaultId));
                }
            }

            return devices.OrderByDescending(d => d.IsDefault).ThenBy(d => d.Name, StringComparer.CurrentCulture).ToList();
        }
        catch (COMException)
        {
            return [];
        }
    }
}
