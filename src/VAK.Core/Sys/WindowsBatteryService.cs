using Windows.Devices.Power;

namespace VAK.Core.Sys;

public class WindowsBatteryService : IBatteryService
{
    public BatteryInfo? Read()
    {
        try
        {
            var r = Battery.AggregateBattery.GetReport();
            if (r.Status == Windows.System.Power.BatteryStatus.NotPresent) return null;
            if (r.FullChargeCapacityInMilliwattHours is not int full || r.RemainingCapacityInMilliwattHours is not int left || full <= 0)
                return null;
            var percent = Math.Clamp((int)Math.Round(left * 100.0 / full), 0, 100);
            var charging = r.Status is Windows.System.Power.BatteryStatus.Charging;
            var plugged = r.Status is Windows.System.Power.BatteryStatus.Charging or Windows.System.Power.BatteryStatus.Idle;
            return new BatteryInfo(percent, charging, BatteryMath.HealthPercent(full, r.DesignCapacityInMilliwattHours), plugged);
        }
        catch { return null; }
    }
}
