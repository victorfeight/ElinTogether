# Fighter bounty attribution and remaining karma work

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

Production Release build: zero warnings/errors. FighterBounty harness: 18 checks,
including real installed game IL matching, preserving other pc/currency/random
calls, ownership, message scope and client replay versus tooltip eligibility.
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
