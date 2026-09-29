# Guild trade phase: source boundary and remaining implementation

Status: normal shop trade implementation completed in source; staged, not installed.
See `tests/ShopTrade/README.md` for coverage, boundaries and live acceptance.
The flow below records the pre-fix RCA and the source requirements.
Source: local Elin 23.338.2 decompilation. Shop and town investment use the
separate validated Investment command; normal shop trading now has its own host command; service/copy and home-resource windows remain outside this phase.

## Existing flow

- `LayerInventory.CreateBuy` constructs an `InvOwnerShop`, assigns the singleton
  `ShopTransaction.current`, and registers `OnEndTransaction` on inventory close.
- `InvTransactionEvent` requests a host item through `ThingRequest`; the reply
  runs the original client transaction under `IsReplayingIntent`.
- `ThingRequest` splits/removes the host item before the UI finishes. Its dangling
  item recovery returns unclaimed items after ten seconds. A reply is therefore
  not proof that a purchase or sale committed.
- Native `InvOwner.Transaction.Process` validates, splits, computes the ledger's
  price, changes currency, processes identification/ledger entries, then places
  the item or starts dragging it. Currency and item placement reach the host as
  independent deltas. The host has no corresponding per-client trade ledger.
- Native shop close computes Negotiation XP from net purchases and Thief guild
  contribution from net stolen sales. Local contribution is subsequently replaced
  by the host-owned guild snapshot.

## Native behavior the replacement must preserve

`ShopTransaction.GetPrice/Process` cancels opposite entries by **item definition
ID and price**, not merely UID. Partial reversals consume only matching quantities.
`HasBought/CanSellBack` instead use the actual Thing reference. One matching rule
cannot replace both. Netting must occur before close-time rewards.

An unidentified sale that was not previously bought is identified and **skips**
`ShopTransaction.Process`. Crediting every transferred stolen item would diverge
from vanilla. Money is the only currency eligible for the close-time XP/guild
rewards. Bought items have their stolen flag cleared, including non-money trades.
Money trades also clear stolen flags on qualifying net sold entries.

Native `OnEndTransaction` additionally relocks the merchant chest, clears item
integer 101, and increments land-deed/Garokk-hammer purchase counters. Running it
wholesale with a temporary `pc` swap would still mutate the host's global Player
flags and conflict with the host's own `ShopTransaction.current`.

Drag purchases charge before final placement. `InvOwnerShop.OnCancelDrag` returns
false; generic inventory cancellation and a completed reverse trade are different
paths. Do not replace all drag operations with automatic inventory pickup.

## Required implementation sequence

1. Add a per-peer/merchant session ledger, separate from the host's UI singleton.
   Validate merchant, current zone, item ownership, quantity, native quote,
   currency, important/container restrictions and merchant sale eligibility.
2. Commit payment and transfer under one host trade command. Reply with resolved
   split/stack identity and authoritative outcome. Local UI completion must not
   emit a second payment or transfer. Preserve drag/drop and pending item recovery.
3. Feed only committed operations into the ledger. Reuse native price/netting
   semantics with the correct actor context; reject stale quotes and duplicate
   operation IDs. Keep identification's ledger exclusion explicit.
4. Settle each ledger once on close/disconnect; persist enough state to prevent a
   save/rejoin from losing committed sale credit. Owner Negotiation XP can reuse
   the acknowledged progression award infrastructure. World guild credit remains
   host-owned. Define ownership of global purchase counters explicitly.
5. Test partial stacks/buybacks, unidentified sales, non-money merchants,
   interrupted drag/drop, stale quotes, duplicate close, disconnection and two
   clients at one merchant. Client-reported guild amounts are not authoritative.

Karma attribution, PC-gated guild events and fighter bounty attribution remain
separate from this trade boundary. They must not be claimed fixed by investment
or guild snapshot synchronization.
