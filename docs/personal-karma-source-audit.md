# Personal karma in multiplayer

## Ownership and integration

Vanilla's serialized Player.karma is process-global, not a Chara field.
WindowChara displays that local value. ElinTogether previously copied the host's
Player on join, selected the remote character, then let the two local values
diverge. It had no remote-karma persistence or snapshot field.

PersonalKarma now stores each remote balance and revision in the host-saved
character's integer data. Integer key overloads deliberately avoid Card.GetInt's
string overload, which can prefer the current Player's dialogFlags. The host's
own balance remains native Player.karma. ActorOwnership uses the stable session
player rather than a temporarily substituted pc when following summon masters.

SaveDataProbe contains the saved balance. OnSaveDataProbe restores the client's
Player.karma before OnGameInstantiated/OnLoad. WorldStateSnapshot includes remote
balances and revisions; stale state cannot undo newer events. API V18 requires
matching builds on both peers.

New characters start at vanilla's 30. Existing characters with no saved balance
initialize once from the old host-based join baseline. This cannot recover an
old client-only value that was never saved; it does not claim to do so.

## Two execution paths, one settlement function

Host-executed native actions run inside short actor scopes restored by Harmony
finalizers. They cover AI progress completion (including Progress_Custom's
harvest/read/steal callbacks), Act.Perform, food, read/use traits, deaths,
kicking, holding, lock operations, rescue, altar offering, poison/explosions,
and witnessed bat transformation. Scopes never span a coroutine yield.

Only karma-relevant IsPC gates are expanded, verified against the installed game
IL. IsPC itself and unrelated food/read/quest branches are not changed. Host-side
Zone.IsCrime also recognizes active remote humans. Native randomness and event
amounts stay in Elin. Client prediction/result replay does not charge again.

Some inventory/dialogue/dynamic-tool callbacks only execute locally. Their
native karma outcome is reported through LocalKarmaOutcomeDelta, bound to the
authenticated sender and a fresh join token. Ordered sequence numbers deduplicate
reports; an acknowledgement in the world snapshot removes completed outbox
entries, while unacknowledged outcomes retry after buffer clearing. A delayed
request from a previous connection cannot apply. HoldCard's host mirror is not
a second charge. Both routes call the same PersonalKarma.Change settlement.

This local-outcome route follows the existing cooperative mod's client-action
trust model. It is NOT host re-execution or anti-cheat validation of inventory,
dialogue or tool actions. It synchronizes their karma outcome, not every other
side effect of those interactions. Rebuilding all those UI transactions remains
a separate task; the shared karma foundation does not claim to solve that.

Settlement changes only the acting player's balance, clamps to [-100,100], and
passes the original signed event once to host quest callbacks. Crimes at -100
still count toward the thieves trial; positive events do not undo negative
progress. Existing guild snapshots publish shared progress. The old Dojin-only
quest-credit workaround is removed to prevent duplicate credit.

A dedicated host-to-owner event displays native karma/transition messages once.
A preceding snapshot does not swallow the notification. The client also receives
its own karma tutorial/criminal achievement. Native host/global event behavior
remains; boss loot is treated as a world reward, not an action by the victim.
Daily recovery for connected remote players rolls on the host.

## Guards

Player.IsCriminal includes ConIncognito. Guard target checks now evaluate the
actual target's owner and that player's disguise. Guard-wide friendliness must
allow a criminal player to be targeted while leaving innocent players friendly.
Refresh tracks which players stopped being criminal, so curing one player can
clear that target while another player remains criminal. Same-sign karma changes
do not call RefreshCriminal and clear unrelated fights. Join/disconnect refresh
the set of active criminal players.

## Source anchors

- Player.cs:945, 1408, 2472, 2610: storage, disguise, daily recovery, karma events.
- WindowChara.cs:377: personal display; QuestTaskKarma.cs:47: signed trial counter.
- Zone.cs:3762/3771; Chara.cs:6909: crime eligibility and guards.
- TaskMine.cs:175, TaskDig.cs:226, TaskHarvest.cs:343-416: crime outcomes.
- Progress_Custom.cs:49 and AI_Read.cs:93: callback execution boundary.
- TraitBaseSpellbook.cs:214, TraitIndulgence.cs:8: reader-only karma gates.
- FoodEffect.cs:298/470; TraitAltar.cs:281; Trait.cs:1096/1136:
  food, offerings, property and lost-property locks.
- Card.cs:6350/7613; Chara.cs:4735: kick, rescue and holding.
- ActEffect.cs:441-455/2957; ConTransmuteBat.cs:12: destruction/poison/bat exposure.
- SaveDataProbe, ElinNetClientPlayer, WorldStateSnapshot: persistence and replicas.

## Validation and live acceptance

Production Release build succeeds with zero warnings/errors. The PersonalKarma
harness exercises production settlement/replica code with game boundary stubs,
plus actual installed vanilla IL and staged production patch discovery. GuildSync
and FighterBounty regressions also pass (81 karma + 32 guild + 27 bounty checks). These tests do not boot Unity or establish
that every third-party callback runs in the expected scope.

Live acceptance with matching builds:

1. Client reads Dojin or performs a lawful-town mining/harvest crime. Only their
   karma changes; host and client see one shared thieves-trial increment.
2. Repeat with host acting. Reverse roles for innocence/criminal status and guards.
3. Client uses indulgence. Only their karma improves; theft progress is retained.
4. Check inventory/dialogue/tool karma events produce one owner notification and
   survive a subsequent snapshot without a second charge.
5. Reconnect after different balances; repeat after host save/reload. Both personal
   balances must persist. Disguise must affect only its owner's criminal check.

Debug logs include `Personal karma actor {ActorUid}: {Before}->{After}, event
{Amount}`. For discrepancies collect both Player.log and Session logs with action,
actor, approximate timestamp and before/after personal plus trial values.
