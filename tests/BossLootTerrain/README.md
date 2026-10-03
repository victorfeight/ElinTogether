# Boss loot terrain regression

Links production terrain wrappers, transpiler, and packet handler. Game/transport boundaries are simulated. The test reads the installed Elin assembly with Cecil to validate the exact two Point setter call sites; it does not install native Unity detours.

Run with the pinned Windows SDK:

```
dotnet run --project tests/BossLootTerrain -c Release -p:ElinGamePath="C:\Program Files (x86)\Steam\steamapps\common\Elin" -- "C:\Program Files (x86)\Steam\steamapps\common\Elin\Elin_Data\Managed\Elin.dll"
```

Covers host-chosen tile application, alternate client tile preservation, duplicate packets, host rejection, wrong zone and bounds checks, progress packing, vanilla solo behavior, and transpiler shape/label preservation.

Live acceptance: defeat a boss next to walls and compare chest and neighboring tiles on both screens. Repeat with a character or installed object occupying one nearby candidate location.

Path destruction now captures host Map.SetBlock/SetObj calls only inside native
Chara.DestroyPath. This includes recursive dependent-ramp mining and multi-tile
creatures. Clients suppress their own DestroyPath replay and apply the host's
existing terrain packet; no local mining/drop rolls run. Tile landing refreshes
shadows and FOV. Ordinary mining/building/AutoAct remain on their own protocols.

The added tests check installed GoalCombat eligibility and setter signatures,
scope nesting/restoration, client/solo behavior, and tile refresh. These are
production-hook boundary tests, not Unity detour or Steam integration tests.
Live acceptance: let a strong ordinary-sized hostile enemy break through walls
while in combat, then let a multi-tile enemy cross walls/blocked objects near a
ramp. Compare both screens, walk through the opening, and re-enter the map.
Host log: `Path terrain cleared block`; client: `Applied authoritative terrain`.
