# Cassette collection reconciliation

Run `dotnet run --project tests/MusicCollection/MusicCollection.csproj` using the
pinned .NET 11 SDK. Tests compile the production helper against boundary stubs;
they do not simulate real network transport, cassette use, or Unity UI behavior.

Source trace (local Elin 23.338.2): `TraitTape.OnUse` adds `owner.refVal` to
`player.knownBGMs`, plays tape audio, and consumes one item. `Player.knownBGMs`
is saved with JsonProperty. `LayerEditPlaylist.Refresh` filters available songs
using that set. MP's save probe copies it on joining, but no prior live packet
carried it. Client cassette use already takes the CardOnUseDelta request/replay
path; host use does not broadcast that request. This change leaves those use
semantics intact and carries the resulting host set in every world snapshot.

Reconciliation mutates the existing set, refreshes open playlist menus only on
change, and never invokes item use, audio, rewards, or collection messages.
It has no per-session cache or separate persistence. V13 is required on both
peers. Current playback and map/jukebox playlist selection are outside scope.

Live acceptance:
1. Host uses an unknown cassette while client has music selection open. Client
   gains the track on the next world snapshot without reconnecting; one cassette
   is consumed on host. Reopen the menu too, to check stored unlock state.
2. Client uses another unknown cassette. Both gain it, and the cassette count
   decreases by exactly one on both peers through the existing use path.
3. Use an already-known cassette and try use in a user zone. Preserve vanilla
   behavior (known cassette still consumed; prohibited use does not consume it).
4. Rejoin/change zones; collection matches host and music does not restart from
   collection reconciliation. No repeated menus/messages on unchanged snapshots.

Debug session log: `Music collection reconciled: N tracks`. Compare both peers'
cassette counts and menus; harness success is not a two-player runtime test.
