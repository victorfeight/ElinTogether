# Shared Return and Evacuation (API V53)

Client scrolls previously ran vanilla's local return countdown while their host
replay saw a non-PC caster and substituted ordinary Teleport. Client MoveZone is
deliberately blocked; only the host can change the shared map.

The existing AI_Read and CharaActPerform channels now identify and deduplicate
travel requests. No second scroll command or new map-transfer protocol is added.
Host request admission checks peer control, actor, zone and item ownership.
Travel scroll OnRead is host-only: its native failure RNG, decrement and Literacy
XP run once. Other scrolls are unchanged. Spells retain existing cost handling;
host replay executes only the effect, not UseAbility again.

SharedTravel owns the requesting human and host driver. A short effect scope
lets vanilla recognize the remote caster as PC, then restores the host before
the destination menu or countdown can run. Native returnInfo and Chara.Tick
remain the countdown and departure implementation. No polling timer is added.

The host chooses Return's destination using vanilla LayerList and the installed
ListReturnLocations provider (including BetterReturn). Evacuation uses vanilla's
world-map exit. Tp Return retains its own instant/anywhere logic but joins the
same ownership and cancellation lifecycle. Ordinary NPCs retain native fallback.

The caster's absolute overweight phase is checked before departure; vanilla
still checks the host driver's weight and survival restrictions. Other party
members retain existing shared-travel behavior. A different player cannot toggle
off a pending journey. Recasting by its owner retains native cancellation.
Busy scroll reads are refused before consumption; spell costs already handled
by the client are not refunded when the host refuses their effect.

Death, revoked control, disconnect, actor/map changes and session shutdown cancel
pending travel. Native menu callbacks are guarded by journey identity, so a stale
selection/close cannot overwrite a newer journey. Pending MP returnInfo is omitted
from both personal profile capture and world saves; live state is restored in the
save finalizer. Solo profile behavior remains native.

Validation: SharedTravel boundary/lifecycle tests plus native IL checks;
SoloCharacter and TpSpells regressions; release compilation against Elin 23.352.
These are not live two-player Unity tests.

In-game checks (same V53 build on both peers):
- Client Return scroll: one consumed, host destination picker, both arrive.
- Client Evacuation scroll: one consumed, both leave to the native destination.
- Client ordinary Return spell and Tp Return: one spell cost; no local teleport.
- BetterReturn extra destinations and instant setting still work on the host.
- Cancel destination selection, then start again; no old callback restarts travel.
- Both players cast during one countdown; second actor cannot cancel the first.
- Caster/driver overweight, caster death and Take a Break during countdown.
- Save while counting down, continue live, then reload: no saved MP countdown.
- Solo Return/Evacuation and unrelated scrolls remain unchanged.

The reported unfinished combat message is separate. AttackProcess emits an
attack prefix, then Card.DamageHP emits the damage/kill ending. MP predicts the
first and replays resolved damage later; DamageHP can exit on an already-dead
target, and kill text also depends on origin.isSynced. This explains a plausible
missing suffix but does not identify the precise cause of the reported grudge
event. No combat-message or damage changes are bundled into this feature.
