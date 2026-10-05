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

Installed after the audit with explicit user approval: `Package/Mod_ElinTogether/ElinTogether.dll`, SHA256 `0DA39C6E0312AE545CA5F48FC9BCB0C027460F2E5B8685A4DB011ED90DEA6E85`. Previous DLL backed up to `C:/Users/Vic/ElinTogether-backups/20261004-230050-before-8f604cb`. Protocol V47 requires matching builds on both machines.

## Automated verification

- `tests/AdvancedBuilding`: installed native IL boundary checks plus production planning, area command, cancellation and per-peer undo tests with a mocked game boundary.
- `tests/Construction`: production construction/terrain/object transactions and result application against an inert native boundary. Covers deduplication, payment ownership, invalid/stale requests, context restoration on exceptions, guarded native limitations, final properties and wrong-zone/parent rejection.
- Release compilation uses the installed Elin assemblies. Automated tests do not exercise Unity rendering, Harmony patch execution inside the running game, worker AI scheduling, or a live host/client session. The unchecked in-game items above remain required.

## Live session tracking — October 4, 2026, 23:16–23:21 local

Read-only review while both users play. No gameplay/config changes or installation during the live review.
Client lobby/session: `109775244024259428`; actor `719`; host actor `1`; active zone `432`.
Evidence: `Player.log` and `ElinMP/Logs/Session_20261004.log`, structured UTC window `2026-10-05T06:15` through `06:21:38`.

| Recent work | Live evidence / remaining verification |
| --- | --- |
| Owner skill awards / throwing (`4d8d06c`) | Client received 84 unique throwing awards: 41 x 2 and 43 x 3 raw XP, total 211. Logged base skill 34, stored XP 35 to 70. Host base skill 17, stored XP 371 to 419. Both are gaining; client awards are not duplicated in this interval. |
| Throwing popups | MoreNotifications loaded its float ModExp patch successfully. Local config has Skill_Enabled=true, Skill_Threshold=5. Observed small awards/actual gains are below the notification threshold. Host config not yet inspected. |
| Shared settlement upgrades/research/policy/hiring (`4d8d06c`) | No transaction-specific confirmation in the captured client window. Host resolution logs and paired action tests still needed. |
| Shared quest acceptance/delivery (`78ac31c`) | Not exercised/confirmed in reviewed window. |
| Quest-event timer relay (`0aff8bc`) | No active quest-event test confirmed. Needs visual comparison during a supported event. |
| Resident appearance (`f1c463b`) | No confirmed cosmetic edit observed. Host transaction logs plus visible/save-reload comparison needed. |
| Planning/area state and edits (`e601817`) | Network snapshots are flowing, but that alone does not certify area state/markers. No dedicated successful area command observed on client. |
| Construction, terrain and object transactions (`8f604cb`) | No specific successful build/terrain transaction observed on client. Their execution logs are principally host-side. Need paired tests from checklist. |
| Roster/reserves (`60c497d`), resident roles (`03fbba0`), maid dialogue (`7b067cd`) | Still tracked; no corresponding action verified in this review. |
| Chest unlock/practice relock (`e9df6e7`) | Still tracked; no unlock/relock test confirmed. |
| Profile/connection support | Joined successfully. Profile revisions 1 and 2 acknowledged with accepted=true and no pending revision. Shared sleep and condition/element updates observed. Heartbeats show connected, active receive and no profile/control block. |
| Older food disappearance/stale-item report | OPEN, RCA unconfirmed. Tent/processed Christmas food reportedly could not be eaten and disappeared after travel/reload. Do not apply speculative food fix. Correlate exact food UID, pending split UID, inventory/held ownership, eat request, native cancellation/completion, FoodEffect consumption, final quantity/removal and UI refresh on both peers. No matching reproduction identified this session. |

Four WorldStateSnapshot warnings occurred at 23:16:14 while AwaitingIntegrity; later join/profile acknowledgement succeeded. The later heartbeat fault count is the same retained startup count, not evidence of repeated disconnections. No Error/Fatal session records or managed exception/patch-failure lines found in this reviewed live window. Player startup also reports a Steam app-list HTTP 404; the game joined despite it.

Vanilla reference: ActThrow special throw branches have distinct awards; normal damaging throws route through AttackProcess.ModExpAtk, which uses target level versus attacker skill, modifiers and randomness. ElementContainer.ModExp then applies potential/skill scaling and stochastic fractional rounding; Element.ExpToNext is 1000. The removed unconditional client +50 is not part of this normal-combat calculation. The absence of a notification is not evidence of absent XP.

Requested evidence: host current Player.log and matching Session log in Downloads; item thrown, target and approximate time. Host logs remain useful for raw award/ack and host-only feature transactions even though the client log already proves both throwing XP values increased. No changes to notification threshold made.

Training-loop exit reviewed at ~23:26: AIPracticeDummyArgs creates a DelegateProgress placeholder on host; attacks arrive through existing melee/ranged/throw requests, not a duplicate host training loop. CharaTaskCancelEvent sends mapped cancellation; CharaTaskCancelDelta resolves DelegateProgress.Represents and cancels/relays. Client heartbeat transitioned AI_PracticeDummy/Progress_Custom at 23:18:16 to NoGoal/NoGoal at 23:18:26, and again 23:25:16 to NoGoal/NoGoal at 23:25:26, remaining idle through 23:26:26. Client exit confirmed; individual host cancellation receipt still requires host log. No code changes.

