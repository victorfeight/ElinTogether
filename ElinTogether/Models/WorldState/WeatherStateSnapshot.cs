using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class WeatherStateSnapshot : EClass
{
    [Key(0)] public int ZoneUid { get; init; }
    [Key(1)] public Weather.Condition Condition { get; init; }
    [Key(2)] public int Duration { get; init; }
    [Key(3)] public int LastRain { get; init; }

    public static WeatherStateSnapshot Create() => new() {
        ZoneUid = _zone.uid,
        Condition = world.weather._currentCondition,
        Duration = world.weather.duration,
        LastRain = world.weather.lastRain,
    };

    public void Apply()
    {
        if (NetSession.Instance.Connection is not ElinNetClient || _zone.uid != ZoneUid) return;
        var weather = world.weather;
        var previous = weather.CurrentCondition;
        // SetCondition rerolls rain-related traits and calls RainWater. The host
        // already performed those gameplay effects; only copy the resulting state.
        weather._currentCondition = Condition;
        weather.duration = Duration;
        weather.lastRain = LastRain;
        // CurrentCondition retains vanilla's map-specific weather override.
        if (weather.CurrentCondition == previous) return;
        screen.RefreshSky();
        EmpLog.Debug("Weather reconciled in zone {ZoneUid}: {Previous} -> {Current}",
            ZoneUid, previous, weather.CurrentCondition);
    }
}
