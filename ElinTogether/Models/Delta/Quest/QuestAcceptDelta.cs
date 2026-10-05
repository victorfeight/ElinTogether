using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class QuestAcceptDelta : ElinDelta
{
    [Key(0)]
    public required int Uid { get; init; }

    [Key(1)]
    public required RemoteCard? Client { get; init; }

    [Key(2)] public int ZoneUid { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is not ElinNetHost host || SharedQuests.Actor(host, OriginPeer, ZoneUid) is not { } actor) {
            return;
        }

        var quest = ResolveQuest();
        if (quest is null) {
            SharedQuests.Reject(host, OriginPeer, Uid, "This quest is no longer available.");
            return;
        }

        if (game.quests.list.Exists(q => q.uid == Uid)) {
            return;
        }

        var previous = pc;
        using var _ = Simulate();
        using var karma = PersonalKarma.For(actor);
        using var messages = MsgRelayContext.RedirectTo(actor);
        try {
            player.chara = actor;
            if (!SharedQuests.CanClientAccept(quest) || quest.IsExpired || quest.chara == null ||
                !quest.IsVisibleOnQuestBoard()) {
                SharedQuests.Reject(host, OriginPeer, Uid, "This quest cannot be accepted here by a client."); return;
            }
            if (game.quests.CountRandomQuest() >= QuestManager.MaxRandomQuest) {
                SharedQuests.Reject(host, OriginPeer, Uid, "The shared quest list is full."); return;
            }
            game.quests.Start(quest);
        } finally {
            player.chara = previous;
        }
        EmpLog.Information("Accepted client quest {QuestUid} {QuestId} for peer {PeerIndex}, actor {Actor}", Uid, quest.id, OriginPeer, actor.uid);
    }

    private Quest? ResolveQuest()
    {
        if (Client?.Find() is Chara owner && owner.quest?.uid == Uid) {
            return owner.quest;
        }

        return game.quests.globalList.Find(q => q.uid == Uid);
    }
}
