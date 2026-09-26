# Auto Act custom steps

Run with the pinned SDK: `dotnet run --project tests/AutoActSteps -c Release`.
Production request handlers, client coroutine/build waiting, pouring and other
serializers, and ally assignment guard are linked. Game and installed-mod types
are fakes; custom step bodies deliberately model only effects needed at the
boundary. This does not execute Unity or replace live two-peer testing.

See ../AutoActSolo/README.md for all implementation phases and acceptance checks.
