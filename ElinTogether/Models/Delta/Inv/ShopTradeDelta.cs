using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using MessagePack;
using Newtonsoft.Json;
using UnityEngine;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class ShopTradeDelta : ElinDelta
{
    [Key(0)] public Guid SessionId { get; set; }
    [Key(1)] public Guid RequestId { get; set; }
    [Key(2)] public RemoteCard? Merchant { get; set; }
    [Key(3)] public RemoteCard? Item { get; set; }
    [Key(4)] public int Quantity { get; set; }
    [Key(5)] public int Price { get; set; }
    [Key(6)] public int ZoneUid { get; set; }
    [Key(7)] public bool Sell { get; set; }
    [Key(8)] public bool Close { get; set; }
    [Key(9)] public CurrencyType Currency { get; set; }
    [Key(10)] public PriceType PriceType { get; set; }
    [Key(11)] public bool Drag { get; set; }
    [Key(12)] public RemoteCard? Destination { get; set; }
    [Key(13)] public int DestX { get; set; } = -1;
    [Key(14)] public int DestY { get; set; } = -1;
    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host) ShopTrade.Resolve(host, OriginPeer, this);
    }
}

[MessagePackObject]
public sealed class ShopTradeReplyDelta : ElinDelta
{
    [Key(0)] public Guid RequestId { get; set; }
    [Key(1)] public RemoteCard? Item { get; set; }
    [Key(2)] public LZ4Bytes? Ledger { get; set; }
    [Key(3)] public string? Error { get; set; }
    [Key(4)] public int Price { get; set; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetClient) ShopTrade.Receive(this);
    }
}

// Native netting and refund prices are reused rather than reimplemented. Only
// durable data is saved; never serialize InvOwner's UI or the global singleton.
internal sealed class ShopTradeLedger
{
    public Guid Id;
    public int MerchantUid, ZoneUid;
    public CurrencyType Currency;
    public PriceType PriceType;
    public List<Line> Bought = [], Sold = [];
    public sealed class Line
    {
        public RemoteCard Item = null!;
        public int Num, Price;
        // An archived ledger item must never resurrect itself into CardCache.
        public Thing Resolve() => CardCache.Find(Item.Uid) as Thing ?? Item.Data!.Decompress<Thing>();
    }
    public ShopTransaction Restore(InvOwner trader) => new() {
        trader = trader,
        bought = Bought.Select(RestoreLine).ToList(), sold = Sold.Select(RestoreLine).ToList(),
    };
    private static ShopTransaction.Item RestoreLine(Line line) => new() { thing = line.Resolve(), num = line.Num, price = line.Price };
    public void Capture(ShopTransaction transaction)
    {
        Bought = transaction.bought.Select(CaptureLine).ToList();
        Sold = transaction.sold.Select(CaptureLine).ToList();
    }
    private static Line CaptureLine(ShopTransaction.Item line) => new() {
        Item = RemoteCard.Create(line.thing, withData: true), Num = line.num, Price = line.price,
    };
}

internal sealed class ShopTrade : EClass
{
    private const string SaveKey = "emp_shop_trade";
    // An off-map item may only have an archived ledger snapshot after loading.
    // Keep its flag update until the actual item is available; never spawn the archive.
    [ElinGameIOProperty("shop_trade_item_flags")]
    private static Dictionary<int, bool> PendingFlags { get => field ??= []; set; }
    private static readonly HashSet<(int, Guid)> Completed = [];
    private static readonly HashSet<(int, Guid)> Closed = [];
    private sealed class ClientSession(ShopTransaction native)
    {
        public readonly Guid Id = Guid.NewGuid();
        public readonly ShopTransaction Native = native;
        public bool Busy, Closed;
    }
    private sealed record Pending(ElinNetClient Client, Chara Actor, ClientSession Session, InvOwner.Transaction Transaction, bool Drag, ShopTradeDelta Request);
    private static readonly Dictionary<ShopTransaction, ClientSession> Clients = [];
    private static readonly Dictionary<Guid, Pending> Requests = [];
    internal static InvOwner.Transaction? Replaying;
    internal static bool FinishingDrag;

    internal static bool Handles(ShopTransaction? shop) => shop?.trader is InvOwnerShop &&
        shop.trader.currency != CurrencyType.None && !shop.trader.UseHomeResource;

