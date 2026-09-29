using System;
using System.Collections.Generic;
using ElinTogether.Helper;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class ShopInvestmentDelta : ElinDelta
{
    [Key(0)] public Guid RequestId { get; set; }
    [Key(1)] public RemoteCard? Shop { get; set; }
    [Key(2)] public int ZoneUid { get; set; }
    [Key(3)] public int ExpectedLevel { get; set; }
    [Key(4)] public int QuotedPrice { get; set; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host) ShopInvestment.Resolve(host, OriginPeer, this);
    }
}

[MessagePackObject]
public class ShopInvestmentResultDelta : ElinDelta
{
    [Key(0)] public Guid RequestId { get; set; }
    [Key(1)] public RemoteCard? Shop { get; set; }
    [Key(2)] public int ZoneUid { get; set; }
    [Key(3)] public int Level { get; set; }
    [Key(4)] public int Influence { get; set; }
    [Key(5)] public OwnerProgressionAward? Award { get; set; }
    [Key(6)] public string Message { get; set; } = "";
    [Key(7)] public bool Accepted { get; set; }
    [Key(8)] public bool HasState { get; set; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetClient) ShopInvestment.Receive(this);
    }
}

internal sealed class ShopInvestment : EClass
{
    private ShopInvestment() { }
    private static readonly HashSet<(int Peer, Guid Id)> Completed = [];
    private sealed record Pending(ElinNetClient Client, Chara Actor, Action<ShopInvestmentResultDelta> Reply);
    private static readonly Dictionary<Guid, Pending> Requests = [];

    [ElinPreLoad]
    private static void OnLoad(GameIOContext context) => Reset();

    internal static void Reset() { Completed.Clear(); Requests.Clear(); OwnerProgressionAwards.Reset(); }

    internal static void Submit(Chara shop, int expectedLevel, int price, Action<ShopInvestmentResultDelta> reply)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client) return;
        var id = Guid.NewGuid();
        Requests.Add(id, new(client, pc, reply));
        client.Delta.AddRemote(new ShopInvestmentDelta {
            RequestId = id, Shop = shop, ZoneUid = _zone.uid,
            ExpectedLevel = expectedLevel, QuotedPrice = price,
        });
    }

    internal static int Price(Chara actor, Chara shop)
    {
        // InvestShop's actor argument does not reach CalcMoney.Invest, which
        // reads pc for CHA/Investing. This scope contains only the price query.
        var previous = pc;
        try {
            game.player.chara = actor;
            return CalcMoney.InvestShop(actor, shop);
        } finally { game.player.chara = previous; }
    }

    internal static void Resolve(ElinNetHost host, int peer, ShopInvestmentDelta request)
    {
        if (request.RequestId == Guid.Empty || !host.ActiveRemoteCharas.TryGetValue(peer, out var actor) ||
            !Completed.Add((peer, request.RequestId))) return;
        var shop = request.Shop?.Find() as Chara;
        var result = new ShopInvestmentResultDelta {
            RequestId = request.RequestId, Shop = request.Shop, ZoneUid = request.ZoneUid,
            Message = "The investment could not be completed. Talk to the shopkeeper again.",
        };
        if (actor.isDead || !actor.IsInActiveMap || _zone.uid != request.ZoneUid ||
            shop is null || shop.isDead || !shop.IsInActiveMap || actor.Dist(shop) > 3 || !shop.trait.CanInvest) {
            host.SendDeltaTo(peer, result);
            return;
        }
        result.Level = shop.c_invest;
        result.Influence = _zone.influence;
        result.HasState = true;
        var price = Price(actor, shop);
        if (shop.c_invest != request.ExpectedLevel || price != request.QuotedPrice || price < 0 ||
            shop.c_invest < 0 || shop.c_invest >= (int.MaxValue - 50) / 20) {
            result.Message = "The investment price changed. Please review the new quote.";
        } else if (actor.GetCurrency() < price) {
            result.Message = "You do not have enough money to invest.";
        } else {
            using var simulation = ElinDelta.Simulate();
            actor.ModCurrency(-price);
            shop.c_invest++;
            _zone.ModInfluence(1);
            result.Award = OwnerProgressionAwards.Record(actor, request.RequestId, 292, 50 + shop.c_invest * 20);
            Guild.Merchant.AddContribution(5 + shop.c_invest);
            result.Accepted = true;
            result.Level = shop.c_invest;
            result.Influence = _zone.influence;
            result.Message = "Shop investment completed.";
        }
        host.SendDeltaTo(peer, result);
        EmpLog.Information("Shop investment {RequestId}: peer {Peer}, shop {ShopUid}, accepted {Accepted}, price {Price}, level {Level}",
            request.RequestId, peer, shop.uid, result.Accepted, price, result.Level);
    }

    internal static void Receive(ShopInvestmentResultDelta result)
    {
        if (!Requests.Remove(result.RequestId, out var pending) ||
            NetSession.Instance.Connection != pending.Client || pc != pending.Actor) return;
        // Snapshot synchronization also sends these fields to observers and late
        // joiners. Apply the receipt before reopening a quick-invest quote.
        if (result.HasState && _zone.uid == result.ZoneUid && result.Shop?.Find() is Chara shop && shop.IsInActiveMap) {
            shop.c_invest = result.Level;
            _zone.influence = result.Influence;
        }
        if (result.Accepted) {
            if (result.Award is { } award) OwnerProgressionAwards.Receive([award]);
            SE.Pay();
        }
        Msg.Say(result.Message);
        pending.Reply(result);
    }
}
