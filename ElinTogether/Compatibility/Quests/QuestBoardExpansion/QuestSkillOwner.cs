using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Net;
using ElinTogether.Patches;

namespace ElinTogether.Models;

// Only QuestPack's timed skill challenges need a persistent participant. Ordinary
// quests keep shared progress. Native saved world flags survive offline character
// switching too; these namespaced entries are never personal profile fields.
internal sealed class QuestSkillOwner : EClass, IDisposable
{
    private const string Prefix = "emp.quest.skillOwner.";
    private static readonly Type? TimedType = QuestExpansionCompat.BoardAssembly?.GetType("QuestPack.Quests.QuestTimedSkill");
    internal static bool Applies(Quest quest) => TimedType?.IsInstanceOfType(quest) == true;
    private static string Key(Quest quest) => Prefix + quest.uid;
    internal static void Bind(Quest quest)
    {
        if (NetSession.Instance.Connection is ElinNetClient || !Applies(quest)) return;
        var active = game.quests.list.Where(Applies).Select(Key).ToHashSet();
        foreach (var key in player.dialogFlags.Keys.Where(k => k.StartsWith(Prefix, StringComparison.Ordinal) && !active.Contains(k)).ToArray())
            player.dialogFlags.Remove(key);
        player.dialogFlags[Key(quest)] = pc.uid;
        RandomQuestStateSnapshot.MarkDirty();
        EmpLog.Information("Bound skill quest {Quest} to actor {Actor}", quest.uid, pc.uid);
    }
    internal static Chara? Actor(Quest quest)
    {
        if (!player.dialogFlags.TryGetValue(Key(quest), out var uid)) return pc;
        return pc.uid == uid ? pc : game.cards.Find(uid);
    }
    internal static Dictionary<int, int> Capture() => game.quests.list.Where(Applies)
        .Where(q => player.dialogFlags.ContainsKey(Key(q)))
        .ToDictionary(q => q.uid, q => player.dialogFlags[Key(q)]);
    internal static void Apply(Dictionary<int, int> owners)
    {
        foreach (var key in player.dialogFlags.Keys.Where(k => k.StartsWith(Prefix, StringComparison.Ordinal)).ToArray())
            player.dialogFlags.Remove(key);
        foreach (var pair in owners) player.dialogFlags[Prefix + pair.Key] = pair.Value;
    }
    // Wrap native Complete/Fail, not just OnComplete: reward drops, affinity,
    // personal karma, XP, and quest-item cleanup must agree on the participant.
    internal static bool Enter(Quest quest, out QuestSkillOwner? scope)
    {
        scope = null;
        if (!Applies(quest)) return true;
        if (NetSession.Instance.Connection is ElinNetClient) return false;
        var actor = Actor(quest);
        if (actor == null) return false; // Never substitute the host for an unavailable saved participant.
        scope = new QuestSkillOwner(actor);
        return true;
    }
    private readonly Chara _previous;
    private readonly IDisposable _karma, _messages;
    private QuestSkillOwner(Chara actor)
    {
        _previous = pc;
        _karma = PersonalKarma.For(actor);
        _messages = MsgRelayContext.RedirectTo(actor);
        player.chara = actor;
    }
    public void Dispose()
    {
        player.chara = _previous;
        _messages.Dispose();
        _karma.Dispose();
    }
}
