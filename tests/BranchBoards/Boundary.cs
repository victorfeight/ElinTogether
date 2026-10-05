using ElinTogether.Models;
namespace ElinTogether.Helper { }
public class EClass {
    public static Zone _zone = new(); public static Player player = new(); public static Chara pc => player.chara;
    public static FactionBranch Branch = new(); public static Home Home = new(); public static Core core = new(); public static Game game = new();
}
public class Core { public bool IsGameStarted = true; }
public class Game { public bool isLoading; }
public class Player { public Chara chara = new(); }
public class Zone { public int uid = 10; public bool IsPCFaction = true; }
public class Home { public HashSet<int> globalPolicies = []; }
public class Card { public int uid; }
public class Chara : Card {
    public bool IsInActiveMap = true, isDead, IsDisabled, HomeMember, Protected;
    public Inventory things = new(); public int Money = 100, Payments;
    public bool IsHomeMember() => HomeMember;
    public int GetCurrency(string s) => Money;
    public void ModCurrency(int n, string s) { Money += n; Payments++; }
    public void SetBool(int id, bool b) { }
}
public class Thing { public int Num = 1; public void ModNum(int n) => Num += n; }
public class Inventory { public Thing? Ticket; public Thing? Find(string s) => Ticket; }
public class Row { public string category = "skill"; public int[] cost = [1]; }
public class Element { public int id, vBase = 1; public int ValueWithoutLink => vBase; public Row source = new(); public bool Hidden; public bool HasTag(string s) => Hidden; }
public class Elements {
    public Dictionary<int, Element> dict = [];
    public Element? GetElement(int id) => dict.GetValueOrDefault(id);
    public int Base(int id) => GetElement(id)?.vBase ?? 0;
    public void SetBase(int id, int n) { if (!dict.TryGetValue(id, out var e)) dict[id] = e = new() { id = id }; e.vBase = n; }
    public void ModBase(int id, int n) => SetBase(id, Base(id) + n);
}
public class Resource { public int value = 100; public void Mod(int n) => value += n; }
public class Resources { public Resource knowledge = new(); public void SetDirty() { } }
public class ResearchRow { public int tech = 20; }
public class ResearchPlan { public string id = "test"; public int rank = 1; public ResearchRow source = new(); }
public class ResearchManager {
    public FactionBranch? branch; public List<ResearchPlan> plans = []; public int Completed;
    public bool CanCompletePlan(ResearchPlan p) => EClass.Branch.resources.knowledge.value >= p.source.tech;
    public void CompletePlan(ResearchPlan p) { Completed++; plans.Remove(p); }
    public void SetOwner(FactionBranch b) => branch = b;
    public ResearchManager Copy() => new() { plans = plans.Select(p => new ResearchPlan { id = p.id, rank = p.rank, source = new() { tech = p.source.tech } }).ToList(), Completed = Completed };
}
public class Policy { public int id = 4, Cost = 2; public bool active; }
public class PolicyManager {
    public List<Policy> list = []; public int Refreshes;
    public int CurrentAP() => list.Where(p => p.active).Sum(p => p.Cost);
    public void RefreshEffects() => Refreshes++;
    public void SetOwner(FactionBranch b) { }
    public PolicyManager Copy() => new() { list = list.Select(p => new Policy { id = p.id, Cost = p.Cost, active = p.active }).ToList() };
}
public class HireInfo { public Chara chara = new(); public bool isNew; public int deadline; }
public class FactionBranch {
    public Elements elements = new(); public ResearchManager researches = new(); public PolicyManager policies = new(); public Resources resources = new();
    public List<HireInfo> listRecruit = []; public int lastUpdateReqruit, MaxAP = 3, Hires; public bool ThrowOnPrice;
    public int GetTechUpgradeCost(Element e) { if (ThrowOnPrice) throw new InvalidOperationException("price"); return e.vBase + 1; }
    public void UpdateReqruits() { }
    public void Recruit(Chara c) { Hires++; listRecruit.RemoveAll(i => i.chara == c); c.HomeMember = true; }
}
public static class CalcGold { public static int Hire(Chara c) => 10; }
public static class Msg { public static void Say(string s) { } }
public static class EmpLog { public static void Information(string s, params object[] a) { } public static void Warning(string s, params object[] a) { } }
public class ElinPreLoadAttribute : Attribute { }
public class GameIOContext { }
namespace ElinTogether.Patches {
    public static class ZoneActivateEvent { public static bool IsHappening; }
    public static class BranchBoardUI { public static int Builds; public static void Refresh() { } public static void RefreshBuildMenu() => Builds++; }
    public static class MsgRelayContext { public static IDisposable RedirectTo(Chara c) => new Scope(); private class Scope : IDisposable { public void Dispose() { } } }
}
namespace ElinTogether.Net {
    public class Queue { public List<ElinDelta> Items = []; public void AddRemote(ElinDelta d) => Items.Add(d); public void DeferLocal(ElinDelta d) => Items.Add(d); }
    public class ElinNetBase { public bool IsHost => this is ElinNetHost; public Queue Delta = new(); }
    public class ElinNetClient : ElinNetBase { }
    public class ElinNetHost : ElinNetBase {
        public Dictionary<int, Chara> ActiveRemoteCharas = []; public List<ElinDelta> Replies = [];
        public bool AcceptsPlayerInput(int p) => ActiveRemoteCharas.ContainsKey(p);
        public void SendDeltaTo(int p, ElinDelta d) => Replies.Add(d);
    }
    public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; }
}
namespace ElinTogether.Models {
    public abstract class ElinDelta : EClass {
        public int OriginPeer;
        protected abstract void OnApply(ElinTogether.Net.ElinNetBase net);
        public void Apply(ElinTogether.Net.ElinNetBase net) => OnApply(net);
        public static IDisposable Simulate() => new Scope(); private class Scope : IDisposable { public void Dispose() { } }
    }
    public class MsgSayDelta : ElinDelta { public string Text = ""; public int R,G,B,A; protected override void OnApply(ElinTogether.Net.ElinNetBase n) { } }
    public class RemoteCard(Chara c) { public Chara? Find() => c; public static RemoteCard Create(Chara c, bool addToCache, bool withData) => new(c); }
    public class ReserveEntry { public required RemoteCard Member; public bool IsNew; public int Deadline; }
    public static class PartyMembership { public static bool Protected(Chara c) => c.Protected; }
    public class LZ4Bytes(object o) {
        public static LZ4Bytes Create<T>(T value) => new(Copy(value!));
        private static object Copy(object o) => o switch { ResearchManager r => r.Copy(), PolicyManager p => p.Copy(), _ => o };
        public T Decompress<T>() => (T)Copy(o);
    }
}
namespace MessagePack { public class MessagePackObjectAttribute : Attribute { } public class KeyAttribute : Attribute { public KeyAttribute(int n) { } } }
