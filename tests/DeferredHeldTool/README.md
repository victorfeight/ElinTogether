# Deferred held tool synchronization

Links production held-tool delta and GoalRemote. Game and transport boundaries are simulated.
Run `dotnet run --project tests/DeferredHeldTool -c Release`.

Checks running-task deferral, application before next task's tool snapshot, latest-wins,
empty hands, idle replacement, ownership validation, local-player exclusion, no rebroadcast,
and natural goal cleanup. Live acceptance: switch pick/axe during progress, then repeatedly
harvest bushes and mine without switching away to recover. Compare both peers' logs for
`Deferred held tool` and `Applied deferred held tool`.

Also links production CharaTaskDelta. Tests task-before-tool ordering, stale deferred switch superseded by task tool, barehand harvest, foreign/destroyed tools, non-harvest tasks, local player exclusion, occupied tile rejection, and observer tool setup.
