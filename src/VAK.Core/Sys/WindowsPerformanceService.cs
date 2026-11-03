using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VAK.Core.Sys;

public class WindowsPerformanceService : IPerformanceService
{
    private PerformanceCounter? _cpu;
    private PerformanceCounter? _disk;
    private bool _initFailed;

    public PerfInfo? Read()
    {
        try
        {
            if (_cpu is null && !_initFailed)
            {
                try
                {
                    _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _disk = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
                    _cpu.NextValue(); _disk.NextValue();
                }
                catch { _initFailed = true; _cpu = null; _disk = null; }
            }
            var st = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (!GlobalMemoryStatusEx(ref st)) return null;
            if (_cpu is null || _disk is null) return null;
            return new PerfInfo(
                Math.Clamp((int)Math.Round(_cpu.NextValue()), 0, 100),
                (int)st.dwMemoryLoad,
                Math.Clamp((int)Math.Round(_disk.NextValue()), 0, 100));
        }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX s);
}
