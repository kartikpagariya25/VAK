using NAudio.CoreAudioApi;

namespace VAK.Core.Sys;

public class WindowsVolumeService : IVolumeService
{
    private readonly MMDeviceEnumerator _enum = new();
    private string? _deviceId;

    private MMDevice? Device()
    {
        try
        {
            return _deviceId is null
                ? _enum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                : _enum.GetDevice(_deviceId);
        }
        catch { return null; }
    }

    public VolumeState? Get()
    {
        try
        {
            var d = Device();
            if (d is null) return null;
            return new VolumeState((int)Math.Round(d.AudioEndpointVolume.MasterVolumeLevelScalar * 100), d.AudioEndpointVolume.Mute, d.ID);
        }
        catch { return null; }
    }

    public IReadOnlyList<AudioDevice> Devices()
    {
        try
        {
            return _enum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Select(d => new AudioDevice(d.ID, d.FriendlyName)).ToList();
        }
        catch { return Array.Empty<AudioDevice>(); }
    }

    public void SetVolume(int percent)
    {
        try { if (Device() is { } d) d.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(percent, 0, 100) / 100f; } catch { }
    }

    public void SetMute(bool muted)
    {
        try { if (Device() is { } d) d.AudioEndpointVolume.Mute = muted; } catch { }
    }

    public void SelectDevice(string id) => _deviceId = id;
}
