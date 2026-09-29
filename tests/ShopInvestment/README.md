# Investment transactions: shop and town

Source checks: Elin 23.338.2 `DramaCustomSequence.Build`'s `_investShop` step,
`DramaActor` choice callbacks, `DramaEventMethod`, `CalcMoney.InvestShop/Invest`,
`Trait.ShopLv`, `Card.c_invest`, `Zone.ModInfluence`, and `Card.ModExp`.
MP checks: currency intent, element ownership/updates, world/character snapshots,
session reset, and existing guild contribution capture.

The patch wraps the native quote-building event, preserving its text/choices and
cancel callback. Only client Yes/Quick Invest callbacks become requests. It does
not depend on compiler-generated closure field/method names. An unexpected choice
shape disables payment callbacks rather than falling through to local spending.

The host validates sender, live map/zone, shop capability, distance, expected
investment level, and quoted price. GUID deduplication is per peer/session.
Price calculation temporarily selects the requesting PC because vanilla Invest
ignores InvestShop's actor parameter and reads global pc. The scope is synchronous
and restored in finally. Payment/upgrade/influence/XP/contribution use vanilla
formulas. Duplicate or stale requests do not spend. This is a synchronous game
transaction, not a database rollback mechanism for arbitrary mod exceptions.

Native currency mutations publish through existing inventory synchronization.
Only the owning client runs native `ModExp`: its rounding, parent attribute XP,
character-level XP and feat changes must not be independently rerolled on the host.
The host saves a pending award under `emp_pending_progression` on the remote
character using vanilla Card string storage. World snapshots reoffer pending
awards, including when the original dialogue receipt was lost. The client
calculates each award once per loaded session and bundles the absolute element
values, character level/XP and feat points into an acknowledged progression delta.

The production output queue absorbs ordinary local progression packets (including
deferred and unrefreshed packets) into this bundle. The bundle precedes subsequent
quick-invest requests, whose prices may depend on the newly gained skill. Host
validation requires the owning peer and outstanding award IDs. Duplicate commits
cannot roll back newer progress. Primitive values and pending-award removal are
written synchronously before existing derived-element callbacks. Observers receive
the absolute result; the owning client does not replay it.

Before acknowledgement, the host character retains the previous progression plus
the pending award. Rejoining restores that baseline from SaveDataProbe and awards
XP once again on the fresh replica. After acknowledgement, the host character has
the resulting progression and no pending award. This follows existing client
ownership; it does not independently verify client XP arithmetic. Player-global
bookkeeping (for example `Player.totalFeat`) remains outside this actor snapshot.
Arbitrary exceptions inside another mod's XP callbacks are not transactionally
rolled back. A dead client defers the award until revived.
NPC investment level and active-zone influence also travel through regular world
snapshots, covering native host investment and observers. Receipts apply the
latest shop state before quick-invest rebuilds its quote. Closed/changed dialogue
is not reopened by a late receipt. Request/result state clears on session/load.
The pending-award list is JSON in existing Card string storage; no separate save file.
Transient UI/deduplication state resets on session/load; saved pending awards remain.

Town investment (`_investZone`) reuses the same request, receipt, pricing context,
UI interception and XP acknowledgement. The host validates town capability and
absence of a guild at the location, checks the quoted development/price, adds
paid currency to cumulative investment, rolls `5 + rnd(5)` development once,
adds 2 influence and awards `100 + resulting development * 2` XP. Town investment
does not upgrade the NPC or award Merchant contribution. World snapshots include
development and cumulative investment; quick-invest uses the acknowledged state.
The cumulative investment overflow display sentinel follows vanilla behavior.

Stolen-goods sales and karma handling remain separate phases. V15 required on both peers.

Run `dotnet run --project tests/ShopInvestment/ShopInvestment.csproj` with the
pinned .NET 11 SDK. Production request/result, dialogue patch, progression acknowledgement, element
application and output queue compile against game-boundary stubs. Tests cover
transaction guards, actor-context restoration, deduplication, receipt handling,
menu callbacks, stale queued progression and before/after-commit recovery. JSON
round trips use the SDK Newtonsoft library because the game DLL targets Mono.
Fake price/XP functions are boundary tests,
not verification of vanilla formulas, Unity rendering or real transport. Full mod
build checks real game API linkage.

Two-player acceptance:
1. Record client/host money, shop investment, zone influence, Investing XP and
   Merchant contribution. Client invests once: only client pays, shop +1,
   influence +1, base XP 50 + new level*20, contribution 5 + new level if member.
2. Use different host/client investing skills; check actual price equals client's
   quote. Train/change discount after quote: host must reject stale price.
3. Quick-invest twice, normal invest, cancel, insufficient funds, move away and
   close dialogue before reply. No duplicate charge or unwanted reopening.
4. Two clients quote the same shop level, both accept: one succeeds; the other
   gets refreshed state and must accept a new quote before another payment.
5. Host invests while client observes; check snapshot shop/influence/guild update.
6. Save/rejoin/change zones; inspect persisted shop, contribution and actor XP.
   Disconnect just after acceptance to exercise pending-award recovery, then repeat
   after acknowledgement and verify that no additional XP is granted.

Host session log contains `Investment` with request ID, peer, shop UID,
accepted status, calculated price and resulting level. Runtime acceptance remains
required before calling this phase multiplayer-tested.

Town acceptance: repeat normal/quick/cancel/stale/insufficient/reconnect cases at a town representative. Verify development rises by 5–9 once, influence by 2, cumulative investment by the quoted payment, and no Merchant contribution is awarded. Host town investment must also reach client snapshots.
