# Settlement and quest integration status

## Completed and committed

- `4d8d06c`: remove unconditional remote throwing XP compensation; route native owner awards once, preserve host/NPC party awards. Settlement board upgrades, research, policies and recruitment use validated host transactions and confirmed state.
- `78ac31c`: eligible shared quest acceptance and deliveries execute on host. Completion packets are results, not client authority to grant rewards. Scripted/instance acceptance remains host-controlled.
- `0aff8bc`: relay host quest-event widget text, including KKQuestTimer output. Clients do not execute duplicate quest events just to display the timer.
- `f1c463b`: confirmed resident rename/portrait/skin/PCC edits use host validation and field-level conflict checks. Human/dormant player profiles are excluded. Custom assets must exist on both machines.

## Advanced building safety boundary

Native `BaseTileSelector.TryProcessTiles` invokes task creation, tile processing and then `ExecuteSummary`. `HitSummary.Execute` charges currency/materials. `UndoManager.Perform` destroys tasks, and `TaskBuild.OnDestroy` returns stored resources through `Zone.AddCard`. Executing these UI operations independently on a client is unsafe even if finished terrain is later synchronized.

`AdvancedBuildingPatch` therefore rejects client designation/build/blueprint/deconstruction/area-selection operations at the selector entry point, before mutation/payment. It guards undo separately and wraps native area-property menu actions before opening their mutation dialogs. Host and solo retain native behavior. Ordinary adventure-mode placement, mining, harvesting and AutoAct tasks do not use these guarded selector modes.

This is a host-only boundary, **not full advanced-building synchronization**. It does not add shared live blueprint markers or area metadata, forward client blueprint batches, or support editor/debug terrain tools. A complete expansion needs explicit batch ownership, stale-state checks, host payment, idempotent cancellation, and read-only result rendering that cannot execute tasks or return stored items. Do not deserialize/execute cloned task resources on the client to obtain previews.

## In-game verification (still required)

- Host and client: test throwing against the same target conditions; compare native skill progress without the former unconditional client bonus.
- Client: upgrade/research/toggle a policy/hire; verify one host-side payment and matching board state, including reopening/rejoining.
- Client: accept an eligible random quest, deliver to its actual recipient, confirm one consumption/reward. Scripted and instance quests stay with the host.
- Both: enter harvest/music/defense quests with the timer mod on host; compare timer/wave text, then exit and confirm the text clears.
- Client: rename/customize an eligible resident; confirm host view and save/reload. Try editing the same field concurrently; newer host state should win a stale request.
- Client: attempt a queued build, cancellation, undo, area edit, and blueprint placement; expect the host-controlled message with no currency/material change or dropped resources.
- Host: perform the same building operations and confirm native behavior. Client: verify normal held-item placement, direct mining/harvesting and AutoAct still work.

Builds are staged only. No Package installation was performed during this audit. Protocol V45 requires matching builds for the newly integrated packet fields.
