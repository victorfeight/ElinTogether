# Personal faith regression checks

Run `dotnet run --project tests/FaithReplication/FaithReplication.csproj` with the
.NET 11 preview SDK used by this repository's standalone regression checks.

The harness links the production personal-faith record, transaction dispatcher
and owner offering-progression code. Game, transport and JSON boundaries are
small stand-ins. In particular, the prayer fixture deliberately mutates personal
gift history and healing to exercise isolation; it does not model vanilla gift
thresholds or claim that a real prayer grants both a gift and healing.

Checks cover sender/zone/life/range/quantity validation, item ownership,
once-only resolution, reply retries, restoring host context after exceptions,
saved personal state, independent hourly mood, stale result rejection, day
rollover ordering, native host outcome capture, client pending actions, and the
numeric piety cap/faith-training/contraband sequence.

See docs/progression-ownership-source-audit.md for source references, migration
limits and live acceptance cases. Passing this harness does not exercise Harmony,
Unity UI, actual item generation or real save IO.
