# Fighter bounty attribution and remaining karma work

The karma follow-up below records the audit at the bounty phase. Its implemented
follow-up and local-outcome trust boundary are documented in
[personal-karma-source-audit.md](personal-karma-source-audit.md).

Implemented against local Elin 23.338.2 and verified against the installed Elin.dll.

## Native payout boundary

Card.DamageHP(long, ...) checks the killing origin's PC faction/minion status,
updates shared kill/guild progress, then checks GuildFighter.HasBounty, rolls a
reward, announces it and pays EClass.pc.ModCurrency. On the host, pc always means
the host even when the client or their summon killed the enemy.

The transpiler changes only the bounty message and that payout recipient. It
preserves eligibility, random amount, native currency mutation and shared guild
contribution. ActorOwnership resolves active remote players before following
master links, including saved summon masters. Unowned shared allies retain the
native host recipient. Ownership cycles terminate safely.

ModCurrency uses ThingContainer.AddCurrency: existing stacks use ModNum; new
stacks use owner.AddThing and SetNum. These continue through existing inventory
synchronization. No client-provided award amount or new payout packet is added.

CardDamageHpDelta replays the native damage body on clients through a reverse
patch. Its scoped replay flag suppresses HasBounty only during that replay,
preventing another roll/message/payment even if the reverse snapshot predates
the payout transpiler. Normal HasBounty calls remain untouched, including
Chara's bounty tooltip. The replay scope restores on exceptions/nested calls.

PersonalMsgSayPatch routes the existing bounty message to the resolved player.
It retains its existing host fallback on failed delivery. No new notification
protocol is needed. API V17 requires matching builds on both peers.

## Validation

Production Release build: zero warnings/errors. FighterBounty harness: 27 checks,
including real installed game IL matching, preserving other pc/currency/random
calls, ownership, message scope and client replay versus tooltip eligibility.
The installed Harmony ReversePatcher.GetTranspiler implementation was also
inspected: it finds a compiler-generated local transpiler on the reverse stub.
The production damage stub now supplies that local function, reusing the same
idempotent payout transform. Tests verify Harmony discovers the compiled function
and that both registration orders work. This closes the case where a Snapshot
was captured before the normal bounty patch was registered. Production replay
scope tests additionally cover nesting, exception cleanup and HP reconciliation.

This does not launch Unity or prove network delivery. Live checks still needed:
host kill, client kill, client summon kill, exact single currency increase,
recipient-only announcement, retained bounty tooltip, reconnect persistence.

## Remaining karma boundary (not implemented here)

Player.ModKarma has no actor argument and operates on the global Player. It also
runs crime witnessing, criminal-state refresh and quest callbacks; the latter
receive the signed event before karma is clamped. QuestTaskKarma counts matching
signed events, so deriving trial progress from a clamped balance is incorrect.

Zone.IsCrime gates on c.IsPC, excluding host-side remote players. Harvest/mine/
dig therefore can skip the crime event entirely. Zone.RefreshCriminal and guard
AI consult global Player.IsCriminal. A pc swap or forwarding a karma amount
would leave guard targeting and host state inconsistent.

A subsequent phase must identify actors at each authoritative action boundary,
persist personal karma with the remote character, evaluate crime and guard
eligibility per actor, and apply guild quest progress exactly once from the
validated signed event. Audit stealing/slaughter/restraint/food/indulgence and
existing remote Dojin progress too; do not double-count replay or overwrite
host karma. Service/copy/home-resource trade windows also remain separate from
the completed normal-shop transaction boundary.

## Follow-up source findings and implementation sequence

SaveDataProbe.Create serializes EClass.game. ElinNetClientPlayer.OnSaveDataProbe
replaces the client's game with it and changes Player.chara/uidChara, without
restoring an actor-specific karma balance. PlayerCharaStateSnapshot only carries
turn/action state. Merely syncing a UI value would not survive reconnect.

Player.IsCriminal combines karma < 0 with pc.HasCondition<ConIncognito>(); an
actor-specific equivalent must evaluate the actual actor's disguise condition,
not just store another integer. Zone.RefreshCriminal alters guard-wide hostility
and clears enemies; Chara.IsHostile independently tests criminal status for a
party target. Both require review together to avoid one player's karma deciding
whether guards attack the other player.

The implementation sequence is:

1. Store the remote actor's karma on its host-saved character, initialize an
   explicit migration/default, and restore/snapshot the local Player mirror on
   join. Do not treat the host's current balance as a durable remote balance.
2. Add an actor-explicit authoritative event helper preserving signed pre-clamp
   quest increments and [-100,100] personal bounds. Reuse ownership resolution
   where the event actually has an origin; do not infer actors from stale Act.CC.
3. Integrate crime eligibility and guard target checks, preserving disguise,
   zone law and existing native hostility rules. Test host criminal/client
   innocent and the reverse, with disguise and reconnect.
4. Connect validated native outcomes in small families. Task owners cover
   mine/harvest/dig/steal, but progress replay must not charge again. Existing
   CharaProgressCompleteEvent exposes the owner while completing an action;
   its static context does not cover all callbacks or inventory transactions.
5. Inventory theft must attach to a successful host transfer. InvOwner.Transaction
   mutates property/karma before splitting; a generic client karma message cannot
   prove that a transfer happened. Forced gifts have separate flag/affinity and
   quantity-dependent penalties. World taxes, quest outcomes and unattended shop
   sales need an explicit shared-event policy, rather than a guessed player.

Service/copy and home-resource transactions remain distinct: InvOwnerCopyShop
is not InvOwnerShop; InvOwner.Transaction calls destination OnProcess for service
side effects, while UseHomeResource spends a home resource rather than actor
currency. Route these through explicit validated operations only after tracing
those outcomes; extending the normal-shop type check alone is insufficient.
