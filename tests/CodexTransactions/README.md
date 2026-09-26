# Shared codex transaction checks

Run `dotnet run --project tests/CodexTransactions -c Release` with the repository SDK.
The project links the production CodexRequestDelta handler and supplies in-memory
substitutes for the Elin and network boundaries. It does not run Unity, Harmony,
MessagePack serialization, or a live peer session.

Required in-game verification on a copied host save, with the same updated DLL on both peers:

1. Collect a new enemy card as client with auto-collect enabled: one shared increase;
   the physical card disappears on both sides. Repeat as host.
2. Disable auto-collect, pick up cards, then use right-click Collect and book Add Cards.
   Each deposited stack must be credited once on both peers.
3. Withdraw as each player: exactly one physical card goes to that player and the shared
   count decreases once. Auto-collection must not consume the withdrawal.
4. Full bag without a matching stack: reject without decrement or ground spawn.
   Full bag with a compatible stack: allow stacking and decrement once.
5. Both players withdraw the final card: only one succeeds.
6. Reconnect and save/reload: book count and physical card inventory persist.
7. Without an MP connection: vanilla codex behavior is unchanged.

The host save's codex is authoritative. Previously divergent client-only collection
counts are not imported or added to it, which would risk crediting the same drops twice.

Message routing checks also link production PersonalMsgSayPatch with mocked formatting/transport. They cover collector routing, nested and exception cleanup, first-craft host suppression, failed/muted delivery, existing craft/refuel routing, toggle suppression and unrelated messages. These do not validate Harmony hooks in Unity; use the packaged two-player checklist.
