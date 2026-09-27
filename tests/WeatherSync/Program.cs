using ElinTogether.Models;
using ElinTogether.Net;
int checks = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); checks++; }
var weather = EClass.world.weather;
weather._currentCondition = Weather.Condition.RainHeavy;
weather.duration = 15;
weather.lastRain = 1234;
var state = WeatherStateSnapshot.Create();
Check(state.ZoneUid == 7 && state.Condition == Weather.Condition.RainHeavy && state.Duration == 15 && state.LastRain == 1234, "capture authoritative weather and zone");
weather._currentCondition = Weather.Condition.Fine;
weather.duration = 3;
NetSession.Instance.Connection = new ElinNetClient();
state.Apply();
Check(weather.CurrentCondition == Weather.Condition.RainHeavy && weather.duration == 15 && weather.lastRain == 1234 && EClass.screen.Refreshes == 1, "stale client receives rain and timer with one visual refresh");
state.Apply();
Check(EClass.screen.Refreshes == 1, "unchanged snapshots do not restart sky effects");
new WeatherStateSnapshot { ZoneUid = 7, Condition = Weather.Condition.RainHeavy, Duration = 14, LastRain = 1235 }.Apply();
Check(weather.duration == 14 && weather.lastRain == 1235 && EClass.screen.Refreshes == 1, "timer changes synchronize without restarting rain");
EClass._zone.uid = 8;
new WeatherStateSnapshot { ZoneUid = 7, Condition = Weather.Condition.Fine }.Apply();
Check(weather.CurrentCondition == Weather.Condition.RainHeavy && weather.duration == 14, "old-zone snapshots cannot overwrite newly entered zone weather");
EClass._zone.uid = 7;
weather.Fixed = Weather.Condition.Fine;
new WeatherStateSnapshot { ZoneUid = 7, Condition = Weather.Condition.Snow, Duration = 9 }.Apply();
Check(weather._currentCondition == Weather.Condition.Snow && weather.CurrentCondition == Weather.Condition.Fine && EClass.screen.Refreshes == 1, "map-specific fixed weather stays in control of visuals");
weather.Fixed = Weather.Condition.None;
new WeatherStateSnapshot { ZoneUid = 7, Condition = Weather.Condition.Fine }.Apply();
Check(weather.CurrentCondition == Weather.Condition.Fine && EClass.screen.Refreshes == 2, "ending precipitation refreshes the sky");
foreach (var connection in new object?[] { null, new object() }) {
    NetSession.Instance.Connection = connection;
    state.Apply();
    Check(weather.CurrentCondition == Weather.Condition.Fine && EClass.screen.Refreshes == 2, "solo/host cannot apply client weather reconciliation");
}
Console.WriteLine($"{checks} checks passed");
