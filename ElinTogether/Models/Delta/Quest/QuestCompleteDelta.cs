using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class QuestCompleteDelta : ElinDelta
{
    [Key(1)]
    public required int Uid { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        // A result is not authority for a client to complete a quest. Consuming
        // turn-ins are validated and executed through QuestDeliverDelta instead.
        if (net.IsHost) return;
        var quest = game.quests.list.Find(q => q.uid == Uid) ??
                    game.quests.globalList.Find(q => q.uid == Uid);
        if (quest is null || quest.isComplete) {
            return;
        }

        // Guild completion is part of its authoritative state, not an instruction
        // from a client or a second gameplay replay on receipt.
        if (GuildStateSnapshot.IsGuildQuest(quest)) return;

        SharedQuests.ApplyCompletion(quest);
    }
}
