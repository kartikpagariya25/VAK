namespace VAK.Core.Sys;

public record BatteryInfo(int Percent, bool Charging, int? HealthPercent, bool PluggedIn = false);
public record NetworkInfo(string Name, int? LinkMbps, double DownBytesPerSec, double UpBytesPerSec, bool IsWireless = false);
public record PerfInfo(int Cpu, int Ram, int Disk);
public record AudioDevice(string Id, string Name);
public record VolumeState(int Percent, bool Muted, string DeviceId);

public interface IBatteryService { BatteryInfo? Read(); }
public interface INetworkService { NetworkInfo? Read(); }
public interface IPerformanceService { PerfInfo? Read(); }

public interface IVolumeService
{
    VolumeState? Get();
    IReadOnlyList<AudioDevice> Devices();
    void SetVolume(int percent);
    void SetMute(bool muted);
    void SelectDevice(string id);
}
