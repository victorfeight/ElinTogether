using ElinTogether.Models;
public class EClass {
    public static Game game = new(); public static Player player = new(); public static Chara pc => player.chara;
    public static Zone _zone = new();
}
public class Game { public QuestManager quests = new(); }
public class Player { public Chara chara = new() { uid = 1 }; }
public class Zone { public int uid = 10; public HashSet<int> completedQuests = []; }
public class Card { public int uid; }
public class Chara : Card {
    public bool IsInActiveMap = true, IsAliveInCurrentZone = true, IsDisabled, isDead;
    public Quest? quest; public List<Thing> Inventory = [];
}
public class Thing : Card { public int Num = 3; public bool isDestroyed, Eligible = true, Important, Equipped; }
public class Source { public string[] drama = []; }
public class QuestPerson { public Chara? chara; }
public class Quest {
    public int uid = 100; public string id = "supply"; public bool IsRandomQuest = true, UseInstanceZone, isComplete, Expired, Visible = true, Guild;
    public Source source = new(); public QuestPerson person = new(); public Chara? chara => person.chara;
    public bool IsExpired => Expired || isComplete; public Zone? ClientZone = EClass._zone;
    public Chara? DestChara => chara; public int AffinityGain = 5;
    public int Completed, Messages, Starts; public Chara? StartedAs, CompletedAs;
    public bool ThrowOnStart;
    public bool IsVisibleOnQuestBoard() => Visible;
    public void Start() { Starts++; StartedAs = EClass.pc; if (ThrowOnStart) throw new InvalidOperationException(); }
    public void Complete() { Completed++; CompletedAs = EClass.pc; isComplete = true; EClass.game.quests.Remove(this); }
    public void ShowCompleteText() => Messages++;
    public virtual bool Deliver(Chara c, Thing? t = null) => false;
}
public class QuestDeliver : Quest {
    public int num = 2; public bool ThrowOnDeliver; public List<Thing> Stock = []; public int Deliveries;
    public List<Thing> ListDestThing() => EClass.pc.Inventory.Concat(Stock).Where(t => t.Eligible && !t.Important && !t.Equipped && !t.isDestroyed).ToList();
    public Thing? GetDestThing() => ListDestThing().FirstOrDefault();
    public bool CanDeliverToClient(Chara c) => c == chara && GetDestThing() != null;
    public override bool Deliver(Chara c, Thing? t = null) {
        if (ThrowOnDeliver) throw new InvalidOperationException();
        Deliveries++; t!.Num -= num; Complete(); return true;
    }
}
public class QuestManager {
    public const int MaxRandomQuest = 5; public List<Quest> list = [], globalList = [];
    public HashSet<string> completedIDs = [], completedTypes = [];
    public int CountRandomQuest() => list.Count(q => q.IsRandomQuest);
    public Quest Start(Quest q) { list.Insert(0, q); q.Start(); return q; }
    public void Remove(Quest q) => list.Remove(q);
}
public static class EmpLog { public static void Information(string s, params object[] v) { } public static void Warning(string s, params object[] v) { } public static void Debug(string s, params object[] v) { } }
public static class EmpPop { public static void Information(string s) { } }
public static class Extensions { public static bool IsEmpty(this string[] a) => a.Length == 0; public static string lang(this string s) => s; }
namespace ElinTogether.Helper {
    public static class OverrideMethodComparer { public static IEnumerable<System.Reflection.MethodBase> FindAllOverrides(Type t, string n, params Type[] p) => []; }
}
namespace ElinTogether.Net {
    public class Queue { public List<ElinDelta> Items = []; public void AddRemote(ElinDelta d) => Items.Add(d); }
    public class ElinNetBase { public bool IsHost => this is ElinNetHost; public Queue Delta = new(); }
    public class ElinNetClient : ElinNetBase { }
    public class ElinNetHost : ElinNetBase {
        public Dictionary<int, Chara> ActiveRemoteCharas = []; public bool AcceptInput = true;
        public bool AcceptsPlayerInput(int peer) => AcceptInput && ActiveRemoteCharas.ContainsKey(peer);
        public void SendDeltaTo(int peer, ElinDelta d) => Delta.AddRemote(d);
    }
    public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; }
}
namespace ElinTogether.Models {
    public class Scope(Action done) : IDisposable { public void Dispose() => done(); }
    public abstract class ElinDelta : EClass {
        public int OriginPeer = 1; public static bool IsApplying;
        protected abstract void OnApply(ElinTogether.Net.ElinNetBase net);
        public void Apply(ElinTogether.Net.ElinNetBase net) { var was = IsApplying; IsApplying = true; try { OnApply(net); } finally { IsApplying = was; } }
        public static IDisposable Simulate() { var was = IsApplying; IsApplying = false; return new Scope(() => IsApplying = was); }
    }
    public class RemoteCard(Card card) {
        public Card Find() => card;
        public static implicit operator RemoteCard(Card c) => new(c);
    }
    public class MsgSayDelta : ElinDelta { public string Text = ""; public int R,G,B,A; protected override void OnApply(ElinTogether.Net.ElinNetBase n) { } }
    public class SocialStateDelta : ElinDelta { public static SocialStateDelta Capture(Chara c) => new(); protected override void OnApply(ElinTogether.Net.ElinNetBase n) { } }
    public static class PersonalKarma { public static Chara? Actor; public static IDisposable For(Chara c) { var was = Actor; Actor = c; return new Scope(() => Actor = was); } }
    public static class GuildStateSnapshot { public static bool IsGuildQuest(Quest q) => q.Guild; }
}
namespace ElinTogether.Patches {
    public static class MsgRelayContext { public static IDisposable RedirectTo(Chara c) => new Scope(() => { }); }
}
namespace MessagePack { public class MessagePackObjectAttribute : Attribute { } public class KeyAttribute(int n) : Attribute { public int N = n; } }
namespace HarmonyLib {
    public class HarmonyPatch : Attribute { public HarmonyPatch() { } public HarmonyPatch(Type t, string n) { } }
    public class HarmonyPrefix : Attribute { } public class HarmonyPostfix : Attribute { }
}
