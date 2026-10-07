# Stable 23.352 compatibility — 2026-10-05

## Evidence and cause

Host screenshot `Downloads/image.png` reports `MissingFieldException: byte Cell._floorMat`
from PathTerrainSync.Floor and ConstructionTerrainPatch.After during mushroom-floor
placement. Native crafting reached Map.SetFloor; our postfixes were compiled against
23.338.2's byte fields. The local multiplayer log verifies game 0.23.352.0.

Upstream source snapshot: Elin-Modding-Resources/Elin-Decompiled commit 027ee53
(EA 23.352 Stable), downloaded beside the preserved 23.338.2 snapshot. Its subsequent
6713f06 commit only updates Doxyfile. Cell now uses integer tile/material/pillar IDs.
Map.SetBridge now accepts an integer pillar and an explicit hidePillar boolean.
RecipeBridgePillar.Build sets both the pillar ID and visibility when placing/removing.

## Changes

- Rebuilt the full mod against installed 23.352 Elin.dll; this fixes binary references
  in all terrain hooks, without changing their authoritative capture/replay flow.
- Removed two byte casts in bridge/pillar result application.
- Added HidePillar to the existing construction result and apply both native fields.
- Bumped protocol V48 to V49 so old and updated peers cannot mix.
- Map transfer still uses native Map.Save and transfers the resulting files unchanged;
  no custom byte-to-int conversion or save migration is required.

The compatibility checker rejects the old installed DLL with 13 obsolete Cell field
references, the old Map.SetBridge signature, and Religion.GetGiftRank(). The latter
also resolves in the new build through recompilation against the current signature.

## Validation

- Release build against installed game: zero warnings/errors.
- Construction: 73 checks, including >255 floor/material/pillar IDs, hidden pillar
  placement/removal, native field/setter metadata and 1,542 compiled game-member
  references resolving against installed Elin.dll. CLR-generated array accessors
  are excluded from metadata resolution because they have no method definitions.
- BossLootTerrain: all 47 checks pass, including native floor hooks, wall destruction,
  boss-loot and explosion terrain call sites, replay boundaries and visual effects.
- AdvancedBuilding: 38 checks pass, including all eight native terrain setters.
- Negative control: the same compatibility scan rejects the old installed DLL with
  the exact byte Cell._floorMat mismatch in the screenshot.

These are metadata and isolated boundary tests, not a live Unity multiplayer test
or an audit of every third-party mod against the game update.

## Installation and live checks

Installed after confirming Elin.exe was closed:
`C:\Program Files (x86)\Steam\steamapps\common\Elin\Package\Mod_ElinTogether\ElinTogether.dll`

Backup: `C:\Users\Vic\ElinTogether-backups\20261005-203028-before-stable-23.352\ElinTogether.dll`

Staged build: `build/stable-23.352/ElinTogether.dll`

SHA256: `0ae5646883077b9782de646e584d544a826e27e3772b9d3642c6c1a8b57b0fe7`

Both peers must use this build on 23.352. Retest host and client mushroom-floor
placement, ordinary floor/wall placement, bridge pillar placement/removal, terrain
raising/lowering and zone re-entry. Confirm matching terrain and no MissingFieldException
or MissingMethodException. Installation does not undo any preexisting map divergence;
re-entering downloads the authoritative map. Live results remain pending.
