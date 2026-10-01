# Saved characters: solo play

Load the host world offline and open **ElinTogether → Saved characters**.
Choose **Play solo** beside a saved multiplayer character. The action saves the
world, exports both characters to JSON, makes a native full-world backup, and
reloads through Elin's normal loading path with the selected character active.
Normal saving persists that selection. Use the same panel to switch back.

Selector hooks are registered at plugin startup under a persistent Harmony owner,
independent of multiplayer connection lifetime. Before world initialization, the
switch updates serialized party membership and invalidates its member cache;
it does not invoke live recruitment/removal callbacks against uninitialized branches.
After loading, it verifies that the selected character actually became active.

The original human remains in the save. Existing companions remain in the party;
inventory and equipment retain their original identities. Karma and saved faith
follow the selected character. World quests, recipes and first-craft claims,
collections, and finances stay with the world.

The same personal profile now serves MP checkpoints, MP joins, and solo switches.
Capture is called explicitly for host saves and client save/disconnect paths. There is
no timed capture, fallback interval, dirty-event hook, or retry timer, and the
client Update loop does not inspect or serialize personal profiles. Checkpoints
use reliable transport; acknowledgements suppress unchanged data on subsequent
explicit captures.
The host validates the peer's character, the current save-probe session, revision,
and profile fields, then stores the record on that character. It never installs a
remote profile into the host's live Player. Normal host saving persists the latest
accepted checkpoint. Joining restores the character's profile before native UI
initialization. Faith and karma retain their existing separate synchronization.

Before a connected host saves, it requests fresh profiles from all registered
clients and waits up to three seconds inside the existing synchronous save call.
Replies must match that save's request ID, connection session and player UID.
Unchanged profiles still reply. Only profile packets execute during this wait;
gameplay packets remain queued in order. The native receive batch is released
before handlers execute, so receiving replies during an existing packet handler
does not overwrite its message pointers. Once all replies arrive, vanilla saving
writes the world and character-attached JSON profiles normally. Offline saved
characters retain their most recently saved profile.

A failed send, invalid connection or timeout returns save failure instead of
claiming fresh data was saved. Native save-and-quit/title callers consequently do
not exit. Native callers that already ignore save failures still have that vanilla
limitation (for example manual backup/map reload); this patch does not rewrite
their behavior. The normal disconnect panel and client save/reconnect paths wait
for profile acknowledgement before proceeding. The host must still save its world
to persist an independently acknowledged client disconnect checkpoint to disk.

Legacy multiplayer saves lack a complete client-local Player profile. Missing
counters and custom UI hotbars start fresh; inventory, equipment, levels, skills,
normal item/tool slots and previously saved ability layouts are retained.
A persistent missing-history marker keeps that limitation visible after
new checkpoints, including profiles created by the initial solo-selector build.
Newly created MP characters begin with a fresh profile without inheriting the
host's counters. Unknown old history cannot be reconstructed from a host save.

This feature does not import a character into another world or reassign Steam
accounts. Hosting requires switching back to the original host character. Both
peers require the new protocol version (V21); older builds are rejected by the
existing compatibility handshake.

Checkpoint acknowledgement means the host accepted the data in memory, not that
the world was written to disk. Save the host world after the checkpoint. A final
capture is acknowledged before a normal panel disconnect/reconnect, but a crash or an
abrupt disconnect can lose changes since the last accepted checkpoint. No claim
of crash-proof saving or restoration of unrecorded pre-upgrade history is made.

**Export JSON** writes an archival character document below Elin's native backup
directory, in `ElinTogether-characters`. Switches export both characters there
automatically. These JSON documents are not standalone playable saves and no
JSON importer is provided. Use the native full-world backup to restore a world.

## Automated checks

Run `dotnet run --project tests/SoloCharacter/SoloCharacter.csproj`.
The harness links production selection, profile, faith and ability-layout code
with game boundary fixtures and JSON serialization. It checks failure recovery,
switch/save/reload/switch-back, original item references, personal/world state
separation, backup output, repeated ability restoration, and acknowledged profile
checkpoints (retries, stale sessions, wrong owners, malformed data, provenance,
polymorphic hotbars and save/rejoin restoration). It does not execute
Unity, Harmony patch ordering or native map activation.

## In-game acceptance test

1. Use a copied host world with both players saved. Disconnect multiplayer.
2. Export each character and confirm the JSON files exist.
3. Select the dormant client. Confirm identity, inventory, equipment, skills,
   karma and faith; move, open inventory, equip a tool and use an ability.
