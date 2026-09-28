using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class QuestUpdateDelta : ElinDelta
{
    [Key(0)]
    public required LZ4Bytes Data { get; init; }

    [Key(1)]
    public required bool AssignQuest { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (NetSession.Instance.IsHost) {
            return;
        }

        var quest = Data.Decompress<Quest>();
        if (GuildStateSnapshot.IsGuildQuest(quest)) return;

        QuestReplica.Apply(quest, AssignQuest);
    }

    public static QuestUpdateDelta Create(Quest quest)
    {
        return new() {
            Data = LZ4Bytes.Create(quest),
            AssignQuest = quest.person.chara?.quest == quest,
        };
    }
}
