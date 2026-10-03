# Host-owned shop transfers and settlement

Run with the pinned .NET 11 SDK:

```
dotnet run --project tests/ShopTrade/ShopTrade.csproj
```

`ElinSourcePath` points to the local Elin 23.338.2 decompilation by default; it can
be overridden with `-p:ElinSourcePath=...`. The suite compiles production trade
commands and presentation guards alongside **the actual vanilla
ShopTransaction.cs**, with game/UI/network boundaries stubbed. It uses real JSON
serialization and a JSON stand-in for LZ4 compression. Nullable warnings from
vanilla's unannotated source are expected. This does not exercise Unity UI,
Steam transport, native inventory mutation hooks, or the game save serializer.

## Behavior

Normal `InvOwnerShop` trades now send one host command, before the old ThingRequest
payment path. The host checks peer/actor, merchant/map/distance, item ownership
(including that peer's validated dangling drag), quantity, currency, native quote,
funds, destination capacity, protected/container and equipped-item restrictions.
The host splits, pays, records the native ledger and transfers the item. Ordinary
purchases use native `Chara.Pick`; explicit destinations retain their container
and slot. Drag purchases reserve the paid item in the buyer's inventory so a
lost reply cannot lose it. They require room for that reservation before payment.

The client completes drag presentation under remote-state landing. The scoped
FreeTrade/IsValid guards prevent another payment or stale local money check.
Direct transfers do not re-execute native financial logic. Paid drag cleanup uses
native UI.EndDrag with its action callback suppressed, avoiding a second sale.
Closing the UI while a request is pending waits for its reply before settlement.
One request per client shop session is in flight at a time.

Paid cursor presentation detaches from the actual buyer container before vanilla
OnStartDrag, rather than trying to remove the purchase from the old shop button's
container. A matching late host reservation is also detached while that exact
cursor remains active. Ending the drag/reset clears this transient presentation
state; other items and other destinations are untouched. The host keeps its paid
reservation for disconnect safety. Explicit same-container placement relocates
the reserved host item, clearing its old slot first. A reply cannot replace an
unrelated cursor that was started while waiting.

Regression checks cover both reservation/reply arrival orders, unrelated updates,
drag end/reset, and ordinary drag isolation. UI methods are boundary stubs; actual
Unity cursor callbacks and transport ordering still require the acceptance run.

Each player's native net-purchase/net-sale ledger is persisted on their host
character. Only ledger data and archived items are stored, not UI objects or the
host's global ShopTransaction.current. Native GetPrice/Process handle price and
partial reversal matching. Unidentified sales preserve their native exclusion.

Close/disconnect/rejoin settles once. Purchases queue owner Negotiation XP through
the existing acknowledged progression mechanism. Net Money stolen sales credit
shared Thief contribution. Deed/Garokk purchase counters follow the existing
world-owned Player flags; close broadcasts their authoritative values. Host-local
shop close also broadcasts item flags/counters. Ordinary non-money purchases
clear stolen flags without Money-only XP/contribution.

Archived ledger items never enter CardCache. If an item is off-map when a saved
ledger settles, a persisted UID flag update is applied to the real item once it
becomes available. Active world snapshots and scene initialization service these
updates. Simultaneous remote ledgers are isolated; a player's close does not
relock the merchant chest for another active session.

Copy/service windows and BranchMoney/home-resource transactions keep their
existing path; this phase does not migrate them. Karma/theft intents and fighter
bounty attribution remain separate. Host commits are synchronous, not rollback
transactions for arbitrary exceptions thrown by other mods. Both peers need V16.

## Two-player acceptance

- Sell stolen stacks, buy back part/all, then close. Compare shared guild credit
  with the final net sale; returning everything should not award contribution.
- Buy and return all/part; compare money and Negotiation XP after close/rejoin.
- Sell an unidentified stolen item; preserve vanilla identification/no-credit rule.
- Use normal click/drag, shift purchase, split quantities, explicit inventory slots,
  alternate currency, vending machine, empty/full inventory, and cursed equipment.
- Close during a pending purchase and disconnect while holding a paid item. Check
  host ownership and saved ledger; rejoin without another payment or reward.
- Buy a rod by ordinary click with one free backpack slot. While it is on the
  cursor it must not also occupy a client backpack slot. Wait several seconds,
  then place it in a chosen backpack/hotbar slot, sell it back, or drop it. Compare
  identity/count/charges and money on both peers, then reopen/rejoin.
- Two clients plus host trade with one merchant. Check independent refund ledgers,
  no forced UI reopening and no premature chest lock.
- Save/reload with an open ledger; inspect world purchase counters, stolen flags
  and pending owner XP. Test both realtime and turn-based configurations.

Host session diagnostics: `Shop trade ...` records accepted payment/quantity;
`Shop trade settled ...` records owner XP and shared contribution.
