using ElinTogether.Models;
namespace ElinTogether.Helper { public class Placeholder { } }
public enum EffectId { Wish, Summon }
public enum BlessedState { Cursed = -1, Normal = 0, Blessed = 1 }
public struct ActRef { }
public class Point { public int x, z; }
public class Card { public Chara? Chara => this as Chara; }
public class Chara : Card {
    public int uid;
    public bool IsPC => EClass.pc == this;
    public bool isDead, IsInActiveMap = true;
    public string NameTitled => "player" + uid;
    public Point pos = new();
}
public class EClass {
    public static Game game = new();
    public static Chara pc => game.player.chara;
    public static Zone _zone = new();
}
public class Game { public Player player = new(); }
public class Player { public Chara chara = new() { uid = 1 }; }
public class Zone { public int uid = 7; }
public class GameIOContext { }
public class ElinPreLoadAttribute : Attribute { }
public static class EmpLog {
    public static void Information(string s, params object?[] args) { }
    public static void Warning(string s, params object?[] args) { }
}
public static class Msg { public static void Say(string id, Card c, string s) { } }
public static class Dialog {
    public static List<Action<bool, string>> Callbacks = new();
    public static void InputName(string id, string value, Action<bool, string> callback) => Callbacks.Add(callback);
}
public static class ActEffect {
    public record Result(string Text, int Recipient, int Power, BlessedState State, int X, int Z);
    public static List<Result> Results = new();
    public static bool Throw;
    public static void Proc(EffectId id, int power, BlessedState state, Card cc, Card? tc, ActRef actRef) { }
    public static bool Wish(string text, string name, int power, BlessedState state) {
        if (WishInteraction.Receiver != EClass.pc) throw new Exception("Wrong message recipient");
        if (Throw) throw new InvalidOperationException("Simulated vanilla failure");
        Results.Add(new(text, EClass.pc.uid, power, state, EClass.pc.pos.x, EClass.pc.pos.z));
        return true;
    }
}
namespace ElinTogether.Net {
    public class ElinNetBase { public Queue Delta = new(); }
    public class Queue { public List<ElinDelta> Items = new(); public void AddRemote(ElinDelta d) => Items.Add(d); }
    public class ElinNetClient : ElinNetBase { }
    public class ElinNetHost : ElinNetBase {
        public Dictionary<int, Chara> ActiveRemoteCharas = new();
        public List<(int Peer, ElinDelta Delta)> Sent = new();
        public bool Deliver = true;
        public bool SendDeltaTo(int peer, ElinDelta delta) { Sent.Add((peer, delta)); return Deliver; }
    }
    public class NetSession {
        public static NetSession Instance = new();
        public ElinNetBase? Connection;
    }
}
namespace ElinTogether.Models {
    public abstract class ElinDelta {
        public int OriginPeer;
        protected abstract void OnApply(ElinTogether.Net.ElinNetBase net);
        public void Apply(ElinTogether.Net.ElinNetBase net) => OnApply(net);
        protected static IDisposable Simulate() => new Scope();
        private class Scope : IDisposable { public void Dispose() { } }
    }
    public class MsgSayDelta : ElinDelta {
        public string Text = "";
        public float R, G, B, A;
        protected override void OnApply(ElinTogether.Net.ElinNetBase net) { }
    }
}
namespace MessagePack {
    public class MessagePackObjectAttribute : Attribute { }
    public class KeyAttribute(int key) : Attribute { public int Key = key; }
}
namespace HarmonyLib {
    public class HarmonyPatch(Type type, string method, params Type[] args) : Attribute {
        public Type Type = type;
        public string Method = method;
        public Type[] Args = args;
    }
    public class HarmonyPrefix : Attribute { }
}