    internal static bool Submit(InvOwner.Transaction transaction, bool drag)
    {
        var native = ShopTransaction.current;
        if (!Handles(native) || transaction.FreeTrade || NetSession.Instance.Connection is not ElinNetClient client) return false;
        if (!Clients.TryGetValue(native, out var session)) Clients[native] = session = new(native);
        if (session.Busy || session.Closed) return true;
        InvOwner.Transaction.error = new() { card = transaction.thing };
        if (!transaction.IsValid()) {
            var error = InvOwner.Transaction.error;
            SE.Play(error.sound);
            if (!string.IsNullOrEmpty(error.lang)) Msg.Say(error.lang, error.card);
            return true;
        }
        var request = new ShopTradeDelta {
            SessionId = session.Id, RequestId = Guid.NewGuid(), Merchant = native.trader.owner,
            Item = transaction.thing, Quantity = transaction.num, Price = transaction.GetPrice(),
            ZoneUid = _zone.uid, Sell = transaction.sell, Currency = native.trader.currency,
            PriceType = native.trader.priceType, Drag = drag, Destination = transaction.destInv.Container,
            DestX = transaction.to?.invX ?? -1, DestY = transaction.to?.invY ?? -1,
        };
        session.Busy = true;
        Requests.Add(request.RequestId, new(client, pc, session, transaction, drag, request));
        client.Delta.AddRemote(request);
        return true;
    }

    internal static bool CloseClient(ShopTransaction native)
    {
        if (NetSession.Instance.Connection is not ElinNetClient || !Handles(native)) return false;
        if (!Clients.TryGetValue(native, out var session)) Clients[native] = session = new(native);
        session.Closed = true;
        if (!session.Busy) SendClose(session);
        if (ShopTransaction.current == native) ShopTransaction.current = null;
        return true;
    }
    private static void SendClose(ClientSession session)
    {
        NetSession.Instance.Connection?.Delta.AddRemote(new ShopTradeDelta { SessionId = session.Id, Close = true, Merchant = session.Native.trader.owner });
        Clients.Remove(session.Native);
    }

    internal static void Receive(ShopTradeReplyDelta reply)
    {
        if (!Requests.Remove(reply.RequestId, out var pending) || NetSession.Instance.Connection != pending.Client || pc != pending.Actor) return;
        var session = pending.Session;
        try {
            if (reply.Error is not null) { Msg.Say(reply.Error); return; }
            var item = reply.Item?.Find() as Thing;
            if (pending.Drag && item is { isDestroyed: false } && !session.Closed && ShopTransaction.current == session.Native && pending.Transaction.button) {
                pending.Transaction.thing = item;
                Replaying = pending.Transaction;
                // OnApply is remote-state landing: placement here must not emit
                // another intent. FreeTrade/IsValid patches skip already committed finance.
                pending.Transaction.Process(true);
            } else if (!pending.Drag && ui.currentDrag is DragItemCard drag && drag.from.thing == pending.Transaction.thing) {
                // UI.EndDrag normally executes OnDrag again. Acknowledge only
                // this already-paid drag, while keeping vanilla UI cleanup.
                FinishingDrag = true;
                try { ui.EndDrag(); } finally { FinishingDrag = false; }
            }
            SE.Play(pending.Request.Sell ? "sell" : "buy");
            if (item is not null) Msg.Say(pending.Request.Sell ? "sold" : "bought", item,
                Lang._currency(reply.Price, pending.Request.Currency.ToString().ToLowerInvariant()));
            if (reply.Ledger?.Decompress<ShopTradeLedger>() is { } saved) {
                var native = saved.Restore(session.Native.trader);
                session.Native.bought = native.bought;
                session.Native.sold = native.sold;
            }
            if (item is not null) LayerInventory.SetDirty(item);
        } finally {
            Replaying = null;
            session.Busy = false;
            if (session.Closed) SendClose(session);
        }
    }

    internal static ShopTradeLedger? Read(Chara actor) =>
        actor.GetStr(SaveKey) is { Length: > 0 } text ? JsonConvert.DeserializeObject<ShopTradeLedger>(text) : null;
    private static void Save(Chara actor, ShopTradeLedger? ledger) => actor.SetStr(SaveKey, ledger is null ? null : JsonConvert.SerializeObject(ledger));

