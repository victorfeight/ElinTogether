# Combat movement timing refactor

## Source cause

ActionModeCombat.ReportPcTurnConsumed previously discarded GoalManualMove after every combat step. Vanilla AM_Adv.SetManualMove resets turbo to zero unless Shift is held. A new goal per step therefore breaks the continuous goal/run lifecycle. AM_Adv.AxisMove also tracks startedRun; clearing turbo does not itself reset that latch. This path predates the recent renderer patch.

The first presentation patch separately forced base render speed across every combat frame, excluding turbo. It stabilized pause transitions but preserved a visible walking/running difference. That BaseGameScreen.Draw transpiler is now removed, not layered over.

CharaMoveDelta previously carried only position/type/zone. Remote ticks reconstruct actTime locally, and the receiving screen has its own turbo. Neither is necessarily the sender's movement timing. Old/new stable CharaRenderer.UpdatePosition timing is unchanged (23.338.2 vs 23.352).

## Final implementation

- Retain a running manual goal while a direction is held, as vanilla does. Before readiness/dispatch, clear released active manual movement only. An explicitly queued tap survives key-up until its first dispatch or explicit cancellation. The existing timeline remains the only authority for performing steps; native extraMoveCancel cannot add a released-key step.
- Add action duration and run multiplier to the existing move packet (API V54). Record the same values for local rendering at the movement event. Actual final position is sent, not requested input.
- Replace the global combat clock override with a scoped CharaRenderer.UpdatePosition hook. While drawing an in-flight combat step, use that mover's duration/run multiplier and the viewer's selected display speed. Restore actTime and render globals in a finalizer. No simulation time, turn costs or unrelated actors/effects are changed.
- Timing is weakly keyed by character object and bound to position/zone. No polling, persisted state, or new logging. Invalid/absent timing falls back to vanilla. Outside combat and mandatory pauses remain native.
- Large corrections retain the native SetFirst reset instead of interpolating across walls. Movement replay still uses Stub_Move: this work does not claim to fix all underlying positional divergence.

## Evidence and validation

Earlier paired session 109775244135409777: 306 of 323 host completions costing 0.26 were followed by Deciding within 0.26 seconds (median 0.024 seconds). These are actions, not exclusively moves. Native movement/animation/input control flow explains concrete flaws; existing logs don't prove every observed slow step had the same trigger.

Focused tests exercise sender/receiver pace mismatch, simulation/render isolation, finalizer restoration, walk/run replacement, zone/position invalidation, bad timing, idle behavior and snapping; installed IL confirms renderer timing and SetManualMove turbo calls. Existing combat timeline regression suite checks opportunity ratios and grants. Release/repress/held-direction behavior still requires an in-game input test; stubs do not establish Unity behavior.

In-game: both peers need V54. Hold a direction through several combat turns, release while waiting and verify no extra step, repress and change direction, compare Shift/autorun with walking, compare host and client views, exit/re-enter combat, and verify speed ratios remain unchanged. Build staged only unless explicitly installed afterwards.

## V54 tap regression correction

Client session 109775244207834751, 2026-10-06 20:31:47.787 UTC: GoalManualMove queued, ready true at .821, ready false at 20:31:48.053, host grant at .121 followed by cost=0 with no pending goal. It repeats next epoch. Clearing `_pendingAi` on key-up erased taps during the host round trip.

Removed that queue mutation; consolidated active-goal release into PlayerActivity.ClearFinishedLocalGoal's optional manual-release cleanup. Held goals remain alive, completed/released active goals stop, pending commands remain under scheduler cancellation/dispatch ownership. PlayerActivity tests simulate delayed dispatch and held/released goal cleanup; actual Unity/network input validation remains necessary.
