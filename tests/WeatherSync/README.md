# Weather reconciliation

Run `dotnet run --project tests/WeatherSync/WeatherSync.csproj -c Release`.

The production WeatherStateSnapshot is compiled against a small boundary. Tests
cover stale rain, stopping precipitation, duration/last-rain updates, unchanged
snapshots, zone mismatches, fixed map weather, and host/solo guards. SetCondition
throws in the boundary to catch accidental client-side weather rerolls/watering.
The full mod build checks integration with game types and MessagePack generation;
Unity particle/audio behavior still needs live validation.

WeatherStateSnapshot travels in the existing WorldStateSnapshot and
WorldDateAdvanceDelta packets. It copies current condition, remaining duration
and lastRain. It does not advance local forecasts or call Weather.SetCondition.
Vanilla GameScreenElona.RefreshWeather updates rain/snow effects every frame;
RefreshSky is requested only when the effective weather changes. Map-specific
fixed conditions remain authoritative for that map's visuals.

Live test with both peers on API V11:
- Join while the host has rain; confirm matching weather outdoors.
- Observe a change between fine/rain/heavy rain, including a time skip.
- Change zones and enter an indoor or fixed-weather map.
- Confirm steady rain does not repeatedly restart or flood message logs.

Expected client diagnostic: `Weather reconciled in zone ...` on a condition
change. This is current-weather synchronization, not forecast-list replication.
