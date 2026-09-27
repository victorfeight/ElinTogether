# Boss loot terrain regression

Links production terrain wrappers, transpiler, and packet handler. Game/transport boundaries are simulated. The test reads the installed Elin assembly with Cecil to validate the exact two Point setter call sites; it does not install native Unity detours.

Run with the pinned Windows SDK:

```
dotnet run --project tests/BossLootTerrain -c Release -p:ElinGamePath="C:\Program Files (x86)\Steam\steamapps\common\Elin" -- "C:\Program Files (x86)\Steam\steamapps\common\Elin\Elin_Data\Managed\Elin.dll"
```

Covers host-chosen tile application, alternate client tile preservation, duplicate packets, host rejection, wrong zone and bounds checks, progress packing, vanilla solo behavior, and transpiler shape/label preservation.

Live acceptance: defeat a boss next to walls and compare chest and neighboring tiles on both screens. Repeat with a character or installed object occupying one nearby candidate location.
