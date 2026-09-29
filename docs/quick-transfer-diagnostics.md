# Shift-click adjacent-item transfer investigation

Diagnostic build: quick-transfer-v2. No transfer/input behavior or wire format changes.
Production Release build passed against the installed game references (0 warnings/errors).
Not yet tested inside a running two-player session. Staged separately from Package.

## What is established

The client session log at 2026-09-29 08:11:06 UTC shows distinct UIDs 39509 and
39101 entering container 4704 about 30 ms apart. This proves two item moves, not
which input/refresh initiated them. The user's observation is one Shift-click
moving the selected and adjacent item on host or client, emerging after recent work.
Do not dismiss the report as pointer movement or call the RCA settled.

Local Elin 23.338.2 source has two transfer entry paths:
- ActionMode.UpdateInput uses Shift + raw held left mouse and the hovered ButtonGrid.
- UIButton queues its callback; ButtonGrid's callback calls InvOwner.OnClick(this),
  which reads the live button item and calls OnShiftClick.

OnShiftClick moves/stacks one selected Thing. Normal grid removal leaves a hole;
UIInventory normally suppresses automatic sorting while Shift remains held.
A second call alone therefore does NOT prove how a different Thing was selected.
CardAddThingDelta addresses exact item/container UIDs, not UI slot indices.

## Trace coverage

All entries start with QuickTransfer. Existing logger enrichment supplies peer role,
MP session and tick. Entries include local frame/time, local gesture and attempt ID,
mouse position/down/held/up, cached and raw Shift, right mouse, input skip and replay.
Gesture IDs are process-local: never match host/client by gesture ID alone.

- trace-start: quick-transfer-v2 activation marker.
- press/release/hover: physical gesture edges and hovered button/item changes.
- pointer-down: exact button/item captured before vanilla schedules its callback.
- button-callback: button/item when InvOwner.OnClick actually executes.
- transfer-enter/exit: invocation route/call stack and selected item's outcome.
  Consecutive identical invocations are summarized to avoid full stack spam.
- button-rebind: old/new card and container when SetCardGrid rebinds a UI button.
- sort: inventory container and caller while the gesture trace window is active.
- grid-reassign: item invX/invY changes in ordinary ThingContainer.RefreshGrid.
- packet-queued: AddRemote accepted an add/remove/stack delta into its queue.
  This is not evidence of delivery. Host forwarding is expected to produce this too.
- packet-apply: received add/remove/stack delta begins, with origin peer and IDs.
- move-enter/exit: native Card.AddThing source/destination, requested slot/stacking,
  returned item and actual final source item state, including errors/skipped calls.
- stack-enter/exit: selected source/target and result during a transfer or replay.

Packet and movement logs run on the receiver even with its inventory closed.
No RemoteCard.Find or extra CanShiftClick/Sort/Refresh invocation is performed
for tracing. No protocol fields, cooldowns, input consumption or mutation changes.
Diagnostics are temporary and add logging/allocation overhead. Remove after RCA.

## In-game capture

Both players should run the same diagnostic package after closing the game.
Use an ordinary box and two adjacent, differently named, non-stackable items;
record the names and approximate time if the unwanted second transfer occurs.

1. Client transfers into box; host observes contents. Then reverse direction.
2. Host performs the same test; client observes contents.
3. Compare a normal short click with a held click, keeping the pointer stationary.
4. Separately sweep across slots while held as a control; this is not an assumption
   about what caused the reported failure.
5. Repeat the originally failing inventory/auto-sort setup, including stackables
   if needed. No settings change is required for the first capture.

Keep each other from manipulating that same box during the initial isolated test.
Then test concurrent use only if isolated attempts do not reproduce.

Collect from BOTH computers, before starting another game session:
- %USERPROFILE%/AppData/LocalLow/Lafrontier/Elin/Player.log
- %USERPROFILE%/AppData/LocalLow/Lafrontier/Elin/ElinMP/Logs/Session_YYYYMMDD.log

Player.log exposes Harmony/load failures; session JSON logs contain the detailed
trace. Keep full files, label host/client, and supply the approximate event time
and selected/unwanted item names. No new logging config is required.

## How to distinguish causes

- Same gesture, different button + mouse position: held-input selected another slot.
- Same button, different UID with rebind/grid-reassign between: UI selection changed
  during the gesture; sort/callback order narrows the cause.
