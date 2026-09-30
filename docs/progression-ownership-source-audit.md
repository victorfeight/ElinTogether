# Progression ownership and religion audit

Source basis: local `elin-decompiled-stable-23.338.2/Elin`, especially Religion,
TraitAltar, ActPray, Player, Chara, ElementContainerFaction, InvOwnerOffering and
InvOwnerDraglet, plus this fork's save and delta paths.

## Saved identity is not a complete player profile

ElinNetHostPlayerManager.SavedRemoteCharas saves Steam account -> character UID
through ElinGameIOProperty("remote_chara"). Disconnected characters remain in
the host's serialized character registry. SaveDataProbe sends the host game and
selected UID; the client changes player.chara, restores personal karma and faith,
and rebuilds ability tokens. It does not restore an entire per-human Player
object. Client game saves remain disabled.

A solo character switch must not merely change the active UID. Personal fields
need a capture/switch/save/reload/switch-back regression test first. Missing
client-only history cannot be recovered from a host save that never recorded it.

## Ownership boundaries

| State | Owner | Current boundary |
|---|---|---|
| Level, XP, feats, skill/spell elements and potential | Character | Existing absolute deltas and acknowledged progression awards |
| Inventory and equipment | Character | Host-saved objects and item/equipment deltas |
| Karma | Character | Existing saved emp_karma and acknowledged changes |
| Ability-token positions | Character | Existing emp_ability_layout; not a complete UI profile |
| God, piety, faith skill | Character | Character fields/elements; validated worship transactions |
| Worship days, prayer day, per-god gift history/mood/relation | Character | New versioned emp_personal_faith_v1 record |
| Guild membership/rank/quests, world quests and town development | World | Existing shared guild/quest/world synchronization |
| Recipe unlocks, collected cards and cassette tracks | World | Existing recipe/codex/music synchronization |
| Altars, consumed offerings, spawned gifts and companions | World objects | One host outcome, normal item/zone/condition replication |
| Faction-wide artifact bonuses | Host-led faction | Personal conversion cannot run vanilla's whole-faction religion recalculation |
| NPC affinity | Existing world NPC state | Vanilla _affinity is not a per-human relationship dictionary |
| First-craft claims, lifetime feat counter, personal statistics/UI | Incomplete personal profile | Must be traced before character switching |
| Bank/debt/fame/knowledge and miscellaneous flags | Policy and implementation still need review | Inheriting a join snapshot is not proof of ongoing synchronization |

First-craft reward claims use player.recipes.craftedRecipes; crafter discovery
uses player.knownCraft. Receiving the ticket and routing its message correctly
does not prove independent claim persistence. player.totalFeat is also distinct
from the character's unspent feat points.

## What the old religion path missed

CharaFaithDelta copied the chosen religion through vanilla's non-PC branch.
That branch does not execute normal conversion penalties and player lifecycle.
The old remote-prayer patch was a limited healing mirror, not vanilla's complete
Eyth/passive-prayer/gift/XP flow. Ordinary offerings were generic draglet replays,
although TraitAltar.OnOffer contains random takeover, item destruction, water
blessing and artifact reforging.

Vanilla stores prayed on Player, and gift rank/mood/relation on global Religion
instances. GetGiftRank reads the active PC's piety. GetPietyValue uses _IsPC,
which is false for the host's remote characters, selecting the NPC formula.

## Implemented religion boundary

FaithRequestDelta expresses prayer, manual altar conversion or offering intent.
The host resolves the sender to its active character and validates life, zone,
altar range, item ownership, selected quantity and pending progression. Passive
prayer requires the believer feat. A request ID executes only once; repeats
receive its cached acknowledgement.

FaithTransactions temporarily supplies the requesting character and personal
religion record to vanilla worship, restoring host character, prayer state,
religion globals, action context and lifetime feat counter in finally.
This is a bounded operation, not a character-switch feature. Existing karma
scoping attributes penalties to the actor. Faction religion callbacks are guarded
because vanilla recalculates every member's global artifact bonuses via pc.

World consequences run once inside the existing host simulation boundary. Normal
item, generation and condition hooks publish the outcome; explicit results cover
faith, changed elements, HP/mana, altar deity and surviving water/remainders.
Native host prayer/offering enters the same result boundary. Legacy generic
offering and prayer replay are excluded. Worship text uses the existing personal
message packet after vanilla formats it in the requesting player's context.

Raw prayer XP and the numeric part of an accepted offering use the existing saved
OwnerProgressionAwards acknowledgement protocol. The owner runs potential,
rounding and level-up calculations. Offering calculation preserves vanilla's
piety cap, faith-training threshold, takeover bonus and contraband penalty;
the host does not execute a competing XP roll. Pending awards must settle before
the next worship transaction can evaluate gift eligibility.

The host persists active worship days, personal prayer day and per-god history.
Hourly mood changes update the remote player's record separately. Rejoining does
not award disconnected days. Revisions reject stale daily/hourly records arriving
after a newer transaction. Client daily callbacks reconcile days afterward to
avoid counting a received day twice. Both humans use the player piety formula.

## Migration and remaining boundaries

- The host's native religion state is preserved. An old remote character has no
  reliable per-god gift ledger: the new ledger starts empty, retaining its saved
  god, elements and worship days. Historical remote gift claims cannot be
  reconstructed; this can make old gifts claimable again. Old transient prayer
  cooldowns likewise cannot be recovered. Review before live installation.
- Gifts become shared world objects. Vanilla artifact uniqueness/purge rules are
  retained; separate gift eligibility does not promise duplicate artifacts can
  coexist in the same faction.
- Standard altar worship is covered. Chaos-altar offerings retain their separate
  existing path. Vanilla healing-god campaign conversion preserves its campaign type. Custom
  scripted conversions and faction artifact effects outside the bounded flow
  still need dedicated coverage;
  this is not a claim that every faith-related system is personalized.
- A new personal profile for first-craft claims, counters and other Player fields
  is not implemented here. Neither is solo character switching.

## Validation and live acceptance

tests/FaithReplication links production persistence, transaction and offering-
progression code against simulated game/network boundaries. Its prayer fixture
is not the real game: gift thresholds, Unity effects, Harmony runtime hooks and
visual inventory results require live validation. tests/ShopInvestment checks
that extending the shared award format preserves the existing investment path.

Live acceptance on a copied host save, with matching builds on both peers:

1. Choose different gods; verify only the converting human receives penalties
   and changed piety. Repeat with both humans choosing the same god.
2. Offer one item from a stack, then the whole stack. Check exact consumption,
   piety/faith XP, no duplicate drops, and an immediate follow-up prayer.
3. Bless/curse water; test neutral and hostile altar takeover and artifact
   reforging. Both screens must show the same surviving item and altar deity.
4. Pray normally, twice in one day, after a day rollover, and with the passive
   believer feat. Check HP, mana and conditions on affected party members.
5. Test actual gift thresholds on both characters; verify gifts spawn once and
   claiming one does not advance the other human's gift ledger.
6. Disconnect/rejoin and save/reload after conversion, prayer and offering. Verify
   god, days, prayer eligibility, gift history, XP and quantities persist.
7. Check the host's faction artifact bonuses before/after a client conversion;
   verify unrelated player state remains unchanged.

Build output is staged separately. This refactor has not been installed or
certified by a two-player in-game test.