    internal static void Resolve(ElinNetHost host, int peer, ShopTradeDelta request)
    {
        if (!host.ActiveRemoteCharas.TryGetValue(peer, out var actor) || request.SessionId == Guid.Empty) return;
        if (request.Close) {
            Closed.Add((peer, request.SessionId));
            if (Read(actor) is { } closing && closing.Id == request.SessionId) Settle(actor, closing);
            else if (request.Merchant?.Find() is { } emptyMerchant && actor.Dist(emptyMerchant) <= 3) {
                UpdateChestLock(emptyMerchant.uid);
                host.Delta.AddRemote(new ShopTradeFlagsDelta { MerchantUid = emptyMerchant.uid,
                    Deeds = player.flags.landDeedBought, Hammers = player.flags.garokkHammerBought });
            }
            return;
        }
        if (request.RequestId == Guid.Empty || !Completed.Add((peer, request.RequestId))) return;
        var reply = new ShopTradeReplyDelta { RequestId = request.RequestId, Error = "The trade could not be completed. Reopen the shop and try again." };
        void Reject() => host.SendDeltaTo(peer, reply);
        if (Closed.Contains((peer, request.SessionId)) || actor.isDead || !actor.IsInActiveMap || _zone.uid != request.ZoneUid ||
            request.Merchant?.Find() is not { isDestroyed: false } merchant ||
            (merchant is Chara npc ? npc.isDead || !npc.IsInActiveMap : merchant is not Thing machine || !_map.things.Contains(machine)) || actor.Dist(merchant) > 3 ||
            merchant.trait.CurrencyType != request.Currency || merchant.trait.PriceType != request.PriceType ||
            request.Currency is CurrencyType.None or CurrencyType.BranchMoney ||
            merchant.things.Find("chest_merchant") is not { } chest ||
            request.Item?.Find() is not Thing { isDestroyed: false } thing || request.Quantity <= 0 || request.Quantity > thing.Num) { Reject(); return; }
        var origin = thing.parent as Card ?? ThingRequest.GetDanglingOrigin(thing, peer);
        if (origin is null || (request.Sell ? origin.GetRootCard() != actor : origin != chest)) { Reject(); return; }
        var saved = Read(actor);
        if (saved is not null && saved.Id != request.SessionId) { Settle(actor, saved); saved = null; }
        saved ??= new() { Id = request.SessionId, MerchantUid = merchant.uid, ZoneUid = _zone.uid, Currency = request.Currency, PriceType = request.PriceType };
        if (saved.MerchantUid != merchant.uid || saved.ZoneUid != _zone.uid || saved.Currency != request.Currency || saved.PriceType != request.PriceType) { Reject(); return; }
        var trader = new InvOwnerShop(merchant, chest, request.Currency, request.PriceType);
        var ledger = saved.Restore(trader);
        int price;
        var previous = pc;
        try {
            game.player.chara = actor;
            price = ledger.GetPrice(thing, request.Quantity, request.Sell);
        } finally { game.player.chara = previous; }
        var destination = request.Sell ? chest : request.Destination?.Find();
        if (destination is null || (!request.Sell && destination.GetRootCard() != actor)) { Reject(); return; }
        var currency = request.Currency.ToString().ToLowerInvariant();
        if (price < 0 || price != request.Price || destination.things.IsFull(thing) ||
            (request.Sell && (thing.c_isImportant || (!ledger.CanSellBack(thing, request.Quantity) && (!trader.AllowSell || price == 0)))) ||
            (!request.Sell && actor.GetCurrency(currency) < price) ||
            (request.Sell && thing.things.Count > 0 && !ledger.HasBought(thing)) || (thing.isEquipped && thing.blessedState <= BlessedState.Cursed)) { Reject(); return; }
        using (ElinDelta.Simulate()) {
            if (thing.isEquipped) actor.body.Unequip(thing);
            var moved = thing.Split(request.Quantity);
            actor.ModCurrency(request.Sell ? price : -price, currency);
            previous = pc;
            try {
                game.player.chara = actor;
                if (request.Sell && !moved.IsIdentified && !ledger.HasBought(moved)) moved.Identify(false, IDTSource.SuperiorIdentify);
                else ledger.Process(moved, moved.Num, request.Sell);
            } finally { game.player.chara = previous; }
            // Capture before stacking can destroy the sold/purchased identity.
            saved.Capture(ledger);
            var received = !request.Sell && !request.Drag && request.DestX < 0 && destination == actor
                ? actor.Pick(moved, false)
                : destination.AddThing(moved, !request.Drag && game.UseGrid, request.DestX, request.DestY);
            moved.SetInt(102, destination.trait is TraitDeliveryChest ? _zone.uid : 0);
            reply.Item = received.isDestroyed ? null : RemoteCard.Create(received, withData: true);
            Save(actor, saved);
            reply.Ledger = LZ4Bytes.Create(saved);
            reply.Error = null;
            reply.Price = price;
        }
        host.SendDeltaTo(peer, reply);
        EmpLog.Information("Shop trade {RequestId}: actor {Actor}, merchant {Merchant}, sell {Sell}, quantity {Quantity}, price {Price}",
            request.RequestId, actor.uid, merchant.uid, request.Sell, request.Quantity, price);
    }

