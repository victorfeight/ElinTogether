# Local hallucination visuals

This harness compiles the actual decompiled `ConHallucination.cs`, transforms its IL
using the production Harmony transpiler, and executes the transformed methods.
It does not install native Unity detours. It checks local/remote application
and removal, host/client identity, scene reconciliation and vanilla solo behavior.
Other game types are simulated; Unity rendering and multiplayer transport still
require live acceptance. No decompiled game source is committed.

Run with the local Windows SDK:

```
dotnet run --project tests/LocalHallucination -c Release -p:ElinGamePath="C:\Program Files (x86)\Steam\steamapps\common\Elin" -p:ElinSourcePath="C:\Users\Vic\elin-decompiled-stable-23.338.2\Elin"
```

Live: inflict/cure hallucination on each player separately, then both together.
Only the affected viewer should see distorted sprites. Cure one while the other
remains affected; reconnect while affected and while healthy. Both peers install
the same build.