- Pointer-down A, later callback B: queued callback read a rebound button.
- Local input A only, locally queued A+B: investigate mutation/stack side effects.
- Sender queued A only, receiver moved A+B: investigate receiver/replay; forwarded
  packets and nested AddThing during merges must be accounted for.
- Two local transfer entries with the same UID do not establish item duplication;
  compare final parent, returned stack target and actual received UID before deciding.

A clean compile does not establish Harmony runtime patch installation or live RCA.
Verify trace-start and the relevant event sequence in the first test session.

## Captured RCA and staged fixes (2026-09-29, transfer-reconnect-fix)

Evidence preserved under `C:/Users/Vic/elin-investigation/transfer-placement-20260929`.
Host source is Downloads/Session_20260929 (2).log; client is the normal local session.
Both belong to session 109775243349808771 and build 0.26.274.16652.

### Confirmed double transfer

Host 16:38:48.904 UTC, frame 27522, gesture 25: held-input transfers gloves 38315
from player 1 to box 25053. Mouse remains (1464,598), cached/raw Shift both true.
At .911 UIInventory.CheckDirty -> onBeforeRedraw -> Sort runs. At .917 button
-20918/index28 is rebound from gloves to ring 12523. At .943 the delayed OnClick
transfers that ring through the SAME button. A second instance occurs with heart
UIDs 40917 and 38326 at 16:40:24. Host sends both; client applies both.

Vanilla's `firstMouseRightDown` latch bypasses the ordinary held-Shift sort guard.
The observed onBeforeRedraw sort while raw Shift is true requires that exception
in this source path. The narrow MP fix clears the stale latch before CheckDirty
while Shift+left mouse are held. It preserves the existing shift-release redraw,
rapid separate clicks, deliberate sweep transfers and the client sort merge fix.
This proves the mechanism, not which recent commit first made it noticeable.

### Client placement / outbound updates

Client 16:37:19.826 UTC: chest 28040/chest6 is moved locally into character719 and
CardAddThingDelta is queued. At .829 it is still valid in inventory slot15. There
is no matching host item delta apply in the supplied log. The host does receive
character snapshots and reconciles position at 16:37:23, while host deltas keep
arriving at the client. No exceptions appear in either peer's current-session JSON.
The traces do NOT record actual send/flush or TaskBuild entry in that installed build.

The earlier ace61c8 placement replay-order fix and 964359b renderer fix remain in
source; neither was reverted by the diagnostic build.

Installed Plugins.Modding.dll's `DeferredCoroutine` condition overload stores a
STATIC coroutine handle and shared pending callbacks, but starts the runner on the
supplied MonoBehaviour. Disconnect calls StopAllCoroutines on that connection.
If this happens before readiness, the runner is stopped without clearing its
static handle; the next join can enqueue its sender-start callback without a live
runner. This is a concrete source defect consistent with this log's first interrupted
join (16:33:19 before zone activation) followed by retries. Logs do not expose the
static coroutine handle, so live confirmation of that exact runtime state is pending.

The fix replaces ONLY the sender-start conditional helper call with a connection-owned
iterator. Cancellation cannot leave a global runner handle that blocks subsequent
connections. Send cadence, build replay ordering and wire format stay unchanged.
A sender-start marker, actual item/build batch-send result, and build queue/apply/
completion diagnostics now distinguish the boundaries the first trace did not cover.
Unrelated replay stack-probe logs were also removed to reduce diagnostic overhead.

Validation: production build and focused startup/sort tests; live MP retest required.
Staged separately; no game files replaced during investigation.

## Client validation after installation

Latest client session on September 29, build 0.26.274.17881:
- 17:58:54 UTC: client delta sending starts after readiness.
- 18:00:00, 18:00:08 and 18:00:13: placement requests queue and send successfully;
  authoritative replies arrive and complete with the expected target UIDs
  (39856 once, 3546 twice), each at distance 1.
- Four captured Shift-click gestures each have one transfer entry. No extra
  adjacent-item transfer is present in these samples.
- No current-session exceptions; warnings before joining are handshake-gated
  incoming packets. This validates ordinary placement and sampled client transfers,
  not every possible transfer or the interrupted-join reproduction itself.

The transfer-reconnect-fix build is installed locally; previous build was backed up.
