# Personal karma regression harness

Build the production mod into `build/personal-karma`, then run:

```powershell
dotnet run --project tests/PersonalKarma/PersonalKarma.csproj -c Release
```

The harness links production PersonalKarma and ActorOwnership to small game/network
boundaries. It checks actor attribution, clamping and signed quest events,
persistence/join restoration, stale snapshots and notification ordering, client
replay suppression, local-only outcome sequencing, acknowledgements, retry and
reconnect rejection. It also loads the installed Elin.dll and staged mod DLL to
verify the actual eligibility/guard/bat IL transformations and scope targets.

It does not launch Unity, replace the installed mod, or simulate Steam delivery.
See docs/personal-karma-source-audit.md for the cooperative trust boundary,
legacy-save initialization and the two-player acceptance checklist.
