# Shared magic mapping and charge correction

Run `dotnet run --project tests/MapReveal/MapReveal.csproj` with the pinned SDK.
Tests link production mapping capture/packets and charge hooks to boundary stubs.
They do not execute Harmony detours, Unity rendering, or Steam transport.

Elin ActEffect.MagicMap requires the local PC, then calls Map.Reveal (random
area) or RevealAll (blessed). Cursed mapping confuses instead. Capture only the
SetSeen(true) calls within those methods, including cells already seen by the
caster. Send those exact results: never replay the scroll or reroll its reveal.
The host accepts reports from active peers on the same map and relays the result.
This trusts native client mapping like cooperative trap discovery. Ordinary
exploration and RevealAll(false) do not share fog changes. Hidden trap/medal flags
are independent. Native map saves already store cell.isSeen.

CardChargeEvent now observes changed c_charges assignments on cached host cards,
covering native ModCharge, SetCharge and direct assignments through one hook.
Existing CardChargeDelta delivers absolute counts and dirties the client inventory.
Client prediction never publishes authoritative counts. This does not add a new
host transaction for every recharge UI, or address loaded weapon c_ammo.

Live tests with matching V19 builds:
- Normal mapping scroll from client, then host: compare revealed terrain, including
  tiles already explored by the caster. Consume one scroll and grant XP only once.
- Blessed mapping: same fully mapped terrain. Cursed: native confusion, no map reveal.
- Rejoin/change maps/save-reload: native host map discovery persists; stale packets
  must not reveal another map. Normal walking retains existing fog behavior.
- Rod zap: one charge consumed; both see the same count. Recharge/direct charge
  changes performed on host reach the client; newly generated rods initialize normally.
- Confirm inventory count and tooltips update without another cast or recharge.
