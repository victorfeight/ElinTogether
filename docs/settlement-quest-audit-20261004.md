# Settlement and quest integration status

## Completed and committed

- `4d8d06c`: remove unconditional remote throwing XP compensation; route native owner awards once, preserve host/NPC party awards. Settlement board upgrades, research, policies and recruitment use validated host transactions and confirmed state.
- `78ac31c`: eligible shared quest acceptance and deliveries execute on host. Completion packets are results, not client authority to grant rewards. Scripted/instance acceptance remains host-controlled.
- `0aff8bc`: relay host quest-event widget text, including KKQuestTimer output. Clients do not execute duplicate quest events just to display the timer.
- `f1c463b`: confirmed resident rename/portrait/skin/PCC edits use host validation and field-level conflict checks. Human/dormant player profiles are excluded. Custom assets must exist on both machines.

## Planning and area follow-up

Native `BaseTileSelector.TryProcessTiles` invokes task creation, tile processing and then `ExecuteSummary`. `HitSummary.Execute` charges currency/materials. `UndoManager.Perform` destroys tasks, and `TaskBuild.OnDestroy` returns stored resources through `Zone.AddCard`. Executing these UI operations independently on a client is unsafe even if finished terrain is later synchronized.

The follow-up replaces part of the earlier blanket guard:

- Host construction/task footprints and working markers are rendered on clients as data only, using native guide passes. They are generic work markers, not full furniture/recipe silhouettes. No executable tasks or stored resources are copied to create previews.
- Area and room geometry, type, assigned-resident IDs, name, access, wall visibility, atrium, group and height settings are mirrored. Existing objects are reused by UID; geometry detaches without `Area.OnRemove` or task destruction. Room plates retain the synchronized settings/type. Local visited flags are left alone.
- Existing world snapshots advertise a planning revision. Native mutation hooks invalidate a cached snapshot; changed results are broadcast, and joining/rejoining clients request a baseline. No new timer is added. Unchanged invalidations do not create revisions, and marker-only updates do not rebuild room geometry.
- Client create/expand/shrink/delete area actions run on host through native area operations. Settings use the six native confirmed callbacks, field-level conflict checks, and rollback of the temporary client edit until host confirmation. Stale destructive shape edits are rejected.
- Client queued mining/digging/cutting/harvesting uses native task validation and task lists on host. Independent additive requests are revalidated rather than rejected for unrelated plan changes. Instant mode and native forced-instant terrain operations are not silently converted into free jobs.
- Client cancellation runs native cleanup on host. Client undo targets only that client's last pending batch (up to ten batches, current map/session), never the host's global undo stack. Completed work is not reversed. Requests are deduplicated; stale explicit cancellation is rejected.

Host/solo behavior and ordinary adventure placement, mining, harvesting and AutoAct keep their existing paths.

## Remaining boundaries

- Client construction (including queued building), installed-object movement, deconstruction, instant terrain editing, and blueprint copy/import remain guarded. Full support still needs host recipe/material selection, native payment and placement semantics, plus result validation. This follow-up does **not** claim those gaps closed.
- Editor/debug terrain tools are outside this integration.
- Scripted/instance quest initiation remains host-controlled; this is not changed by planning synchronization.
- Custom appearance files are not transferred. Both installations still need the assets.
- Automated checks cover native IL boundaries and production transaction code with a mocked game boundary. They cannot certify Unity rendering or live paired play; use the tests below.

## In-game verification (still required)

- Host and client: test throwing against the same target conditions; compare native skill progress without the former unconditional client bonus.
- Client: upgrade/research/toggle a policy/hire; verify one host-side payment and matching board state, including reopening/rejoining.
- Client: accept an eligible random quest, deliver to its actual recipient, confirm one consumption/reward. Scripted and instance quests stay with the host.
- Both: enter harvest/music/defense quests with the timer mod on host; compare timer/wave text, then exit and confirm the text clears.
- Client: rename/customize an eligible resident; confirm host view and save/reload. Try editing the same field concurrently; newer host state should win a stale request.
- Host: queue construction and gathering work; client should see work footprints appear, change when worked, and disappear on completion/cancellation. Rejoin and verify the current baseline, then leave the zone and check old markers clear.
- Client: create, expand, shrink and delete an area. Compare boundaries on host. Rename it and change access/group/height/atrium/wall visibility; compare both views and save/reload. Repeat with a room plate. Simultaneously edit the same setting and confirm a stale edit cannot overwrite the newer value.
- Client: with instant mode off, designate mining/digging/cutting/harvesting. Host should receive one batch and native workers can execute it. Queue separate batches from both sides, then client undo: only that client's latest pending work should disappear.
- Client: cancel host-created work containing gathered materials. Confirm native refunds occur once on host and existing item synchronization shows the result. Try cancelling while the host changes the same plan; expect rejection/resync instead of removing newer work.
- Client: attempt construction, instant terrain work, installed-item movement, deconstruction and blueprint import. Expect the host-controlled message with no payment or local mutation. These are remaining unsupported actions, not implemented client features.
- Host: perform the same building operations and confirm native behavior. Client: verify normal held-item placement, direct mining/harvesting and AutoAct still work.

Builds are staged only in `build/building-planning`. No Package installation was performed during this audit. Protocol V46 requires matching builds for the newly integrated packet fields.
