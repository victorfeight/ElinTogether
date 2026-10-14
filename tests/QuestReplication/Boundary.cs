using ElinTogether.Models;
using Newtonsoft.Json;
public class EClass {
    public static Game game = new(); public static Player player = new(); public static Chara pc => player.chara;
    public static Zone _zone = new(); public static Map _map = new();
}
public class Player { public Chara chara = new(); public object? returnInfo; public Dictionary<string,int> dialogFlags = []; }
public class Game { public QuestManager quests = new(); public Cards cards = new(); }
public class Cards : Dictionary<int,Chara> { public Chara? Find(int uid) => this.GetValueOrDefault(uid); }
public class Zone { public int uid = 10; public Events events = new(); public HashSet<int> completedQuests = []; public void ModInfluence(int n) { } }
public class Map { public List<Thing> things = []; }
public class Events { public List<ZoneEvent> list = []; }
public class ZoneEvent { public virtual void OnVisit() { } public virtual void OnLeaveZone() { } public virtual void OnCharaDie(Chara c) { } public void Tick(float dt) { } }
public class ZoneEventQuest : ZoneEvent { public int uidQuest; public void _OnTickRound() { } }
public class ZonePreEnterEvent { public virtual void Execute() { } }
public class ZonePreEnterOnCompleteQuestInstance : ZonePreEnterEvent { public override void Execute() { } }
public class QuestManager {
    public const int MaxRandomQuest = 5;
    public List<Quest> list = [], globalList = []; public HashSet<string> completedIDs = [], completedTypes = [];
    public T? Get<T>() where T:Quest => list.OfType<T>().FirstOrDefault();
    public Quest Start(Quest q) { list.Add(q); return q; }
    public void Remove(Quest q) => list.Remove(q); public void RemoveGlobal(Quest q) => globalList.Remove(q);
}
public class Person { public Chara? chara; }
public class Quest {
    [JsonProperty] public int uid;
    [JsonProperty] public int counter;
    [JsonProperty] public bool track, isNew;
    [JsonProperty] public QuestTask? task;
    public bool IsRandomQuest = true, Guild; public bool CanAbandon = true, UseInstanceZone; public int Failures; public string GetTitle() => "quest";
    public Person person = new(); public Chara? chara => person.chara; public Zone? ClientZone;
    public void SetClient(Chara c, bool assign) { person.chara = c; if(assign) c.quest = this; }
    public void Start() { } public void Complete() { } public void Fail() { Failures++; EClass.game.quests.Remove(this); } public void CompleteTask() { } public virtual void OnEnterZone() { } public virtual void OnStart() { }
}
public class QuestHarvest : Quest { public int weightDelivered; }
public class QuestDeliver : Quest {
    public bool Throw;
    public List<Thing> ListDestThing(bool onlyFirst = false) {
        if (Throw && EClass.pc.uid == 2) throw new InvalidOperationException();
        return EClass.pc.things.Where(t => t.Eligible).ToList();
    }
}
public class QuestTask { public Quest? owner; public void SetOwner(Quest q) => owner = q; public virtual void OnKillChara(Chara c) { } }
public class Inventory : List<Thing> { public List<Thing> List(Func<Thing,bool> f, bool onlyAccessible=false) => this.Where(t => (!onlyAccessible || t.Accessible) && f(t)).ToList(); }
public class Card { public int uid; }
public class Chara : Card {
    public bool isDead, IsInActiveMap = true, IsDisabled; public AIAct ai = new(); public Quest? quest;
    public Inventory things = []; public int Distance = 1; public int Dist(Card c) => Distance;
}
public class Thing : Card {
    public bool isDestroyed, Eligible = true, Accessible = true; public int Num = 3, SelfWeight = 10;
    public object trait = new(); public Chara? Root;
    public Card? GetRootCard() => Root;
    public Thing Split(int n) { if(n == Num) return this; Num -= n; return new Thing { uid=uid+1000, Num=n, Root=Root }; }
}
public class TraitFarmChest { }
public class InvOwnerDraglet { }
public class Button { public Action? Action; public void SetOnClick(Action a) => Action=a; }
public class LayerDragGrid { public Button buttonDeliver=new(); public bool Closed; public void Close() => Closed=true; public void SetInv(InvOwnerDraglet owner) { } }
public static class SE { public static void BeepSmall() { } }
public class InvOwnerDeliver(Card card) : InvOwnerDraglet {
    public Card owner = card; public enum Mode { Crop } public Mode mode;
    public bool ShouldShowGuide(Thing t) => t.Eligible;
    public void _OnProcess(Thing t) { EClass.game.quests.Get<QuestHarvest>()!.weightDelivered += t.Num*t.SelfWeight; t.isDestroyed = true; RandomQuestStateSnapshot.MarkDirty(); }
}
public class AIAct { public AIAct? child; public bool IsRunning = true; }
public class AI_PlayMusic : AIAct { }
public class GameIOContext { }
public class ElinPreLoadAttribute : Attribute { }
public static class EmpLog { public static void Debug(string s, params object?[] args) { } public static void Information(string s, params object?[] args) { } }
namespace MessagePack { public class MessagePackObjectAttribute : Attribute { } public class KeyAttribute(int n) : Attribute { public int N = n; } }
namespace ElinTogether.Helper { public class Marker { } }
namespace ElinTogether.Net {
    public class ElinNetBase { public Queue Delta = new(); }
    public class Queue { public List<ElinDelta> Items = []; public void AddRemote(ElinDelta d) => Items.Add(d); }
    public class ElinNetHost : ElinNetBase { public Dictionary<int,Chara> ActiveRemoteCharas = []; public bool Accept = true; }
    public class ElinNetClient : ElinNetBase { }
    public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; }
}
namespace ElinTogether.Models {
    public class Scope : IDisposable { public void Dispose() { } }
    public abstract class ElinDelta : EClass { public int OriginPeer = 1; public static bool IsApplying; public static bool IsRemoteStateLanding => IsApplying && !ThingRequest.IsReplayingIntent; protected abstract void OnApply(ElinTogether.Net.ElinNetBase net); public void Apply(ElinTogether.Net.ElinNetBase n) => OnApply(n); public static IDisposable Simulate() => new Scope(); }
    public static class ThingRequest { public static bool IsReplayingIntent; }
    public class RemoteCard(Card c) { public int Uid = c.uid; public int Num = (c as Thing)?.Num ?? 1; public Card Find() => c; public static implicit operator RemoteCard(Card c) => new(c); }
    public static class PendingSplit { public static RemoteCard Split(Card c) => new(c); }
    public static class PendingUid { public static bool IsPending(int uid) => uid < 0; }
    public static class SharedQuests { public static void Reject(ElinTogether.Net.ElinNetHost h,int p,int q,string reason) { } public static Chara? Actor(ElinTogether.Net.ElinNetHost h,int peer,int zone) => h.Accept && zone == EClass._zone.uid ? h.ActiveRemoteCharas.GetValueOrDefault(peer) : null; }
    public static class GuildStateSnapshot { public static bool IsGuildQuest(Quest q) => q.Guild; public static void MarkDirty() { } }
    public class LZ4Bytes {
        private object? data; public static int Captures;
        public static LZ4Bytes Create<T>(T value) { Captures++; return new(){ data=value }; }
        public T Decompress<T>() => (T)data!;
    }
}
namespace ElinTogether.Patches { public static class MsgRelayContext { public static IDisposable RedirectTo(Chara c) => new Scope(); } }

namespace ElinTogether.Models { public static class PersonalKarma { public static IDisposable For(Chara c) => new Scope(); } }
namespace EModding.Helper { public static class DerivedExtensions { public static IEnumerable<Type> OfDerived(this IEnumerable<Type> ts, Type type) => ts.Where(type.IsAssignableFrom); } }
namespace EModding.Helper.Runtime {
 public static class TypeQualifier { public static Type[] Declared = []; }
 public static class ReflectionExtensions { public static bool IsDeclaredMember(this System.Reflection.MethodInfo method) => method.DeclaringType == method.ReflectedType; }
}

public static class EmpPop { public static void Information(string text) { } }
public static class TextExt { public static string lang(this string text) => text; }
public static class Msg { public static void Say(string text,string title) { } }
