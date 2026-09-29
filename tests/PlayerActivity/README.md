# Remote player activity and idle pause

The production snapshot now reports idle (action ID 0) for dead players,
no-goal actions and finished controllers. `PauseGame` combines a connected
remote player's existing `LastAct` report with its reconstructed running task.
Dead/out-of-map characters are excluded; combat phase rules still take priority.
No new packet or independent activity timer is introduced.

Finished local goals are normalized through `SetNoGoal` from the existing
per-frame multiplayer update in both time modes. This runs before input even
when character ticks are paused, preventing an idle report from stranding a
finished controller behind vanilla's `HasNoGoal` movement gate. Running goals,
remote characters and dead players are excluded; combat cancellation guards
and queued decisions remain intact.

`dotnet run --project tests/PlayerActivity -c Release` links the production
activity helper and pause patch with simulated game/network boundaries.
The tests cover the between-task gap, cancellation/completion, invalid reports,
disconnect, death, zone mismatch, combat phases and existing client/solo behavior.
They do not validate Unity scheduling or actual network timing.

Mandatory `Game.Pause` pauses are preserved even in MP. Ordinary menus still
do not pause other players. The harness also covers mandatory pause/resume on
both peers, combat execution/settlement, disconnected vanilla behavior, and a
modeled expired-quest event loop with a busy remote player. That model verifies
the pause boundary, not Unity layer destruction or the complete zone transition.

Live acceptance with both peers on this build:

1. Enable the host's vanilla auto-pause and leave her idle. On the client,
   start Auto Act on multiple separated flowers/walls. Selection, travel and
   repeated work should continue without host input.
2. Cancel Auto Act, then finish an entire batch. With both players idle and no
   running remote task, the world should pause again.
3. Repeat during turn-based combat: choosing/waiting must still obey the combat
   scheduler. Also verify auto-pause disabled and single-player behavior.
4. Check disconnect and zone transition while automation is active.
5. With host idle, finish or cancel a batch, then immediately move using keyboard
   and mouse. Repeat with no remaining target and with manual cancellation during
   work. No host movement should be required to release the client's input.
6. Let a farming quest expire while the client is still harvesting/using Auto Act.
   Leave the host's completion pause open for several seconds, then dismiss it.
   Expect one return to town, accessible rewards, and no unsolicited world-map
   transition. Repeat for a timed music/wedding quest when practical. Confirm
   opening an ordinary inventory still lets the other player act.

The timed-quest fix prevents repeated completion callbacks; it does not relocate
rewards already stranded in a saved map. Client logs can confirm the transition
sequence, but host logs/runtime observation are needed to verify the pause itself.

The activity handshake uses the existing 5 Hz world snapshot exchange, so a
short network/update delay remains possible when starting or stopping activity.
