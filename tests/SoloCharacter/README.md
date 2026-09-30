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
