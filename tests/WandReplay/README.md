# Wand replay regression checks

The client used to send only ActZap's ability ID after execution. ActZap needs its bound TraitRod, and vanilla mutates static Act.TC/TP while applying effects. Generic replay could neither reconstruct that trait nor reliably preserve the ground aim. Locally generated summons were subsequently destroyed by the existing prediction cleanup.

ActZapEvent now captures the item-bound request before vanilla execution; CharaActPerformEvent excludes zaps to avoid sending it twice. The existing CharaActPerformDelta carries wand UID, copied aim, zone UID and a request ID. The host authenticates the caster and rod ownership, deduplicates, binds TraitRod, and calls vanilla ActZap within ElinDelta.Simulate. This makes the existing generation/placement/charge/condition hooks publish authoritative results. The host does not relay the cast for clients to simulate another summon. Client prediction cleanup remains in place.

Run `dotnet run --project tests/WandReplay -c Release` with the pinned SDK. Tests link production capture, replay, generation and placement hooks against small game/transport boundaries. They cover frozen aim, lazy action IDs, request uniqueness, once-only authoritative charges, generation before placement, replay-scope restoration, ownership/zone validation, empty rods, non-summon effects and standalone exclusion. These are not Unity runtime or real spell-effect tests.

Live acceptance (same build on both peers):
1. Client uses shadow and animal rods. Host should log `Zap applied` with the correct wand/effect, one charge decrement and a larger actor count; client receives zone placements and persistent minions. Temporary prediction cleanup messages can still appear and do not mean authoritative summons died.
2. Aim a silence rod at an enemy one tile away. `Zap requested` aim must match `Zap applied`, not the caster's tile. Check target condition on both peers; resistance/empty charges remain vanilla outcomes.
3. Repeat with host casting, turn mode on/off, zero charges and multiple consecutive casts. No duplicate minions or charge deductions.
4. Check existing inventory/identify rods separately: local PC-only behavior remains predicted while host simulates the authoritative item effect. This change does not replace vanilla effect rules.
