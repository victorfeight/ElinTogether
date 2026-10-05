using ElinTogether.Models;
using ElinTogether.Patches;
namespace ElinTogether.Helper { }
public class Card {
    public int uid; public bool IsPC;
    public Chara? Chara => this as Chara;
    public Trait trait = new(); public bool IsRestrainedResident;
    public void ModExp(int ele, int a) { if (this is Chara c) c.elements.ModExp(ele, a); }
    public void ModExpParty(int ele, int a) {
        if (!RemoteModExpPartyPatch.OnModExpParty(this, ele, a) || this is not Chara c) return;
        foreach (var m in c.party?.members ?? [c]) m.ModExp(ele, a);
    }
}
public class Party { public List<Chara> members = []; }
public class Chara : Card {
    public AIAct ai = new(); public Gauge stamina = new();
    public AIAct SetAI(AIAct next) { if (ThrowTraining.AllowTask(next)) ai = next; return next; }
    public bool IsRemotePlayer, isDead;
    public Party? party;
    public readonly ElementContainer elements;
    public Chara() { elements = new() { Card = this }; }
}
public class Trait { public bool CanBeDestroyed = true; }
public class TraitTrainingDummy : Trait { }
public class Gauge { public int value = 10; }
public class Thing : Card { public bool isDestroyed, Returning; public bool HasElement(int id) => id == 410 && Returning; public Thing Split(int n) => this; }
public class Element { public bool CanGainExp = true; }
public class ElementContainer {
    public Card? Card;
    public Dictionary<int, Element> dict = [];
    public List<(int Skill, float Amount)> Native = [];
    public void ModExp(int ele, float a, bool chain = false) {
        if (HostSkillProgressionPatch.Before(this, ele, a, chain)) Native.Add((ele, a));
    }
}
public class Point { public int x, z; public Point Copy() => new() { x=x, z=z }; public void Set(Point p) { x=p.x; z=p.z; } }
public static class Act { public static Chara? CC; public static Card? TC; public static Point TP = new(); }
public class AIAct { }
public class AI_PracticeDummy : AIAct { public Card? target; public Thing? throwItem; }
public class AI_UseCrafter : AIAct { }
namespace ElinTogether.Models.AI { }
public enum ThrowMethod { Default, Reward, Punish }
public static class ActThrow {
    public static Action<Card>? Native;
    public static int Calls;
    public static void Throw(Card c, Point p, Card? target, Thing t, ThrowMethod method) {
        var run = ActThrowEvent.OnClientThrow(c, p, target!, t, method, out var state);
        if (run) { Calls++; Native?.Invoke(c); }
        ActThrowEvent.OnClientThrowEnd(c, target!, t, state);
    }
}
public static class EmpLog { public static void Debug(string text, params object[] args) { } }
namespace ElinTogether.Net {
    public class ElinNetBase { public bool IsHost => this is ElinNetHost; public Queue Delta = new(); }
    public class Queue { public List<ElinDelta> Items = []; public void AddRemote(ElinDelta d) => Items.Add(d); }
    public class ElinNetHost : ElinNetBase { public Dictionary<int, Chara> ActiveRemoteCharas = []; }
    public class ElinNetClient : ElinNetBase { }
    public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; }
}
namespace ElinTogether.Models {
    public class AIUseCrafterArgs { }
    public abstract class ElinDelta {
        public static bool IsApplying;
        public int OriginPeer;
        protected abstract void OnApply(ElinTogether.Net.ElinNetBase net);
        public void Apply(ElinTogether.Net.ElinNetBase net) {
            var old = IsApplying; IsApplying = true;
            try { OnApply(net); } finally { IsApplying = old; }
        }
    }
    public class RemoteCard(Card card) {
        public int Num = 1;
        public Card Find() => card;
        public static implicit operator RemoteCard(Card c) => new(c);
        public static implicit operator Card(RemoteCard c) => c.Find();
    }
    public class Position {
        public static implicit operator Position(Point p) => new();
        public static implicit operator Point(Position p) => new();
    }
    public static class PendingSplit { public static RemoteCard Split(Thing t) => t; }
    public static class TaskCache { public static int Cancels; public static void CancelClientAct(object n, object d, object t) => Cancels++; }
    public record Award(Chara Actor, Guid Id, int Skill, float Amount);
    public static class OwnerProgressionAwards {
        public static List<Award> Items = [];
        public static Award Record(Chara actor, Guid id, int skill, float amount) {
            var a = new Award(actor, id, skill, amount); Items.Add(a); return a;
        }
    }
}
namespace HarmonyLib {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type t, string n, params Type[] args) { } }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPostfix : Attribute { }
}
namespace MessagePack {
    public class MessagePackObjectAttribute : Attribute { }
    public class KeyAttribute : Attribute { public KeyAttribute(int key) { } }
}
