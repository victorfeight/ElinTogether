using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace ElinTogether.Models;

internal static class QuestReplica
{
    // Keep existing quest references held by dialogue/tracker UI. Copy saved state
    // without running ChangePhase/CompleteTask, which have gameplay side effects.
    internal static Quest Apply(Quest incoming, bool assignQuest)
    {
        var index = EClass.game.quests.list.FindIndex(q => q.uid == incoming.uid);
        var quest = index < 0 ? incoming : EClass.game.quests.list[index];
        if (quest.GetType() != incoming.GetType()) quest = incoming;
        if (quest != incoming) {
            foreach (var field in incoming.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                         .Where(f => f.IsDefined(typeof(JsonPropertyAttribute)) && f.Name is not ("track" or "isNew"))) {
                field.SetValue(quest, field.GetValue(incoming));
            }
        }
        quest.task?.SetOwner(quest);
        if (index < 0) EClass.game.quests.list.Add(quest);
        else EClass.game.quests.list[index] = quest;
        if (quest.person?.chara is { } owner) quest.SetClient(owner, assignQuest);
        return quest;
    }
}
