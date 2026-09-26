// Test doubles for game/transport APIs only; transaction logic is linked from production.
using ElinTogether.Models;
public class EClass { public static Chara pc = new(); public static Player player = new(); public static Sources sources = new(); public static object _zone = new(); }
public class Player { public CodexManager codex = new(); }
public class Sources { public Charas charas = new(); public class Charas { public Dictionary<string, object> map = new(); } }
public class CodexCreature { public int numCard; }
public class CodexManager { readonly Dictionary<string,CodexCreature> entries = new(); public CodexCreature GetOrCreate(string id) { if (!entries.ContainsKey(id)) entries[id] = new(); return entries[id]; } }
public class Card { public object? parent; public bool isDestroyed; public object GetRootCard() => parent is Card c ? c.GetRootCard() : this; public virtual Thing AddThing(Thing t, bool stack) => throw new NotImplementedException(); }
public class Chara : Card { public int uid; public bool IsInActiveMap = true; public Bag things; public Chara() { things = new(this); } public int Dist(object p) => 1; public override Thing AddThing(Thing t, bool stack) { t.parent = this; things.Items.Add(t); return t; } }
public class Bag(Chara owner) : List<Thing> { public int GridSize = 40; public bool Full; public Thing? Stack; public List<Thing> Items => this; public Dest GetDest(Thing t) => new() { stack = Stack, container = Full ? null : owner }; public List<Thing> List(Func<Thing,bool> f, bool onlyAccessible) => Items.Where(f).ToList(); }
public class Dest { public Card? container; public Thing? stack; public bool IsValid => stack != null || container != null; }
public class Thing : Card { public string id = "figure3", c_idRefCard = ""; public object trait = new TraitCard(); public object pos = new(); public int invY; public bool isEquipped; public int Num = 1; public void MakeFigureFrom(string s) { c_idRefCard = s; } public void Destroy() { isDestroyed = true; } public bool TryStackTo(Thing t) { t.Num += Num; Destroy(); return true; } }
public class TraitCard {}
public static class ThingGen { public static Thing? Last; public static Thing Create(string s) => Last = new(); }
public static class ContentCodex { public static void Collect(Thing t) { EClass.player.codex.GetOrCreate(t.c_idRefCard).numCard += t.Num; t.Destroy(); } }
public struct TestColor { public float r, g, b, a; }
public static class Msg {
 public static bool ignoreAll, alwaysVisible;
 public static TestColor currentColor;
 public static void Say(string s) {}
 public static void SetColor() => currentColor = default;
 public static string GetGameText(string s) => s;
 public static bool IsThirdPerson(int i) => i != 1;
 public static string GetRawText(string id, params object?[] args) => id;
}
public static class GameLang { public static string Parse(string id, bool plural, params string?[] args) => id + ":" + plural; }
public sealed class ScopeExit : IDisposable { public Action OnExit = null!; public void Dispose() => OnExit(); }
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple=true)]
 public class HarmonyPatch : Attribute { public HarmonyPatch() {} public HarmonyPatch(Type type, string name, params Type[] args) {} }
 public class HarmonyPrefix : Attribute {}
}
namespace ElinTogether.Models {
 public static class RemoteCraft { public static Chara? ProductReceiver; }
 public class MsgSayDelta : ElinDelta { public string Text = ""; public float R,G,B,A; }
}
public class GameIOContext {}
public class ElinPreLoadAttribute : Attribute {}
public static class EmpLog { public static void Information(string s, params object[] args) {} }
namespace MessagePack { public class MessagePackObjectAttribute : Attribute {} public class KeyAttribute(int n) : Attribute {} }
namespace ElinTogether.Helper { internal class Placeholder {} }
namespace ElinTogether.Models {
 public abstract class ElinDelta : EClass { public int OriginPeer; protected virtual void OnApply(ElinTogether.Net.ElinNetBase n) {} public void Apply(ElinTogether.Net.ElinNetBase n) => OnApply(n); protected static IDisposable Simulate() => new Scope(); class Scope : IDisposable { public void Dispose() {} } }
 public class CodexCountDelta : ElinDelta { public string Species = "", Message = ""; public int Count; public static void Publish(string s) {} public static void RefreshUI() {} }
 public static class CardCache { public static Dictionary<int,Card> Cards = new(); public static Card? Find(int uid) => Cards.GetValueOrDefault(uid); }
}
namespace ElinTogether.Net {
 public class Queue { public void AddRemote(ElinDelta d) {} }
 public class ElinNetBase { public Queue Delta = new(); }
 public class ElinNetHost : ElinNetBase { public Dictionary<int,Chara> ActiveRemoteCharas = new(); public List<ElinDelta> Replies = new(); public bool Deliver = true; public int LastPeer; public bool SendDeltaTo(int peer, ElinDelta d) { LastPeer = peer; if (!Deliver) return false; Replies.Add(d); return true; } }
 public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; }
}