    internal static void Settle(Chara actor, ShopTradeLedger ledger)
    {
        // Save removal and rewards execute synchronously on the game thread.
        long xp = 0, credit = 0;
        var bought = new List<int>(); var sold = new List<int>();
        foreach (var line in ledger.Bought) {
            var item = line.Resolve();
            item.SetInt(101); item.isStolen = false; bought.Add(item.uid); PendingFlags[item.uid] = true;
            if (ledger.Currency != CurrencyType.Money) continue;
            xp += (long)(int)Mathf.Sqrt(Mathf.Abs(unchecked(line.Price * 5))) * line.Num;
            if (item.id == "deed") player.flags.landDeedBought += line.Num;
            if (item.id == "hammer_garokk") player.flags.garokkHammerBought += line.Num;
        }
        if (ledger.Currency == CurrencyType.Money) foreach (var line in ledger.Sold) {
            var item = line.Resolve();
            if (!item.isStolen) continue;
            credit += (long)((int)Mathf.Sqrt(line.Price) / 2) * line.Num;
            item.isStolen = false; sold.Add(item.uid); PendingFlags.TryAdd(item.uid, false);
        }
        Save(actor, null);
        ApplyPendingFlags();
        if (xp >= 10) OwnerProgressionAwards.Record(actor, ledger.Id, 291, (int)Math.Min(xp, int.MaxValue));
        if (credit > 0) Guild.Thief.AddContribution((int)Math.Min(credit, int.MaxValue));
        UpdateChestLock(ledger.MerchantUid);
        NetSession.Instance.Connection?.Delta.AddRemote(new ShopTradeFlagsDelta {
            Bought = bought.ToArray(), Sold = sold.ToArray(), MerchantUid = ledger.MerchantUid,
            Deeds = player.flags.landDeedBought, Hammers = player.flags.garokkHammerBought,
        });
        EmpLog.Information("Shop trade settled {SessionId}: actor {Actor}, negotiation XP {Xp}, thief contribution {Credit}", ledger.Id, actor.uid, xp, credit);
    }
    [ElinPostSceneInit]
    private static void OnScene(Scene.Mode mode) => ApplyPendingFlags();
    internal static void ApplyPendingFlags()
    {
        if (NetSession.Instance.Connection is ElinNetClient) return;
        foreach (var (uid, bought) in PendingFlags.ToArray()) {
            if (CardCache.Find(uid) is not { } item) continue;
            if (!item.isDestroyed) { item.isStolen = false; if (bought) item.SetInt(101); }
            PendingFlags.Remove(uid);
        }
    }
    internal static void UpdateChestLock(int merchantUid)
    {
        var open = ShopTransaction.current?.trader.owner.uid == merchantUid ||
            (NetSession.Instance.Connection is ElinNetHost host && host.ActiveRemoteCharas.Values.Any(c => Read(c)?.MerchantUid == merchantUid));
        if (CardCache.Find(merchantUid)?.things.Find("chest_merchant") is { } chest) chest.c_lockLv = open ? 0 : 1;
    }
    internal static void PublishHostClose(ShopTransaction native)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host || !Handles(native)) return;
        UpdateChestLock(native.trader.owner.uid);
        host.Delta.AddRemote(new ShopTradeFlagsDelta {
            MerchantUid = native.trader.owner.uid,
            Bought = native.bought.Select(i => i.thing.uid).ToArray(),
            Sold = native.trader.currency == CurrencyType.Money ? native.sold.Where(i => !i.thing.isStolen).Select(i => i.thing.uid).ToArray() : [],
            Deeds = player.flags.landDeedBought, Hammers = player.flags.garokkHammerBought,
        });
    }
    internal static void Release(Chara actor) { if (Read(actor) is { } ledger) Settle(actor, ledger); }
    internal static void Reset() { Completed.Clear(); Closed.Clear(); Clients.Clear(); Requests.Clear(); Replaying = null; FinishingDrag = false; }
}

[MessagePackObject]
public sealed class ShopTradeFlagsDelta : ElinDelta
{
    [Key(0)] public int[] Bought { get; set; } = [];
    [Key(1)] public int[] Sold { get; set; } = [];
    [Key(2)] public int MerchantUid { get; set; }
    [Key(3)] public int Deeds { get; set; }
    [Key(4)] public int Hammers { get; set; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net is not ElinNetClient) return;
        foreach (var uid in Bought) if (CardCache.Find(uid) is { } item) { item.SetInt(101); item.isStolen = false; }
        foreach (var uid in Sold) if (CardCache.Find(uid) is { } item) item.isStolen = false;
        // Keep an open client shop usable; vanilla close relocks its chest.
        if (ShopTransaction.current?.trader.owner.uid != MerchantUid && CardCache.Find(MerchantUid)?.things.Find("chest_merchant") is { } chest) chest.c_lockLv = 1;
        player.flags.landDeedBought = Deeds; player.flags.garokkHammerBought = Hammers;
    }
}
