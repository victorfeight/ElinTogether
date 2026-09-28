# Shared guild state

Run `dotnet run --project tests/GuildSync/GuildSync.csproj` with the pinned .NET 11 SDK.
The harness compiles the production snapshot, replica, action handler, and patches
against boundary stubs. It checks state reconciliation and request validation; it
does not exercise Unity, real Harmony installation, MessagePack/LZ4 transport, or
the live dialogue UI. The full mod build checks the installed game API separately.

## Source-backed design

Audited against local Elin 23.338.2 source:

- `Guild`, `FactionRelation`, and `QuestGuild`: membership, rank, contribution,
  and quest progress live in world state, not individual characters.
- `Quest.CompleteTask` calls `NextPhase` before clearing `task`. Capture at the
  existing world snapshot boundary so the phase and task arrive together.
- `Quest.OnModKarma`, `OnKillChara`, and `OnGiveItem`: host owns trial progress;
  client gameplay replay must not award it again.
- `DramaOutcome.guild_*` and the doorman/clerk/merchant dialogue spreadsheets:
  one validated action request handles trial, admission, mage letter, promotion.
  Mage letter acceptance immediately admits the player in vanilla dialogue.
- `TraitBaseSpellbook.OnRead`: remote readers fail vanilla's `c.IsPC` check.
  Host completion explicitly credits ancient books and Dojin trial progress.
- Existing quest deltas carry phases/start/completion separately and do not
  cover task counters or faction relations. Guilds use one versioned snapshot;
  old quest messages cannot overwrite that snapshot. Reconciliation updates
  quest objects in place to preserve journal/tracker references and preferences.

No personal karma unification, new guild save file, or reward replay is added.
The host's normal save persists guild state. Requests verify actor, representative,
distance, expected phase/rank, requirements, and request ID. The mage letter is
consumed from the requesting character, not the host's inventory.

## Known boundary

This is the shared-state and guild-dialogue foundation, not a complete rewrite of
every transaction awarding guild contribution. Host contribution changes sync.
Client stolen-goods sales (`ShopTransaction.OnEndTransaction`) and shop investment
(a local dialogue callback in `DramaCustomSequence`) still lack host transaction
requests. Locally awarded contribution there is not guaranteed to reach the host
and may be replaced by its next guild revision. Do those transactions on the host
until their payment/item changes and guild credit can be committed together.
Do not add a generic client-supplied contribution amount: it would allow duplicate
credits and separate credit from the transaction that earned it.

Personal-karma events that execute only locally also need source-specific host
handling; the trial counter is shared, but this does not make every karma source
multiplayer-aware. Dialogue text already rendered may require reopening after
the authoritative reply. Runtime localization/message polish is not claimed.

## Two-player acceptance (both peers require protocol V12)

1. Join a host with an active thieves' trial. Compare accumulated trial karma,
   then perform one theft and one client Dojin reading. Compare the increment
   on both journals; personal karma is not expected to match.
2. Start trials from a client. Check exactly one quest on both peers, then
   fighter kills and merchant flyer gifts from each player. Check one increment
   per eligible event and completion phase with no remaining active task.
3. Have only the client hold a mage trial letter. Hand it in: consume exactly
   one letter, admit both to the shared guild, and leave host inventory alone.
4. Decode an ancient book as client while a member. Both contribution displays
   must rise by `5 + refVal * 2` once.
5. Attempt promotion without enough contribution, then with enough. Attempt
   simultaneous promotions: one expected-rank transaction succeeds, the stale
   one asks to reopen dialogue. Check rank, contribution, and access on both.
6. Save, disconnect/rejoin, and change zones. Check identical guild state,
   retained unrelated quests, and no duplicate rewards or quests.

Useful evidence: both Player.log and ElinMP session logs, selected representative,
action, and before/after journal counts. The host logs `Guild action` with peer,
request ID, phase, and rank; debug logging also records applied guild revisions.
