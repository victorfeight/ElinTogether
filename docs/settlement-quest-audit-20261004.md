# Settlement and quest integration status

## Completed and committed

- `4d8d06c`: remove unconditional remote throwing XP compensation; route native owner awards once, preserve host/NPC party awards. Settlement board upgrades, research, policies and recruitment use validated host transactions and confirmed state.
- `78ac31c`: eligible shared quest acceptance and deliveries execute on host. Completion packets are results, not client authority to grant rewards. Scripted/instance acceptance remains host-controlled.
- `0aff8bc`: relay host quest-event widget text, including KKQuestTimer output. Clients do not execute duplicate quest events just to display the timer.
- `f1c463b`: confirmed resident rename/portrait/skin/PCC edits use host validation and field-level conflict checks. Human/dormant player profiles are excluded. Custom assets must exist on both machines.

## Planning, areas and construction follow-up

Native `BaseTileSelector.TryProcessTiles` invokes task creation, tile processing and then `ExecuteSummary`. `HitSummary.Execute` charges currency/materials. `UndoManager.Perform` destroys tasks, and `TaskBuild.OnDestroy` returns stored resources through `Zone.AddCard`. Executing these UI operations independently on a client is unsafe even if finished terrain is later synchronized.

The follow-up replaces the earlier blanket guard for ordinary settlement construction and work:

- Host construction/task footprints and working markers are rendered on clients as data only, using native guide passes. They are generic work markers, not full furniture/recipe silhouettes. No executable tasks or stored resources are copied to create previews.
- Area and room geometry, type, assigned-resident IDs, name, access, wall visibility, atrium, group and height settings are mirrored. Existing objects are reused by UID; geometry detaches without `Area.OnRemove` or task destruction. Room plates retain the synchronized settings/type. Local visited flags are left alone.
- Existing world snapshots advertise a planning revision. Native mutation hooks invalidate a cached snapshot; changed results are broadcast, and joining/rejoining clients request a baseline. No new timer is added. Unchanged invalidations do not create revisions, and marker-only updates do not rebuild room geometry.
- Client create/expand/shrink/delete area actions run on host through native area operations. Settings use the six native confirmed callbacks, field-level conflict checks, and rollback of the temporary client edit until host confirmation. Stale destructive shape edits are rejected.
- Client mining/digging/cutting and queued harvesting run through native action modes, tile selection, hit tests, summary/payment and task lists on host. This includes instant and native forced-instant terrain rules, rather than converting them into free queued jobs. Independent additive requests are revalidated rather than rejected for unrelated plan changes.
- Client cancellation runs native cleanup on host. Client undo targets only that client's last pending batch (up to ten batches, current map/session), never the host's global undo stack. Completed work is not reversed. Requests are deduplicated; stale explicit cancellation is rejected.

- Client ordinary construction resolves the selected recipe, stock item and ingredient UIDs on host; validates availability, known recipes, material compatibility, appearance, height, workshop, cost and tile placement; then runs native `AM_Build` and `HitSummary.Execute`. The requesting character pays. No separate crafting, cost or refund algorithm is introduced.
- A synchronous scope temporarily supplies the requesting actor and build selection to native code, restoring the host's action mode, build singleton, selector, Agent position, instant flag and undo stack even on exception. The native material-preference UI write is skipped only in this scope; host UI need not be open. No periodic character capture is added.
- Finished terrain is sent as native setter results, including bridges, roofs, decorations, liquids, pillars and direct corner/object rotations. Final modified/harvest state is included where native code writes it after a setter. Clients do not mine or generate drops again. Existing held-placement replay remains in place; new capture covers non-held construction and the explicit terrain scope.
- Final item placement fields are sent after native construction/movement, because `RecipeCard.Build` adds an item before setting altitude, roof/free-position and other display properties. Results validate zone and parent before applying.
- Client installed-object movement and put-away/deconstruction use exact item UIDs and original positions, followed by native task movement or `Map.PutAway`. The move menu's separate put-away shortcut uses the same request. NPC/editor deletion is excluded. Delayed selections cannot sweep up a newly adjacent item.
- Native retargeting cancels the selected item's earlier pending move before validating the new destination; the integration preserves that behavior. Cancelling/undoing a pending move does not teleport an item that has already moved.

Host/solo adventure placement, mining, harvesting and AutoAct retain their existing paths. Host/NPC non-held construction also publishes final results. This is additional result synchronization, not replacement of native building logic.

## Remaining boundaries

- **Intentional authority boundary:** blueprint copy/import, house templates, god-mode flood fill, and editor/debug tools remain host-controlled. They require distinct template/content transactions; they are not ordinary recipe selections.
- **Vanilla limitations explicitly guarded:** queued roof removal/construction does not persist roof intent, and native instant harvesting does not invoke the harvest progress callback. Use instant roof work and queued harvesting. Forced-instant roof recipes are allowed because they finish within the captured roof scope.
- **Display limitation:** planning footprints are generic work markers, not full recipe/furniture silhouettes. Area metadata and completed items/terrain are synchronized.
- **Native worker rules retained:** queued building still relies on workers fetching materials. Keep materials accessible in settlement stock; test queued completion across save/reload and client departure. The new protocol does not reserve materials, teleport supplies, or grant workers access to absent player inventories.
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
- [ ] Client: build a wall/floor/furniture item using selected ingredients and existing stock. Compare item counts and currency on both sides; only the requester pays, once. Repeat with the host build menu closed and open.
- [ ] Client: queue construction with accessible settlement stock. Let a resident complete it, then repeat across save/reload. Cancel a partially supplied job and verify native resource returns occur once.
- [ ] Client: instant-mine a block, dig/remove a floor or bridge, cut an object and remove a roof. Compare drops, terrain and currency. Verify adjacent/dependent terrain changes also match.
- [ ] Client: construct/rotate roofs, bridge pillars and raised/free-position furniture; compare material, height, direction and roof layer. Rejoin and check the result persists.
- [ ] Client: move installed furniture in instant and queued modes, including a multi-tile item. Try selecting it while the host moves/removes it; expect rejection of the stale selection.
- [ ] Client: put away an item using both deconstruction and the move-menu shortcut. Check that only selected items move and both sides agree on inventory/stockpile destination.
- [ ] Client: request queued roof work, instant harvesting, blueprint/template import or editor-only tools. Expect explanatory refusal without payment or world mutation; use the supported mode or host action.
- [ ] Both: verify normal held placement, direct harvesting/mining and AutoAct after using the build menu. These paths must remain functional.
- Host: perform the same building operations and confirm native behavior. Client: verify normal held-item placement, direct mining/harvesting and AutoAct still work.

Builds are staged only in `build/building-planning`. No Package installation was performed during this audit. Protocol V47 requires matching builds for the newly integrated packet fields.

## Automated verification

- `tests/AdvancedBuilding`: installed native IL boundary checks plus production planning, area command, cancellation and per-peer undo tests with a mocked game boundary.
- `tests/Construction`: production construction/terrain/object transactions and result application against an inert native boundary. Covers deduplication, payment ownership, invalid/stale requests, context restoration on exceptions, guarded native limitations, final properties and wrong-zone/parent rejection.
- Release compilation uses the installed Elin assemblies. Automated tests do not exercise Unity rendering, Harmony patch execution inside the running game, worker AI scheduling, or a live host/client session. The unchecked in-game items above remain required.
