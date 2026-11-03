using NAudio.CoreAudioApi;

namespace VAK.Core.Sys;

/// <summary>Reads and sets the volume of one output device. The device object and the device list are cached and refreshed every
/// few seconds: NAudio registers a Core Audio callback per volume object, so creating one on every poll leaks registrations.</summary>
public class WindowsVolumeService : IVolumeService
{
    private static readonly TimeSpan CacheLife = TimeSpan.FromSeconds(15);

    private readonly MMDeviceEnumerator _enum = new();
    private string? _deviceId;
    private MMDevice? _device;
    private DateTime _deviceAt;
    private IReadOnlyList<AudioDevice> _devices = Array.Empty<AudioDevice>();
    private DateTime _devicesAt = DateTime.MinValue;

    private MMDevice? Device()
    {
        try
        {
            if (_device is not null && DateTime.UtcNow - _deviceAt < CacheLife) return _device;
            DropDevice();
            _device = _deviceId is null
                ? _enum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                : _enum.GetDevice(_deviceId);
            _deviceAt = DateTime.UtcNow;
            return _device;
        }
        catch { DropDevice(); return null; }
    }

    private void DropDevice()
    {
        try { _device?.Dispose(); } catch { }
        _device = null;
    }

    public VolumeState? Get()
    {
        try
        {
            var d = Device();
            if (d is null) return null;
            return new VolumeState((int)Math.Round(d.AudioEndpointVolume.MasterVolumeLevelScalar * 100), d.AudioEndpointVolume.Mute, d.ID);
        }
        catch { DropDevice(); return null; }
    }

    public IReadOnlyList<AudioDevice> Devices()
    {
        if (DateTime.UtcNow - _devicesAt < CacheLife) return _devices;
        try
        {
            var list = new List<AudioDevice>();
            foreach (var d in _enum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                list.Add(new AudioDevice(d.ID, d.FriendlyName));
                d.Dispose();
            }
            _devices = list;
        }
        catch { _devices = Array.Empty<AudioDevice>(); }
        _devicesAt = DateTime.UtcNow;
        return _devices;
    }

    public void SetVolume(int percent)
    {
        try { if (Device() is { } d) d.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(percent, 0, 100) / 100f; } catch { }
    }

    public void SetMute(bool muted)
    {
        try { if (Device() is { } d) d.AudioEndpointVolume.Mute = muted; } catch { }
    }

    public void SelectDevice(string id)
    {
        _deviceId = id;
        DropDevice();
    }
}
