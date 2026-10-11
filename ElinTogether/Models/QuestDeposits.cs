using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class QuestDepositDelta : ElinDelta
{
    [Key(0)] public Guid RequestId { get; set; }
    [Key(1)] public int ZoneUid { get; set; }
    [Key(2)] public int QuestUid { get; set; }
    [Key(3)] public RemoteCard? Chest { get; set; }
    [Key(4)] public RemoteCard[] Items { get; set; } = [];
    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host) QuestDeposits.Resolve(host, this);
    }
}

internal sealed class QuestDeposits : EClass
{
    private static readonly HashSet<(int, Guid)> Completed = [];
    [ElinPreLoad] private static void OnLoad(GameIOContext context) => Reset();
    internal static void Reset() => Completed.Clear();
    internal static void Submit(InvOwnerDeliver owner, IEnumerable<Thing> items)
    {
        if (game.quests.Get<QuestHarvest>() is not { } quest) return;
        var selected = items.ToArray();
        if (NetSession.Instance.Connection is ElinNetClient client) {
            var refs = selected.Select(PendingSplit.Split).Where(t => t != null && !PendingUid.IsPending(t.Uid)).Cast<RemoteCard>().ToArray();
            if (refs.Length > 0) client.Delta.AddRemote(new QuestDepositDelta {
                RequestId = Guid.NewGuid(), ZoneUid = _zone.uid, QuestUid = quest.uid, Chest = owner.owner, Items = refs,
            });
        } else {
            foreach (var item in selected) if (!item.isDestroyed && owner.ShouldShowGuide(item)) owner._OnProcess(item);
        }
    }
    internal static void Resolve(ElinNetHost host, QuestDepositDelta request)
    {
        if (request.RequestId == Guid.Empty || !Completed.Add((request.OriginPeer, request.RequestId))) return;
        var actor = SharedQuests.Actor(host, request.OriginPeer, request.ZoneUid);
        if (actor == null || request.Chest?.Find() is not Thing { trait: TraitFarmChest, isDestroyed: false } chest ||
            !_map.things.Contains(chest) || actor.Dist(chest) > 3 ||
            game.quests.Get<QuestHarvest>() is not { } quest || quest.uid != request.QuestUid ||
            !_zone.events.list.OfType<ZoneEventQuest>().Any(e => e.uidQuest == quest.uid)) return;
        var owner = new InvOwnerDeliver(chest) { mode = InvOwnerDeliver.Mode.Crop };
        var previous = pc;
        using var simulation = ElinDelta.Simulate();
        using var messages = MsgRelayContext.RedirectTo(actor);
        try {
            player.chara = actor;
            var count = 0;
            foreach (var remote in request.Items.GroupBy(i => i.Uid).Select(g => g.First())) {
                if (remote.Find() is not Thing { isDestroyed: false } item || item.GetRootCard() != actor ||
                    !actor.things.List(t => t == item, onlyAccessible: true).Contains(item) ||
                    remote.Num <= 0 || remote.Num > item.Num || !owner.ShouldShowGuide(item)) continue;
                owner._OnProcess(item.Split(remote.Num));
                count++;
            }
            EmpLog.Information("Quest deposit {Request}: peer {Peer}, quest {Quest}, accepted {Count}/{Requested}, weight {Weight}",
                request.RequestId, request.OriginPeer, quest.uid, count, request.Items.Length, quest.weightDelivered);
        } finally { player.chara = previous; }
    }
}
