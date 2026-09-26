# Combat input routing checks

Links the production CombatInputBuffer with mocked game boundaries. Run with the pinned Windows SDK via `dotnet run --project tests/CombatInputRouting/CombatInputRouting.csproj -c Release`.

The checks exercise hook methods and deferred callbacks directly. They verify Fire/ability deferral, scoped cleanup, EndTurn suppression, target/weapon validation and replay exclusions. They do not install Harmony hooks or run Unity. Use the paired-player checklist in `../SharedCombatRounds/README.md` for those integration checks.
