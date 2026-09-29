# Fighter bounty regression checks

Build the production mod to `build/guild-bounty` first, then run:

```powershell
dotnet run --project tests/FighterBounty/FighterBounty.csproj -c Release
```

Uses production ownership/transpiler code with game-boundary stubs for ownership
and replay tests. Separately loads the staged production DLL and installed Elin
DLL and applies the actual transpiler to decoded native DamageHP IL. Requires
the local Steam Elin installation and Harmony path configured in the project.
These checks do not replace live host/client testing or install the mod.