4. Save, exit and reload. Confirm the selected character and progress persist.
5. Switch back to the original host. Check its inventory, counters, hotbars and
   faith, and confirm the other character remains available in the panel.
6. Switch again and check that items and ability icons have not duplicated.
7. Return to the original host before testing multiplayer hosting.
8. On both updated peers, join MP. Change the client's hotbar and tracking/UI
   preferences and gain personal progress. Invoke Save on the host. The host session
   log should report `Personal profile checkpoint accepted` with the client's UID
   and a revision. Merely waiting or moving must not trigger profile capture.
9. Save on the host, disconnect and rejoin. Verify the client's settings and
   progress, and verify the host's own settings did not change.
10. In a copy of that saved world, select the client solo. Verify the same personal
    settings; change them, save, and switch back to the original host. Host that
    world again and verify the returning client receives the solo changes.
11. With a legacy character, confirm the missing-history notice remains truthful
    after saving; with a newly created character, confirm there is no legacy
    notice and no copied host counters.
12. Change the client's hotbar and immediately save-and-quit on the host. Reload
    and verify the change. Repeat with an unresponsive client: saving must fail
    within the bounded wait and the host must remain in-game, without reporting
    a successful save. Reconnect and retry normally.

Production compilation and fixture tests are not an in-game acceptance result.

## Offline human companions

The same panel offers **Bring as companion** and **Dismiss companion** for a
saved human. Recruiting first saves the world, exports the two humans and creates
a native backup. It reuses the existing global character and item identities,
adds them to the current party, places them beside the player and lets vanilla
`ChooseNewGoal` select ally AI. The original account-to-character mapping stays intact.
There is no companion copy, profile polling or custom combat controller.

`emp_solo_companion` explicitly records this offline role. Dormant-human cleanup
keeps that character only while offline and still in the active player's party.
The usual home-branch detachment remains: saved humans do not become settlement
workers. Vanilla party membership carries companions across zones. Selecting the
companion as the player clears the role; the outgoing human is parked as before.
Dismissal removes AI/map/party presence but keeps the global character and inventory.
Dead companions can be dismissed; recruitment does not revive them.

Before opening a multiplayer lobby, all solo companions are parked. Mounted,
ridden or contained companions block startup until released; validation occurs
before any companion is changed. Joining also clears the role before normal MP
player setup, whose existing SetAI patch hands the character to GoalRemote.
No offline-companion cleanup exemption exists on either multiplayer peer.

Companions use normal NPC combat, consumption, death and progression rules, so
their skill/item changes persist on the human character. Their stored Player
profile, personal karma and god gift/prayer history are not overwritten from the
active player. NPC piety uses vanilla's NPC formula while the character is an ally;
NPC altar prayer (`AI_Pray.Pray`) awards magic XP, not the human prayer action.
This does not add human worship-day progression or player-only counters while
that human is AI-controlled, nor does it add shared-XP configuration.

Source checks (local Elin 23.338.2): `Chara.MakeAlly/_MakeAlly` also changes home,
faction and minion state, so existing same-faction humans need only Party.AddMemeber.
`Chara.MoveZone` returns immediately when currentZone already matches: direct
`Zone.AddCard` is necessary for parked humans absent from the active map.
`Zone.AddCard` reparents and registers the actor; `Chara.ChooseNewGoal` selects
GoalIdle for party members; `Chara.MoveZone` moves living party followers;
`Zone.Activate` reconstructs saved global actors. MP handoff is checked against
StartServer, SendSaveProbe, RemoveLeftOverCharas and CharaTaskRemoteEvent.

Additional manual checks:

1. Offline, recruit Dinbit. Verify one roster/map entry, following, combat and
   unchanged equipment. Change zones, save/reload, and verify they remain present.
2. Dismiss, then recruit again. Verify earned skills and equipment changes remain
   and no second character or inventory appears.
3. Switch into the companion and check player control. Recruit the original host
   as the companion, then dismiss them and switch back.
4. On the original host, recruit the client again and start MP. Verify the AI
   disappears before invites; join from the other machine and verify human control
   and the same inventory. Disconnect and confirm no autonomous human remains.
5. Mount the companion and attempt hosting/dismissal. It should ask you to dismount
   and retain both actors unchanged; retry after dismounting.

The companion fixture exercises the production role service and save/selection
paths; it does not execute Unity's map activation, native AI or Steam handoff.

## Host setting: AI controls disconnected players

