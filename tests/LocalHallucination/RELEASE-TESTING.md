# Multiplayer acceptance

Both players must close Elin and install the same build (network API V8).

1. Client uses calligraphy on a weapon. On completion, inspect that same weapon on both peers: spell and charges must agree; confirmation must reach the client. Test a host craft too.
2. Inflict and cure hallucination separately on each player. Only the affected player sees distorted sprites. Rejoin while affected and while healthy.
3. Switch pick/axe during task progress, then harvest bushes and mine repeatedly. The tool switch is deferred until current task cleanup, before the next task snapshots its tool. Log markers: Deferred held tool / Applied deferred held tool.

Automated: 39 transaction/message checks, 13 hallucination checks on transformed vanilla IL, and 9 deferred-tool checks passed. Full mod build has zero warnings/errors. Harnesses simulate game/transport boundaries; live multiplayer acceptance remains necessary.

The discarded tool update is a confirmed code defect consistent with the reported recovery after switching tools. Existing client logs show repeated cancellations but lack the host held-tool state; they do not conclusively establish this defect caused that specific incident.
