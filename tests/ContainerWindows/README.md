# Container settings and carried-window persistence

## Source-backed diagnosis

Compared with local upstream/main (4a487d1): the previous InvRefreshMenuEvent and
InvSaveDataDelta were identical to upstream. This is an inherited integration
fault, not purse-specific vanilla behavior.

Vanilla 23.338.2 source:
- TraitContainerPurse inherits ordinary container opening; it has no position reset.
- LayerInventory.CreateContainerPC/CreateContainer bind window.saveData directly
  to the item's c_windowSaveData. Initialization supplies size/anchor defaults.
- Window.idWindow is layer.uid + windowIndex. Floating containers share this ID.
- Window.UpdateSaveData writes geometry into that same object; OnKill and
  Player.OnBeforeSave call it. Card stores c_windowSaveData in its native data.
- UIInventory.RefreshWindow only falls back to Window.dictData when saveData is null.

The old unconditional RefreshMenu prefix replaced a valid item-owned record with
Window.dictData[genericWindowId]. Closing then updated the wrong object. Old
settings packets selected the first matching window ID or wrote only to that
generic dictionary when closed, so they could affect another container and did
not reliably persist container rules on the actual item.

This is a definite code defect and fits the observed purse reset. There is no
runtime trace proving every observed reset followed this branch.

## Change and scope

Remove the replacement prefix. Shared settings packets identify the actual card,
mutate its native record in place, refresh only matching windows, and relay from
host to peers. Main/shop sorting preferences and window appearance remain local.
When no record exists yet, initialize once from the sender's native initialized
record, with open=false. No container or inventory item is cloned.

Carried container layouts (including nested bags) are an optional field in the
existing character profile, captured at its existing explicit lifecycle checkpoints.
Open carried windows flush their current geometry first. Restore applies layout
only to items currently owned by that character and preserves newer host behavior.
Older profiles without the field remain valid. No timers, polling, new save files,
or save migration. Town/ground-container per-player layout archives are outside
this change; the native world save still owns those records.

API V32 is required because the settings packet now carries card identity.
Both peers must update together. No install is performed by these test projects.

## Verification

- ContainerWindows: 13 checks running production delta/menu/settings code with a
  simulated network/Unity boundary: identical prefab IDs, closed/reopened items,
  first initialization, relay, destroyed targets, main/shop exclusion, no echo.
- ContainerWindowsNative: 8 checks using installed Elin/Plugins.UI types with the
  production copy helper: exact integer geometry, native flags/JSON callbacks,
  filter independence, and separating personal layout from shared behavior.
  Uses the SDK Newtonsoft serializer to run those native callbacks under .NET 11;
  the game itself uses its bundled serializer/Mono runtime.
- SoloCharacter: 140 checks including the added profile/open-window flush,
  nested-container restoration, legacy compatibility and host-setting preservation.
- Release build succeeds with zero warnings/errors.

These do not automate a running Unity multiplayer session. In-game acceptance:
1. Move the purse and another bag to different positions. Close/reopen repeatedly.
2. Change the other bag's sharing/filter/auto-dump setting on the other peer.
   Verify the correct bag changes and neither existing window jumps.
3. Close a bag locally, change its settings remotely, reopen and verify.
4. Move the purse while open, let the host save successfully, disconnect/rejoin;
   check position. Repeat after closing it before saving.
5. Check a nested bag, a solo character switch and return, and an older save.

A client crash before any acknowledged checkpoint cannot persist unsaved layout
changes; the existing profile acknowledgement/backup rules still apply.
