using ElinTogether.Models;
using ElinTogether.Patches;
using Newtonsoft.Json;
public class EClass {
    public static Game game = new();
    public static Chara pc = new() { uid = 1 };
}
public class Game {
    public QuestManager quests = new();
    public Guild[] Guilds = [new() { id = "fighter" }, new() { id = "mage" }, new() { id = "thief" }, new() { id = "merchant" }];
}
public class QuestManager {
    public List<Quest> list = new(), globalList = new();
    public HashSet<string> completedIDs = new(), completedTypes = new();
    public Quest Start(string id, Chara owner, bool assignQuest) {
        var q = new QuestGuild { uid = list.Count + 100, id = id, person = new() { chara = owner } };
        list.Add(q); return q;
    }
    public void Start(Quest q) => list.Add(q);
}
public class Person { public Chara? chara; }
public class Quest {
    [JsonProperty] public int uid, phase;
    [JsonProperty] public string id = "";
    [JsonProperty] public bool track, isNew;
    [JsonProperty] public QuestTask? task;
    [JsonProperty] public Person person = new();
    public void SetClient(Chara c, bool assign) => person = new() { chara = c };
    public void OnModKarma(int a) { if (GuildQuestProgressPatch.Before(this) && task != null && a < 0) { task.num += a; GuildQuestProgressPatch.After(this); } }
    public void OnKillChara(Chara c) { }
    public void OnGiveItem(Chara c, Thing t) { }
    public void CompleteTask() { NextPhase(); task = null; }
    public void Complete() { }
    public void NextPhase() => ChangePhase(phase + 1);
    public void ChangePhase(int a) { phase = a; GuildStateSnapshot.MarkDirty(); }
    public Quest Clone() {
        var q = (Quest)MemberwiseClone();
        q.person = new() { chara = person.chara };
        q.task = task is null ? null : new() { num = task.num, owner = q };
        return q;
    }
}
public class QuestGuild : Quest { public const int Started = 0, CompletedTrial = 1, Joined = 10; }
public class QuestGuildOrder : Quest { }
public class QuestTask { public int num; public Quest owner = null!; public void SetOwner(Quest q) => owner = q; }
public class Faction {
    public string id = "";
    public string Name => id;
    public FactionRelation relation = new();
    public void AddContribution(int a) { if (relation.type == FactionRelation.RelationType.Member) relation.exp += a; GuildStateSnapshot.MarkDirty(); }
}
public class FactionRelation {
    public enum RelationType { Default, Owner, Member }
    public RelationType type;
    public int rank, exp, affinity;
    public int MaxRank => 6;
    public int ExpToNext => 100 + rank * rank * 100;
    public void Promote() { exp -= ExpToNext; rank++; }
}
public class Guild : Faction {
    public static Guild Fighter => EClass.game.Guilds[0];
    public static Guild Mage => EClass.game.Guilds[1];
    public static Guild Thief => EClass.game.Guilds[2];
    public static Guild Merchant => EClass.game.Guilds[3];
    public static Guild CurrentDrama => Thief;
    public QuestGuild? Quest => EClass.game.quests.list.OfType<QuestGuild>().FirstOrDefault(q => q.id == "guild_" + id);
    public bool IsMember => relation.type == FactionRelation.RelationType.Member;
    public bool IsCurrentZone = true;
    public int DevelopmentRefreshes;
    public void RefreshDevelopment() => DevelopmentRefreshes++;
}
public class Card { public string id = ""; public int uid, refVal; }
public class Chara : Card {
    public bool isDead, IsInActiveMap = true;
    public bool IsPC => this == EClass.pc;
    public Trait trait = new();
    public Container things = new();
    public int Distance = 1;
    public int Dist(Chara c) => Distance;
}
public class Container { public List<Thing> list = new(); public Thing? Find(string id) => list.Find(t => t.id == id && t.Num > 0); }
public class Thing : Card { public int Num = 1; public void ModNum(int a) => Num += a; }
public class Trait { public Card owner = new(); }
public class TraitGuildDoorman : Trait {
    public string GuildId = "thief";
    public int JoinCalls;
    public void GiveTrial() => EClass.game.quests.Start("guild_" + GuildId, (Chara)owner, false);
    public void OnJoinGuild() => JoinCalls++;
}
public class TraitBaseSpellbook : Trait {
    public enum Type { Ancient, Spell, Dojin }
    public Type BookType;
    public void OnRead(Chara c) { }
}
public class DramaOutcome { public Chara cc = new(); public void guild_trial() { } public void guild_join() { } public void guild_mageTrial() { } public void guild_promote() { } }
public class GameIOContext { }
public class ElinPreLoadAttribute : Attribute { }
public static class EmpLog { public static void Debug(string s, params object?[] a) { } public static void Information(string s, params object?[] a) { } }
namespace Newtonsoft.Json { public class JsonPropertyAttribute : Attribute { } }
namespace ElinTogether.Helper { public class Placeholder { } }
namespace MessagePack {
    public class MessagePackObjectAttribute : Attribute { }
    public class KeyAttribute(int key) : Attribute { public int Key = key; }
}
namespace HarmonyLib {
    public class HarmonyPatch : Attribute { public HarmonyPatch() { } public HarmonyPatch(Type t, string s) { } }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPostfix : Attribute { }
    public static class AccessTools { public static System.Reflection.MethodInfo Method(Type t, string s, Type[]? args = null) => args is null ? t.GetMethod(s)! : t.GetMethod(s, args)!; }
}
namespace ElinTogether.Net {
    public class ElinNetBase { public Queue Delta = new(); }
    public class Queue { public List<ElinDelta> Items = new(); public void AddRemote(ElinDelta d) => Items.Add(d); }
    public class ElinNetClient : ElinNetBase { }
    public class ElinNetHost : ElinNetBase {
        public Dictionary<int, Chara> ActiveRemoteCharas = new();
        public List<ElinDelta> Sent = new();
        public bool SendDeltaTo(int peer, ElinDelta d) { Sent.Add(d); return true; }
    }
    public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; }
}
namespace ElinTogether.Models {
    public class LZ4Bytes {
        public static int DecodeCount;
        private Quest value = null!;
        public static LZ4Bytes Create<T>(T value) => new() { value = ((Quest)(object)value!).Clone() };
        public T Decompress<T>() { DecodeCount++; return (T)(object)value.Clone(); }
    }
    public class RemoteCard(Card c) {
        public Card Find() => c;
        public static implicit operator RemoteCard(Card c) => new(c);
    }
    public abstract class ElinDelta : EClass {
        public int OriginPeer;
        public static bool IsApplying;
        protected abstract void OnApply(ElinTogether.Net.ElinNetBase net);
        public void Apply(ElinTogether.Net.ElinNetBase net) => OnApply(net);
        protected static IDisposable Simulate() => new Scope();
        private class Scope : IDisposable { public void Dispose() { } }
    }
    public class MsgSayDelta : ElinDelta { public string Text = ""; public float R,G,B,A; protected override void OnApply(ElinTogether.Net.ElinNetBase net) { } }
}