`Server.DisconnectedPlayerAI` defaults to false and appears in the host settings
panel. With it enabled, a fully joined living player leaving or losing transport
can remain in the host party as a normal AI companion. The character must be in
the host's zone, attached to a zone, and unmounted/uncontained. Kicks, explicit
resyncs, incomplete joins and host shutdown never initiate takeover.

Disconnect removes network ownership/session state before selecting vanilla AI.
Immediate and deferred incoming deltas from that peer are discarded; the delta
executor also rechecks ownership during a batch. Cached client snapshots and
pending held-tool commands are discarded, and the remote child action is halted.
The combat timeline already synchronizes its participants from connected humans;
companions use the existing NPC advancement path. Shared-speed calculation drops
the disconnected player as well. The existing one-second cleanup job preserves
only the host connection's explicitly tracked companions; turning the option off
parks them on that job's next pass. No new scheduler or capture timer is added.

Rejoin stops companion AI before assigning peer ownership and serializing the save
probe. An already-owned character cannot be claimed by a second connection. Normal
GoalRemote control is explicitly restored even when MoveZone is a same-zone no-op.
Riders acquired during AI mode are detached through vanilla Unride before handoff.
Normal skill/equipment changes remain on the original character; stored personal
profiles and human prayer/gift/karma records are not replaced by the host profile.
As with offline companions, NPC actions do not synthesize player-only counters.
An abrupt disconnect can retain only the last state received by the host.

Host saves include the AI character and its offline-companion marker. Loading that
save offline keeps it as a companion. Starting another MP session follows the
existing solo-companion rule: park it first; this setting enables takeover on a
subsequent real player departure, not automatic recruitment of all saved humans.

In-game acceptance:

1. With the setting off, disconnect a joined client: their character should vanish
   as before. With it on, repeat: exactly one companion should remain and act.
2. Disconnect during harvesting and during a shared combat turn. The old task/tool
   command must stop; remaining humans must not wait for the departed player's turn.
3. Gain companion skill XP or change their gear, save, then reconnect that owner.
   Verify retained changes, one character entry, and immediate human control.
4. Turn the setting off while an AI companion is present: it should be parked.
   Kick a connected player or request resync: no AI takeover should occur.
5. Save with a retained AI, reload offline, and check companion persistence.
   Return to hosting, rejoin the owner, and check normal ownership again.
6. Repeat with another human still connected, checking their view of AI movement,
   combat, disappearance and rejoin. Unity/Steam handoff is not fixture-tested.


## Connected breaks and Ally Expansion

The Session tab has an own-character **Take a break / Take control** action.
The host owns a resting client's AI while its connection remains alive. Incoming
client actions and character/profile snapshots are rejected until an acknowledged
save/zone resynchronization completes. No periodic personal-profile capture is added.
The host's own break uses native PC goals without changing the active player.

Ally Expansion's host assignment entry points accept connected characters only
while they are explicitly AI-controlled. AutoAct child publication uses the same
ownership rule, preserving the existing terrain-completion synchronization.
Start/restart the host's AutoAct after entering a break; the extension distributes
orders on that event, not by periodically enrolling new companions. Its enable,
tool, riding, action-support and pickup settings still apply. This does not forward
a client's AutoAct orders to the host's companions.

Automated coverage: `tests/PlayerControl` links the production host/client handoff;
`tests/AutoActSolo` covers assignment and task publishing at both ownership states;
`tests/SharedCombatRounds` checks input revocation and installed Elin/Ally Expansion
patch targets. Fixtures cannot establish Unity/Steam playability.

Live acceptance (same V22 build on both peers):
1. Client takes a break while idle, then while harvesting/in combat. Confirm one
   character, host AI movement, no local movement/inventory actions, accessible menu.
2. Host restarts AutoAct harvesting/mining with Ally Expansion enabled. Confirm the
   resting client joins when equipped appropriately and sees terrain disappear.
3. Take control during work. Confirm task cancellation, brief character/map reload,
   same position, retained gear/skills/hotbar, and immediate return of human input.
4. Change zones and save while the client watches. Confirm AI survives the zone ACK,
   save succeeds without asking the spectator for its stale personal profile, and
   taking control uses the current host character state.
5. Test host break following/combat, client-initiated party sleep with the host on
   break, and normal host sleep with a watching client. Human participants must not
   wait for spectator votes.
6. Test death, disconnect/reconnect, and mounted handoff rejection. This feature
   must not revive characters or leave both human and AI simulation active.
