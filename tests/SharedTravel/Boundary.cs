public enum EffectId { Return, Evac, Teleport }
public enum BlessedState { Normal }
public struct ActRef { }
public class ZoneTransition { }
public class ItemGeneral { }
public class Zone { public int uid = 10; }
public class GameIOContext { }
public class Game { public bool Save(bool isAutoSave=false,bool silent=false)=>true; }
public class ElinPreLoadAttribute : Attribute { }
public class Card { public Trait trait = new(); }
public class Trait { }
public class TraitScrollStatic : Trait { public EffectId idEffect; public void OnRead(Chara c) { } }
public class Act { public int id; }
public class Row { public string[] proc = []; }
public class ElementSources { public Dictionary<int, Row> map = []; }
public class Sources { public ElementSources elements = new(); }
public class Burden { public int Phase; public int GetPhase() => Phase; }
public class Chara : Card {
 public bool IsPC=>EClass.pc==this;
 public int uid; public bool isDead, IsInActiveMap = true; public string NameSimple = "actor";
 public Burden burden = new(); public void Tick() { } public void MoveZone(Zone z, ZoneTransition t) { }
}
public class Player {
 public class ReturnInfo { public int turns, uidDest; public bool askDest, isEvac; }
 public Chara chara = new(); public ReturnInfo? returnInfo;
}
public class DebugSettings { public bool ignoreWeight; }
public static class EClass {
 public static Player player = new(); public static Chara pc => player.chara;
 public static Zone _zone = new(); public static Sources sources = new(); public static DebugSettings debug = new();
}
public static class ActEffect { public static void Proc(EffectId id, int power, BlessedState state, Card cc, Card tc, ActRef act) { } }
public class AM_Adv { public void _OnUpdateInput() { } }
public class Layer {
 public Action? Killed; public bool Closed;
 public Layer SetOnKill(Action action) { Killed = action; return this; }
 public void Close() { Closed = true; Killed?.Invoke(); }
 public static implicit operator bool(Layer l) => !l.Closed;
}
public class LayerList : Layer {
 public void SetList2<T>(ICollection<T> list, Func<T,string> name, Action<T,ItemGeneral> onClick, Action<T,ItemGeneral> onInstantiate, bool autoClose = true) { }
}
public static class Msg { public static List<string> Lines = []; public static string Say(string text) { Lines.Add(text);return text; } }
public static class EmpLog { public static void Information(string text, params object?[] args) { } }
namespace ElinTogether.Helper { public class Placeholder { } }
namespace ElinTogether.Net {
 public class ElinNetBase { }
 public class ElinNetClient : ElinNetBase { }
 public class DeltaQueue { public List<object> Items=[];public void AddRemote(object delta) => Items.Add(delta); }
 public class ElinNetHost : ElinNetBase {
  public Dictionary<int, Chara> ActiveRemoteCharas=[];public HashSet<int> HumanPeers=[];public HashSet<Chara> Companions=[];
  public bool IsCompanionControlled(Chara actor)=>Companions.Contains(actor);
  public bool AcceptsPlayerInput(int peer)=>HumanPeers.Contains(peer);public DeltaQueue Delta=new();
 }
 public class NetSession { public static NetSession Instance=new();public ElinNetBase? Connection;public Chara Player=new(); }
}
namespace ElinTogether.Models {
 public class MsgSayDelta { public string Text="";public float R,G,B,A; }
 public class ScopeExit : IDisposable { public void Dispose() { } }
 public class ElinDelta { public static IDisposable Simulate()=>new ScopeExit(); }
}
namespace ElinTogether.Patches {
 public static class MsgRelayContext {
  public static IDisposable RedirectTo(Chara actor)=>new Models.ScopeExit();
  public static IDisposable RedirectTo(int peer)=>new Models.ScopeExit();
 }
}
