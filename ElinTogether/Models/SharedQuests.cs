using ElinTogether.Net;
using ElinTogether.Helper;
using ElinTogether.Patches;

namespace ElinTogether.Models;

internal sealed class SharedQuests : EClass
{
    // Keep the UI, Start hook and authoritative request on the same supported boundary.
    internal static bool CanClientAccept(Quest q) =>
        q is { uid: >= 0, IsRandomQuest: true, UseInstanceZone: false } && q.source.drama.IsEmpty();

    internal static Chara? Actor(ElinNetHost host, int peer, int zone) =>
        zone == _zone.uid && host.AcceptsPlayerInput(peer) &&
        host.ActiveRemoteCharas.TryGetValue(peer, out var actor) &&
        actor.IsInActiveMap && !actor.isDead && !actor.IsDisabled ? actor : null;

    internal static void Reject(ElinNetHost host, int peer, int uid, string reason)
    {
        EmpLog.Warning("Quest request rejected: peer {Peer}, quest {Quest}, reason {Reason}", peer, uid, reason);
        host.SendDeltaTo(peer, new MsgSayDelta { Text = reason, R = 1, G = 1, B = 1, A = 1 });
    }

    internal static void Deliver(ElinNetHost host, QuestDeliverDelta request)
    {
        var actor = Actor(host, request.OriginPeer, request.ZoneUid);
        if (actor == null) return;
        // Only an accepted, unfinished quest can consume goods. Removing it on
        // completion also makes repeated requests harmless without another ledger.
        if (game.quests.list.Find(q => q.uid == request.Uid) is not QuestDeliver quest ||
            !CanClientAccept(quest) || quest.IsExpired ||
            request.Recipient.Find() is not Chara { IsAliveInCurrentZone: true } recipient) {
            Reject(host, request.OriginPeer, request.Uid, "This delivery is no longer available."); return;
        }
        var previous = pc;
        using var simulation = ElinDelta.Simulate();
        using var karma = PersonalKarma.For(actor);
        using var messages = MsgRelayContext.RedirectTo(actor);
        try {
            player.chara = actor;
            // ListDestThing includes accessible inventory and eligible settlement
            // stock for supply quests; don't substitute the host's chosen item.
            if (request.Item.Find() is not Thing { isDestroyed: false } item ||
                quest.num <= 0 || item.Num < quest.num || !quest.CanDeliverToClient(recipient) ||
                !quest.ListDestThing().Contains(item)) {
                Reject(host, request.OriginPeer, request.Uid, "The selected item or quest recipient is no longer eligible."); return;
            }
            var itemUid = item.uid;
            var quantity = quest.num;
            var delivered = quest.Deliver(recipient, item);
            EmpLog.Information("Quest delivery resolved: peer {Peer}, actor {Actor}, quest {Quest}, recipient {Recipient}, item {Item}, quantity {Quantity}, delivered {Delivered}, complete {Complete}",
                request.OriginPeer, actor.uid, quest.uid, recipient.uid, itemUid, quantity, delivered, quest.isComplete);
        } finally {
            player.chara = previous;
        }
    }

    // A confirmed host result updates bookkeeping only. Never replay rewards,
    // item consumption, karma or quest-specific OnComplete on the client.
    internal static void ApplyCompletion(Quest quest)
    {
        if (quest.isComplete) return;
        game.quests.Remove(quest);
        game.quests.completedIDs.Add(quest.id);
        game.quests.completedTypes.Add(quest.GetType().ToString());
        quest.ShowCompleteText();
        if (quest.chara?.quest?.uid == quest.uid) quest.chara.quest = null;
        quest.ClientZone?.completedQuests.Add(quest.uid);
        quest.isComplete = true;
    }
}
