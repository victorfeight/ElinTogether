# Deferred held tool synchronization

Links production held-tool delta and GoalRemote. Game and transport boundaries are simulated.
Run `dotnet run --project tests/DeferredHeldTool -c Release`.

Checks running-task deferral, application before next task's tool snapshot, latest-wins,
empty hands, idle replacement, ownership validation, local-player exclusion, no rebroadcast,
and natural goal cleanup. Live acceptance: switch pick/axe during progress, then repeatedly
harvest bushes and mine without switching away to recover. Compare both peers' logs for
`Deferred held tool` and `Applied deferred held tool`.

Also links production CharaTaskDelta. Tests task-before-tool ordering, stale deferred switch superseded by task tool, barehand harvest, foreign/destroyed tools, non-harvest tasks, local player exclusion, occupied tile rejection, and observer tool setup.

Links production `RemoteHeldItem` too. The vanilla `HoldCard` and `PickHeld` boundary
methods now throw instead of pretending to be side-effect-free assignments. This
detects reintroducing the inventory/drop/renderer operations into tool replay.
Checks ownership/count preservation, nested containers, destroyed/empty targets,
local-player exclusion, and held-light FOV refresh. These are isolated boundary
tests, not a live Unity/network test.

Live overflow acceptance (same build on host and client): exceed backpack capacity,
keep axe/pick in the hotbar, and alternate tools and empty hands both while idle and
during harvest progress. Both peers must retain the tools in the client's inventory;
no tool should appear on the host's floor. Also place an owned box, switch a held
light on/off, and verify intentional drops still work. Do not replace capacity rules
or suppress real drops to satisfy this test.
