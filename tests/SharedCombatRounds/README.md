# Speed-aware combat timeline experiment

Branch: `experiment/shared-combat-rounds`. Replaces the strict equal-action prototype in commit `5be1f91`; stable card/message work is preserved in `251e8f1`.

## Design and source trace

1. Vanilla `AM_Adv` feeds `ActPlan.Item.Perform` and `SetAIImmediate` for ordinary clicks/movement. Its Fire shortcut also calls `Act.Perform` or `Chara.UseAbility` directly. The latter must be captured before damage, ammunition, or mana effects, not merely at EndTurn.
2. Input buffers one latest decision. When a character is due, the scheduler installs the pending goal with SetAI and explicitly dispatches one Chara.Tick. Nested ticks are guarded. Vanilla increments `player.stats.turns` before conditions and AI; the final `actTime` includes speed/action/terrain modifiers. A consumed turn reports that cost, even if a status prevents the intended action.
3. `CombatTimeline` stores each living connected player's next eligible timestamp. Equal deadlines share a decision window; players whose deadlines are later do not block readiness. Completed actions set next = now + actual cost. Zero-cost cancellation requests another decision without advancing enemies.
4. Once all due players finish, the host advances to the earliest remaining deadline, credits that elapsed interval to host-simulated NPC timers, and lets the vanilla character updater settle those timers before opening the next window. NPCs retain vanilla per-character update order within a slice; this is not a newly ordered global queue of every NPC action.
5. Player/remote timers receive no wall-clock credit while active. The existing updater transpiler gates the actual loop condition so skipped ticks cannot drain action time. Player ticks are dispatched explicitly. On exit, player timers (including host mirrors) are rebased to their outstanding debt.
6. Auto-combat, pathfinding, and long-task enumerators remain attached between opportunities. Do not use SetAI(NoGoal) as a pause: AIAct.Cancel calls Reset and disposes them. Manual movement ends after one step; its next input can be buffered because the obsolete TryMove rejection gate is removed.
7. Phase packets carry an epoch, due-player IDs, clock, and deadlines. Readiness and completion are authenticated and epoch-tagged. Duplicate completion cannot charge twice. A timeout retries the SAME epoch; clients resend acknowledgements rather than replay actions or discard missing debt.
8. Death/disconnect removes a participant from the barrier. New/revived players are seeded from host-side timer debt. Combat entry also uses current timer debt, preventing unconditional free turns. Host mirrors remain the available estimate for remote entry timing; live validation must check encounter transitions.

Example: with equivalent action costs of 0.5 and 1.0, both act at time 0, only the faster player acts at 0.5, and both are eligible again at 1.0. The deterministic test obtains 200 vs 100 actions in 100 time units.

## Automated validation

Build the mod to `build/shared-rounds` using the repository's pinned Windows SDK. Then:

```
dotnet run --project tests/SharedCombatRounds/SharedCombatRounds.csproj -c Release
dotnet run --project tests/CombatInputRouting/CombatInputRouting.csproj -c Release
dotnet run --project tests/CodexTransactions/CodexTransactions.csproj -c Release
```

Timeline tests link the production scheduler and cover unequal speeds, costly actions, zero-cost decisions, invalid/stale/duplicate reports, membership changes, debt retention and resets. They also load the built mod and the installed Elin.dll, decode its real tick-loop IL, and invoke the production transpiler. Unity's bundled MonoMod reader cannot run correctly under this standalone SDK, so the small IL decoder is test-only.

Input tests link the production input-buffer patches with fake game boundaries. They check that input does not cause effects until dispatch, mana is deferred, EndTurn cannot overwrite buffered input, removed targets/transferred weapons are rejected, and scoped input cleanup does not capture scripted effects or network replay.

These tests do not run Unity, actual Harmony patch installation, live AI goal progression, or two connected players. No live acceptance result is implied by passing unit/IL checks.

## Two-player acceptance checklist

Install the SAME experimental package on host and client with both games closed. The new packet format is incompatible with r3/strict-round builds. Preserve saves and the known r3 package for comparison.

1. Unequal speeds, equivalent repeatable actions: match `turn-complete` costs and due IDs across both logs. Faster player must receive intermediate opportunities rather than wait for a slower character's whole goal. Reverse host/client roles.
2. Separate players; let one auto-combat a durable enemy while the other walks. The walker should get decisions when due, before the enemy dies. A due idle player STILL waits for input; no automatic distant-player wait was added.
3. Both auto-combat: goals must persist, enemies must keep acting, and each acknowledged action must appear once.
4. Test the Fire shortcut separately from mouse attacks, with ranged weapons, thrown weapons, and abilities. Queue while not due: no early damage/ammunition/mana payment. Remove target or transfer weapon before dispatch.
5. Start a long craft/harvest/path goal. It must retain progress across due windows and produce rewards once. Cancel/replace it and confirm the new decision is honored.
6. Queue a movement key or click while waiting, release it, then end combat. No missing input, duplicate step, or free timer reset.
7. Force rejection/cancellation, death, disconnect/reconnect, and movement animations spanning phase changes. No completed turn should repeat after acknowledgement retries.
8. Turn-based mode OFF: ordinary multiplayer flow should match r3.

## Limits

This changes character action scheduling. Existing calendar/weather/environment update policies, shrine/overflow behavior and the separate forced-sleep synchronization bug are not redesigned. Host-side initial remote timer estimates and live action-cost application need paired-log verification. Due living players remain participants regardless of distance; this is not independent encounter simulation.
