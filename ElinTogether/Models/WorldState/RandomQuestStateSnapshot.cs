using System.Linq;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

// The host publishes saved state, never instructions to replay quest gameplay.
// Mutation hooks invalidate this cache; no additional timer or polling loop.
[MessagePackObject]
public class RandomQuestStateSnapshot : EClass
{
    [Key(0)] public long Revision { get; init; }
    [Key(1)] public LZ4Bytes[] Quests { get; set; } = [];
    [Key(2)] public string[] CompletedIds { get; set; } = [];
    [Key(3)] public string[] CompletedTypes { get; set; } = [];
    [Key(4)] public int[] AssignedQuestUids { get; set; } = [];

    private static RandomQuestStateSnapshot? _cached;
    private static bool _dirty = true;
    private static long _revision, _applied = -1;

    internal static bool Includes(Quest q) => q.IsRandomQuest && !GuildStateSnapshot.IsGuildQuest(q);
    [ElinPreLoad]
    private static void OnLoad(GameIOContext context) => Reset();
    internal static void Reset() { _cached = null; _dirty = true; _revision = 0; _applied = -1; }
    internal static void MarkDirty()
    {
        if (NetSession.Instance.Connection is ElinNetHost) _dirty = true;
    }
    internal static RandomQuestStateSnapshot Capture()
    {
        if (!_dirty && _cached is not null) return _cached;
        _cached = new() {
            Revision = ++_revision,
            Quests = game.quests.list.Where(Includes).Select(LZ4Bytes.Create).ToArray(),
            AssignedQuestUids = game.quests.list.Where(q => Includes(q) && q.person?.chara?.quest == q).Select(q => q.uid).ToArray(),
            CompletedIds = game.quests.completedIDs.ToArray(),
            CompletedTypes = game.quests.completedTypes.ToArray(),
        };
        _dirty = false;
        return _cached;
    }
    internal void Apply()
    {
        if (NetSession.Instance.Connection is not ElinNetClient || Revision <= _applied) return;
        var incoming = Quests.Select(q => q.Decompress<Quest>()).Where(Includes).ToArray();
        var active = incoming.Select(q => q.uid).ToHashSet();
        // Failure/removal is bookkeeping on replicas: no fame, karma, release,
        // confiscation or rewards are executed a second time here.
        foreach (var removed in game.quests.list.Where(q => Includes(q) && !active.Contains(q.uid)).ToArray()) {
            game.quests.list.Remove(removed);
            if (removed.chara?.quest?.uid == removed.uid) removed.chara.quest = null;
            removed.ClientZone?.completedQuests.Add(removed.uid);
        }
        game.quests.globalList.RemoveAll(q => Includes(q) && active.Contains(q.uid));
        foreach (var quest in incoming) QuestReplica.Apply(quest, AssignedQuestUids.Contains(quest.uid));
        game.quests.completedIDs.UnionWith(CompletedIds);
        game.quests.completedTypes.UnionWith(CompletedTypes);
        _applied = Revision;
        EmpLog.Debug("Applied shared random quest revision {Revision}, {Count} active quests", Revision, incoming.Length);
    }
}