Follow-up: user reports occasional apparent training-exit stalls despite successful sampled exits. OPEN until a timed reproduction distinguishes input consumption from cancellation/state failure. Native AM_Adv left/right mouse-down calls TryCancelInteraction, consumes the click, and returns; the Wait action also cancels a manually cancellable AI. AI_Practice.CanManualCancel=true. Advise release held inputs, click once on the map to cancel, then separately move after acknowledgement. Prior sampled idle transitions do not rule out intermittent failures.

### Throw/training restart RCA and correction

Host evidence: Player (21).log covers the live lobby and shows training -> NoGoal -> training -> NoGoal at host 13:25:01/11/21/31 (client local 23:25). This proves a restart occurred, not whether it was intentional. Session_20261005 - Copy.rar contains exactly the extracted log (CRC 87F3E732), ending at UTC 06:20:21; it does not contain the later attempts. No current-session cancellation exception was recorded.

Source defect: ActThrowDelta invokes static ActThrow.Throw with its explicit owner but without establishing Act.CC. Native Throw sets TC/TP, not CC; its final returning-weapon training branch calls Act.CC.SetAI(new AI_PracticeDummy). AttackProcess.CC is a separate instance field. Received throws can therefore create training on the wrong local actor. Even with correct context, a late own result can restart a cancelled loop. The XP change in 4d8d06c did not introduce the missing actor context.

Implemented ThrowTraining.Replay to set and restore CC/TC/TP in finally, with a nested replay scope. The existing CharaTaskRemoteEvent prefix refuses AI_PracticeDummy assignments in that scope before task publication. Initial client training starts from its original queued throw using the native eligibility conditions (returning item, destroyable trait, dummy/restrained target, local player, stamina, not already training), through existing SetAI/task/progress replication. Host/solo native starts remain intact. No protocol fields, polling, save changes, forced-idle workaround, or cancellation-ack behavior change. Debug events identify deliberate client starts and blocked replay starts.

Validation: Release build to build/throw-training, 0 warnings/errors. ThrowProgression: 57 checks, including installed native IL, built guard-before-publication IL, simulated late own/other-player results, host wrong-actor case, nested/exception context restoration, training eligibility and original XP regressions. Not a live Unity/network test. Package installation unchanged.

Installed on 2026-10-05 into Package/Mod_ElinTogether after confirming Elin was closed. Installed DLL SHA256: e7464d8a6c5c3000c0d76499d9b3f770ff651e04bcae9d378a52dd99b913ed44. Prior DLL backed up under ElinTogether-backups/20261005-000053-before-throw-training.

Live result: on 2026-10-05 the user confirmed that the installed fix resolved the reported training issue. Exact test steps and coverage of both timing modes were not supplied; do not infer those from the confirmation. The separate missing-task cancellation acknowledgement concern remains an unconfirmed edge case, not the established cause of this resolved report.

### Faction rename and terrain brush coverage (2026-10-05)

Faction rename report: client chose CalmTears; host retained Home. Native TraitFactionBoard's confirmed callback directly assigns Home.name, bypassing existing board transactions. Added a RenameFaction operation to the shared board command, host validation and name in BranchBoardState. The native chooser-open callback requests current state; the confirmed callback submits instead of writing locally. Host-initiated renames use the same transaction. Global name state applies even if the originating settlement has changed. Native host save remains the persistence source. Callback selection is anchored to the Faction.name write and LayerList.SetStringList, with signature/count checks. BranchBoards suite: 50 checks passed, including native callback IL and request/result validation.

Terrain brush report: client raising/lowering terrain did not appear on host. Native AM_Terrain.OnProcessTiles directly writes Cell.height and bridgeHeight, bypassing captured map setters and existing construction commands. Added a host-validated TerrainBrushCommand with native mode, center, radius and Shift. Client retains native timer/fixed-center input handling but does not mutate terrain. Host calls native AM_Terrain.OnProcessTiles with a scoped Shift value (restored in finally), preserving native slope, flatten, water-boundary, bridge and clamp behavior. A shared hook captures changed cells for both host-originated strokes and client requests; existing ConstructionTerrainDelta gains Elevation results and refreshes room/render state on clients. No new polling or save format.

Terrain request checks: active owner, live/enabled actor, current zone and settlement/tent, valid center, enum mode, radius 1..32, nonempty ID and duplicate protection. Existing completed-work undo limitation remains: this does not introduce terrain undo. Construction suite: 54 checks passed (installed native IL and simulated gameplay/network boundaries). Combined Release build in build/terrain-brush passed with 0 warnings/errors; includes both fixes and requires protocol V48 on both peers. Installed and SHA256-verified on 2026-10-05 in Package/Mod_ElinTogether (c0c7b5248094921d51d26fc3a4d106496c6ac26f6d3812e633ecefa62f3b7993); prior DLL backed up in ElinTogether-backups/20261005-004033-before-terrain-faction. Live confirmation pending. Re-enter the map after updating to replace pre-fix client-only terrain edits, then test host/client raise/lower/flatten, Shift and bridge terrain. Rename from each peer, reopen the chooser, and save/rejoin to verify shared name persistence.
