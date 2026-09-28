using System;
using System.Linq;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class GuildStateSnapshot : EClass
{
    [MessagePackObject]
    public class Relation
    {
        [Key(0)] public string Id { get; set; } = "";
        [Key(1)] public FactionRelation.RelationType Type { get; init; }
        [Key(2)] public int Rank { get; init; }
        [Key(3)] public int Exp { get; init; }
        [Key(4)] public int Affinity { get; init; }
    }

    [Key(0)] public long Revision { get; init; }
    [Key(1)] public Relation[] Relations { get; set; } = [];
    [Key(2)] public LZ4Bytes[] Quests { get; set; } = [];
    [Key(3)] public string[] CompletedIds { get; set; } = [];
    [Key(4)] public string[] CompletedTypes { get; set; } = [];

    private static GuildStateSnapshot? _cached;
    private static bool _dirty = true;
    private static long _revision, _appliedRevision = -1;

    internal static bool IsGuildQuest(Quest q) => q is QuestGuild or QuestGuildOrder;
    internal static Guild[] All => [Guild.Fighter, Guild.Mage, Guild.Thief, Guild.Merchant];
    internal static Guild? Find(string id) => All.FirstOrDefault(g => g.id == id);
    internal static bool IsGuildId(string id) => id.StartsWith("guild_", StringComparison.Ordinal);

    [ElinPreLoad]
    private static void OnLoad(GameIOContext context) => Reset();

    internal static void Reset()
    {
        _cached = null;
        _dirty = true;
        _revision = 0;
        _appliedRevision = -1;
        GuildActionDelta.Reset();
    }

    internal static void MarkDirty()
    {
        if (NetSession.Instance.Connection is ElinNetHost) _dirty = true;
    }

    internal static GuildStateSnapshot Capture()
    {
        if (!_dirty && _cached is not null) return _cached;
        _cached = new() {
            Revision = ++_revision,
            Relations = All.Select(g => new Relation {
                Id = g.id, Type = g.relation.type, Rank = g.relation.rank,
                Exp = g.relation.exp, Affinity = g.relation.affinity,
            }).ToArray(),
            Quests = game.quests.list.Where(IsGuildQuest).Select(LZ4Bytes.Create).ToArray(),
            CompletedIds = game.quests.completedIDs.Where(IsGuildId).ToArray(),
            CompletedTypes = game.quests.completedTypes.Where(t => t.StartsWith("QuestGuild", StringComparison.Ordinal)).ToArray(),
        };
        _dirty = false;
        return _cached;
    }

    internal void Apply()
    {
        if (NetSession.Instance.Connection is not ElinNetClient || Revision < _appliedRevision) return;
        // Decode new quest state before changing live state. Repeated current
        // revisions still repair client-only relation writes, without decoding
        // quests or replacing task references held by the UI each tick.
        var quests = Revision > _appliedRevision
            ? Quests.Select(q => q.Decompress<Quest>()).Where(IsGuildQuest).ToArray()
            : null;
        foreach (var state in Relations) {
            if (Find(state.Id) is not { } guild) continue;
            guild.relation.type = state.Type;
            guild.relation.rank = state.Rank;
            guild.relation.exp = state.Exp;
            guild.relation.affinity = state.Affinity;
        }
        if (quests is null) return;
        game.quests.list.RemoveAll(q => IsGuildQuest(q) && !quests.Any(s => s.uid == q.uid));
        game.quests.globalList.RemoveAll(q => IsGuildQuest(q) && quests.Any(s => s.uid == q.uid));
        foreach (var quest in quests) QuestReplica.Apply(quest, false);
        game.quests.completedIDs.RemoveWhere(IsGuildId);
        foreach (var id in CompletedIds) game.quests.completedIDs.Add(id);
        game.quests.completedTypes.RemoveWhere(t => t.StartsWith("QuestGuild", StringComparison.Ordinal));
        foreach (var type in CompletedTypes) game.quests.completedTypes.Add(type);
        _appliedRevision = Revision;
        EmpLog.Debug("Applied shared guild state revision {Revision}, {QuestCount} quests", Revision, quests.Length);
    }
}
