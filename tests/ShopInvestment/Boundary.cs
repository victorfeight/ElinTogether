using ElinTogether.Models;
public class Core { public bool IsGameStarted = true; }
public class SourceTable { public Dictionary<int, object> map = new() { [292] = new(), [10] = new(), [20] = new() }; }
public class Sources { public SourceTable elements = new(); }
public class EClass {
    public static Core core = new(); public static Sources sources = new();
    public static Game game = new(); public static Zone _zone = new();
    public static int RandomCalls; public static int rnd(int max) { RandomCalls++; return 2; }
    public static Chara pc => game.player.chara;
}
public class Game { public Player player = new(); }
public class Player { public Chara chara = new(); }
public class Zone { public bool AllowInvest = true; public int uid = 7, influence, development, investment; public void ModDevelopment(int a) => development += a; public void ModInfluence(int a) => influence += a; }
public class Card { public int uid; }
public class Element {
    public int id, vBase = 1, vExp, vPotential = 100, vTempPotential, vSource, vLink;
    public void CheckLevelBonus(Elements owner) { }
    public void OnChangeValue() { }
}
public class Elements {
    public Dictionary<int, Element> dict = [];
    public Element GetOrCreateElement(int id) {
        if (!dict.TryGetValue(id, out var e)) dict[id] = e = new() { id = id };
        return e;
    }
    public void Remove(int id) => dict.Remove(id);
}
public class Chara : Card {
    public int LV = 1, exp, feat, ModExpCalls;
    public bool IsPC => EClass.pc == this;
    public Elements elements = new();
    private readonly Dictionary<string, string?> strings = [];
    public string? GetStr(string key) => strings.GetValueOrDefault(key);
    public void SetStr(string key, string? value) => strings[key] = value;
    public int c_invest, Money = 10000, Discount, Experience, Charges, Distance = 1;
    public bool isDead, IsInActiveMap = true;
    public Trait trait = new();
    public int Dist(Chara c) => Distance;
    public int GetCurrency() => Money;
    public void ModCurrency(int a) { Money += a; Charges++; }
    public void ModExp(int id, int a) {
        ModExpCalls++; Experience += a;
        elements.GetOrCreateElement(id).vExp += a;
        elements.GetOrCreateElement(10).vExp += 3;
        LV++; exp += 4; feat++;
    }
}
public class Trait { public bool CanInvest = true, CanInvestTown = true; }
public class Guild {
    public static Guild? Current; public static Guild? GetCurrentGuild() => Current;
    public static Guild Merchant = new(); public int Exp;
    public void AddContribution(int a) => Exp += a;
}
public static class CalcMoney {
    public static bool Throw;
    public static int InvestZone(Chara ignored) => 500 + EClass._zone.development * 50 - EClass.pc.Discount;
    public static int InvestShop(Chara ignored, Chara shop) {
        if (Throw) throw new InvalidOperationException("price error");
        return 200 + shop.c_invest * 700 - EClass.pc.Discount;
    }
}
public static class SE { public static int Payments; public static void Pay() => Payments++; }
public static class Msg { public static void Say(string s) { } }
public static class EmpLog {
    public static void Debug(object s, params object[] args) { }
    public static void Information(string s, params object[] args) { }
    public static void Warning(string s, params object[] args) { }
}
public class GameIOContext { }
public class ElinPreLoadAttribute : Attribute { }
public static class GameLang { public static string refDrama1 = "200"; }
public static class Lang { public static string lang(this string s) => s; }
public class DramaEvent { public string step = ""; }
public class DramaEventMethod : DramaEvent { public Action action = () => { }; }
public class DramaChoice { public string text = ""; public Action? onJump; }
public class DramaEventTalk { public List<DramaChoice> choices = []; }
public class DramaManager { public DramaEventTalk lastTalk = new(); }
public class DramaSequence { public bool isExited; }
public class LayerDrama {
    public static LayerDrama? Instance; public DramaManager drama = new();
    public static implicit operator bool(LayerDrama? l) => l is not null;
}
public class DramaCustomSequence {
    public List<DramaEvent> events = []; public DramaManager manager = new();
    public DramaSequence sequence = new(); public string StepDefault = "default", LastJump = "";
    public void Build(Chara c) { }
    public void TempGoto(string s) { LastJump = s; manager.lastTalk = new(); }
    public void _TempTalk(string who, string s, string jump) => manager.lastTalk = new();
}
namespace ElinTogether.Net {
    public class ElinNetBase { internal readonly NetworkFlightRecorder Trace = new(_ => {}, watchdog: false); public ElinDeltaManager Delta = new(); public bool IsHost => this is ElinNetHost; public void ReportDesync(string s) => throw new Exception(s); }
    public class ElinNetHost : ElinNetBase {
        public Dictionary<int, Chara> ActiveRemoteCharas = [];
        public bool AcceptsPlayerInput(int peer) => ActiveRemoteCharas.ContainsKey(peer);
        public List<InvestmentResultDelta> Replies = [];
        public bool SendDeltaTo(int peer, ElinDelta d) { Replies.Add((InvestmentResultDelta)d); return true; }
    }
    public class ElinNetClient : ElinNetBase { }
    public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; }
}
namespace ElinTogether.Models {
    public class RemoteCard(Card c) {
        public static Dictionary<int, Card> Resolved = [];
        public int Uid => c.uid;
        public object Find() => Resolved.GetValueOrDefault(Uid, c);
        public static implicit operator RemoteCard(Card c) => new(c);
    }
    public abstract class ElinDelta : EClass {
        public int OriginPeer, DeferCount;
        public bool RequiresGameStarted => true;
        public enum OverrideOrder { Stack, First, Last }
        internal virtual OverrideOrder Order => OverrideOrder.Stack;
        protected virtual bool OnRefresh() => true;
        public bool Refresh() => OnRefresh();
        protected abstract void OnApply(ElinTogether.Net.ElinNetBase net);
        public void Apply(ElinTogether.Net.ElinNetBase net) => OnApply(net);
        public static IDisposable Simulate() => new Scope();
        private class Scope : IDisposable { public void Dispose() { } }
    }
}
namespace ElinTogether.Models {
    public class CharaLevelDelta : ElinDelta { public required RemoteCard Owner; protected override void OnApply(ElinTogether.Net.ElinNetBase n) { } }
    public class CharaFeatPointDelta : ElinDelta { public required RemoteCard Owner; protected override void OnApply(ElinTogether.Net.ElinNetBase n) { } }
    public class DynamicDelta : ElinDelta { protected override void OnApply(ElinTogether.Net.ElinNetBase n) { } }
    public class GameDelta : DynamicDelta { }
    public class CharaTickDelta : DynamicDelta { }
    public class CharaTickConditionDelta : DynamicDelta { }
    public class CardGenDelta { public static void ClearRecordedUids() { } }
    public class QuestCreateDelta { public static void ClearRecordedUids() { } }
}
namespace MessagePack {
    public class MessagePackObjectAttribute : Attribute { }
    public class KeyAttribute : Attribute { public KeyAttribute(int index) { } }
}
namespace HarmonyLib {
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
    public class HarmonyPostfix : Attribute { }
}
namespace ElinTogether.Helper { public class Marker { } }
