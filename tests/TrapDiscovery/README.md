# Shared trap discovery

Run `dotnet run --project tests/TrapDiscovery/TrapDiscovery.csproj` with the pinned SDK.
Boundary tests link the production helper/delta; they do not exercise Unity or Steam.

Elin's ActWait.Search rolls detection and grants XP before Card.SetHidden(false).
Trap activation also reveals via TraitFloorSwitch. Share only discovered TraitTrap
UIDs using world snapshots, with client-only discoveries reported to the host.
Existing native flags persist in the host map save. No search/XP/activation replay,
map reveal, hidden medals, character invisibility or new save format is involved.
The host validates the active peer/map and resolves only existing active traps.
Client discovery is trusted like existing cooperative local search; no host reroll.
This makes discovery shared and monotonic for an existing trap UID.

Matching V19 builds required. Test both directions: discover a trap on one player,
then look at that tile on the other. Unknown traps must stay hidden. Normal field
of view still applies. Test an existing discovery on join, zone revisit/save-reload,
disarm/destruction, and an undiscovered medal. No second detection XP or activation
should occur. Reports retry with snapshots until reflected in host state.
Debug log: `Shared trap discovery: zone ..., trap ...`.
