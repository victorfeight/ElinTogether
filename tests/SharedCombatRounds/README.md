# Shared combat round experiment

Run with the repository's pinned Windows .NET SDK:

```
dotnet run --project tests/SharedCombatRounds/SharedCombatRounds.csproj -c Release
```

Build the mod to `build/shared-rounds` first. The harness links the production round ledger and loads the built mod plus locally installed Elin.dll and Harmony. It decodes the actual vanilla tick-loop IL and invokes the production transpiler, checking that the loop condition is gated and action-time subtraction remains intact. It does not run Unity, execute character goals, or simulate two connected players.

11 checks cover stale readiness/completion, duplicates, next-round clearing, session reset, and real IL transformation. The independent card/message suite has 25 checks.

## Source findings and design

- `Chara.Tick` increments `player.stats.turns` before conditions and AI execution. That identifies a spent turn even if a condition prevents movement.
- `AIAct.Cancel` calls `Reset`, disposing the goal enumerator. A phase transition must not use `SetAI(NoGoal)` to suspend a continuing task.
- `GameUpdater.CharaUpdater.FixedUpdate` subtracts `actTime` after each Tick. Merely suppressing Tick would still drain timer credit. The loop-condition patch prevents entering that iteration after a completed turn, and accumulation is frozen while waiting.
- The host waits for explicit, authenticated, round-tagged completion, not the end of an AI goal. Duplicate reports cannot grant time twice.
- The host gives NPCs the existing maximum player action-cost budget and a settling frame before the next decision pause.
- One manual step ends the manual-movement goal. Other running goals remain attached and continue next round. Replacement action clicks can be queued; an ordinary queued action still uses vanilla `Act.Perform`/`CanPerform` validation.
- Combat exit preserves queued input. Death does not require a player decision. The existing execution timeout remains a recovery mechanism.

## Deliberate limitations

This is STRICT one-turn-per-player scheduling, not a speed-fair event scheduler. Vanilla action costs and speed multipliers remain, but a fast player cannot take additional turns relative to a slower teammate. Do not claim full vanilla speed balance is preserved.

All connected players still participate regardless of distance. Manual keyboard movement is not promised to buffer while its existing movement gate is closed. No independent encounter groups or automatic distant-player waiting were added.

The completion packet and round fields require the SAME experimental DLL on host and client. This is a separate experiment from stable r3; multiplayer validation is still outstanding.

## Two-player acceptance checklist

1. Turn-based combat ON. Host auto-combats a durable enemy while client is outside combat range. Client selects one movement step per round. After each attack/step, another client decision must be possible before the enemy dies. Repeat with roles reversed.
2. Both auto-combat: at most one consumed local turn per round; both goals continue without repeated clicks. Enemies continue acting from granted time.
3. Start a long harvest/craft task while the other player moves. Progress must survive round boundaries and rewards must occur only once.
4. Click another action after spending a turn: it should run next round, not immediately or twice. Kill/remove its target first to verify vanilla validation.
5. Queue a move while deciding, then end combat: the move must not disappear or execute twice.
6. Cancel auto-combat/pathfinding: cancellation must revoke readiness; choose another action and proceed.
7. Compare unequal character speeds and costly actions. The strict equal-opportunity tradeoff must be acceptable before promoting this branch.
8. Death, forced sleep, reconnect, and a task rejected by the host must not create an endless executing wait. The pre-existing forced-sleep synchronization bug remains separate.
9. Turn-based combat OFF: vanilla continuous multiplayer flow should be unaffected.

Capture both Session logs with a test timestamp. Match `Combat completion accepted`, `Combat readiness`, phase round ids, and `TurnTrace turn-complete`. No Unity/two-player checks have been claimed as passed.
