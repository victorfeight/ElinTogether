using System;
using System.Collections.Generic;
using ElinTogether.Helper;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

public enum InvestmentKind { Shop, Town }

[MessagePackObject]
public class InvestmentDelta : ElinDelta
{
    [Key(0)] public Guid RequestId { get; set; }
    [Key(1)] public RemoteCard? Shop { get; set; }
    [Key(2)] public int ZoneUid { get; set; }
    [Key(3)] public int ExpectedLevel { get; set; }
    [Key(4)] public int QuotedPrice { get; set; }
    [Key(5)] public InvestmentKind Kind { get; set; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host) Investment.Resolve(host, OriginPeer, this);
    }
}

[MessagePackObject]
public class InvestmentResultDelta : ElinDelta
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
    [Key(9)] public InvestmentKind Kind { get; set; }
    [Key(10)] public int Development { get; set; }
    [Key(11)] public int TownInvestment { get; set; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetClient) Investment.Receive(this);
    }
}

internal sealed class Investment : EClass
{
    private Investment() { }
    private static readonly HashSet<(int Peer, Guid Id)> Completed = [];
    private sealed record Pending(ElinNetClient Client, Chara Actor, Action<InvestmentResultDelta> Reply);
    private static readonly Dictionary<Guid, Pending> Requests = [];

    [ElinPreLoad]
    private static void OnLoad(GameIOContext context) => Reset();

    internal static void Reset() { Completed.Clear(); Requests.Clear(); OwnerProgressionAwards.Reset(); }

    internal static void Submit(Chara shop, int expectedLevel, int price, Action<InvestmentResultDelta> reply, InvestmentKind kind = InvestmentKind.Shop)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client) return;
        var id = Guid.NewGuid();
        Requests.Add(id, new(client, pc, reply));
        client.Delta.AddRemote(new InvestmentDelta {
            RequestId = id, Shop = shop, ZoneUid = _zone.uid,
            ExpectedLevel = expectedLevel, QuotedPrice = price, Kind = kind,
        });
    }

    internal static int Price(Chara actor, Chara shop, InvestmentKind kind = InvestmentKind.Shop)
    {
        // InvestShop's actor argument does not reach CalcMoney.Invest, which
        // reads pc for CHA/Investing. This scope contains only the price query.
        var previous = pc;
        try {
            game.player.chara = actor;
            return kind == InvestmentKind.Town ? CalcMoney.InvestZone(actor) : CalcMoney.InvestShop(actor, shop);
        } finally { game.player.chara = previous; }
    }

    internal static void Resolve(ElinNetHost host, int peer, InvestmentDelta request)
    {
        if (request.RequestId == Guid.Empty || !host.ActiveRemoteCharas.TryGetValue(peer, out var actor) ||
            !Completed.Add((peer, request.RequestId))) return;
        var shop = request.Shop?.Find() as Chara;
        var result = new InvestmentResultDelta {
            RequestId = request.RequestId, Shop = request.Shop, ZoneUid = request.ZoneUid, Kind = request.Kind,
            Message = "The investment could not be completed. Talk to the shopkeeper again.",
        };
        if (actor.isDead || !actor.IsInActiveMap || _zone.uid != request.ZoneUid || !_zone.AllowInvest ||
            shop is null || shop.isDead || !shop.IsInActiveMap || actor.Dist(shop) > 3 ||
            (request.Kind switch {
                InvestmentKind.Shop => !shop.trait.CanInvest,
                InvestmentKind.Town => !shop.trait.CanInvestTown || Guild.GetCurrentGuild() is not null,
                _ => true,
            })) {
            host.SendDeltaTo(peer, result);
            return;
        }
        CaptureResult(result, shop);
        var level = request.Kind == InvestmentKind.Town ? _zone.development : shop.c_invest;
        var price = Price(actor, shop, request.Kind);
        var maxLevel = request.Kind == InvestmentKind.Town ? (int.MaxValue - 100) / 2 - 9 : (int.MaxValue - 50) / 20;
        if (level != request.ExpectedLevel || price != request.QuotedPrice || price < 0 ||
            level < 0 || level >= maxLevel) {
            result.Message = "The investment price changed. Please review the new quote.";
        } else if (actor.GetCurrency() < price) {
            result.Message = "You do not have enough money to invest.";
        } else {
            using var simulation = ElinDelta.Simulate();
            actor.ModCurrency(-price);
            int experience;
            if (request.Kind == InvestmentKind.Town) {
                // Preserve vanilla cumulative int arithmetic; the dialogue displays
                // a negative overflow value as int.MaxValue.
                _zone.investment = unchecked(_zone.investment + price);
                _zone.ModDevelopment(5 + rnd(5));
                _zone.ModInfluence(2);
                experience = 100 + _zone.development * 2;
            } else {
                shop.c_invest++;
                _zone.ModInfluence(1);
                experience = 50 + shop.c_invest * 20;
                Guild.Merchant.AddContribution(5 + shop.c_invest);
            }
            result.Award = OwnerProgressionAwards.Record(actor, request.RequestId, 292, experience);
            result.Accepted = true;
            CaptureResult(result, shop);
            result.Message = request.Kind == InvestmentKind.Town ? "Town investment completed." : "Shop investment completed.";
        }
        host.SendDeltaTo(peer, result);
        EmpLog.Information("Investment {RequestId}: {Kind}, peer {Peer}, NPC {ShopUid}, accepted {Accepted}, price {Price}, level {Level}, development {Development}",
            request.RequestId, request.Kind, peer, shop.uid, result.Accepted, price, result.Level, result.Development);
    }

    private static void CaptureResult(InvestmentResultDelta result, Chara shop)
    {
        result.HasState = true;
        result.Level = shop.c_invest;
        result.Influence = _zone.influence;
        result.Development = _zone.development;
        result.TownInvestment = _zone.investment;
    }

    internal static void Receive(InvestmentResultDelta result)
    {
        if (!Requests.Remove(result.RequestId, out var pending) ||
            NetSession.Instance.Connection != pending.Client || pc != pending.Actor) return;
        // Snapshot synchronization also sends these fields to observers and late
        // joiners. Apply the receipt before reopening a quick-invest quote.
        if (result.HasState && _zone.uid == result.ZoneUid) {
            if (result.Shop?.Find() is Chara shop && shop.IsInActiveMap) shop.c_invest = result.Level;
            _zone.influence = result.Influence;
            _zone.development = result.Development;
            _zone.investment = result.TownInvestment;
        }
        if (result.Accepted) {
            if (result.Award is { } award) OwnerProgressionAwards.Receive([award]);
            SE.Pay();
        }
        Msg.Say(result.Message);
        pending.Reply(result);
    }
}
