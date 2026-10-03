// Native boundary models the inspected Pick/GetDest/PickOrDrop branches.
// Production hooks and packet handlers are linked by the project.
using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
public class EClass {public static Zone _zone=new();public static Map _map=new();public static Game game=new();public static Chara pc=new();}
public class Game {public Zone activeZone=>EClass._zone;public Spatials spatials=new();}
public class Spatials {public Dictionary<int,Zone> Others=[];public Zone? Find(int uid)=>uid==EClass._zone.uid?EClass._zone:Others.GetValueOrDefault(uid);}
public class Point {public int x,z;public Point(int x=1,int z=1){this.x=x;this.z=z;}}
public interface ICardParent { void RemoveCard(Card c); }
public class Zone : ICardParent {public int uid=1;public bool IsActiveZone=>ReferenceEquals(this,EClass._zone);public Map map=new();public void RemoveCard(Card c){if(IsActiveZone){map.Ground.Remove(c);map.CellEntries.Remove(c);}c.parent=null;}public Card AddCard(Card t,Point? p=null)=>AddCard(t,(p??t.pos).x,(p??t.pos).z);public Card AddCard(Card t,int x,int z){if(ZoneAddCardEvent.OnAddCardToZone(this,t,x,z)){t.parent?.RemoveCard(t);t.parent=this;t.pos=new(x,z);if(IsActiveZone){map.Ground.Add(t);map.CellEntries.Add(t);}}return t;}}
public class Map {public List<Card> Ground=[],CellEntries=[];public bool Smooth=true;public void TrySmoothPick(Point p,Thing t,Chara c){if(!CharaTrySmoothPickEvent.OnTrySmoothPick(p,t,c))return;if(c.IsPC&&(Smooth||(c.pos.x==p.x&&c.pos.z==p.z)))c.PickOrDrop(p,t);else EClass._zone.AddCard(t,p);}}
public enum PlaceState { none, roaming, installed }
public class Trait{} public class TraitAbility:Trait{}
public class Card : ICardParent {
 public int dir, Placements; public void Stub_SetPlacedState(PlaceState state,bool byPlayer){Placements++;}
 public int uid,Num=1;public string id="item";public bool isDestroyed,IsPC,IsPlayer;public ICardParent? parent;public Point pos=new();public Trait trait=new();public List<Thing> things=[];
 public bool IsHostOwned=>!PendingUid.IsPending(uid);public Card GetRootCard()=>parent is Card p?p.GetRootCard():this;public bool GetBool(string key)=>false;
 public bool CanStackTo(Thing t)=>!isDestroyed&&!t.isDestroyed&&id==t.id&&this!=t;
 public int Removes; public void RemoveCard(Card c){Removes++;if(c is Thing item)things.Remove(item);c.parent=null;}
 public Thing AddThing(Thing t,bool tryStack=true,int destInvX=-1,int destInvY=-1){if(!CardAddThingEvent.OnCardAddThing(this,t,tryStack,destInvX,destInvY))return t;if(t.parent==this)return t;t.parent?.RemoveCard(t);t.parent=this;t.invX=destInvX;t.invY=destInvY;return t;}
 public bool TryStackTo(Thing t){var result=false;if(!CardTryStackEvent.OnCardTryStackTo(this,t,ref result))return result;if(!CanStackTo(t))return false;t.Num+=Num;isDestroyed=true;return true;}
}
public class Thing:Card {public int invX,invY;public string c_idAbility="";}
public class Chara:Card {
 public Card? held;public bool Full,ThrowPick,IsPCParty=true;public Card? Destination;public Thing? Stack;public int Picks;
 public Thing Pick(Thing t,bool msg=true,bool tryStack=true){Thing result=null!;if(!CharaPickThingEvent.OnCharaPickThingy(this,t,ref result))return result;if(!AllyPickup.Prefix(this,t,msg,tryStack))return null!;Picks++;if(ThrowPick)throw new InvalidOperationException("native");if(t.parent==this)return t;if(Stack is not null&&t.CanStackTo(Stack)){t.TryStackTo(Stack);return Stack;}if(Full){if(t.parent!=EClass._zone)EClass._zone.AddCard(t,pos);return t;}return (Destination??this).AddThing(t);}
 public void PickOrDrop(Point p,Thing t,bool msg=true){if(!CharaPickOrDropEvent.OnCharaPickOrDrop(this,p,t))return;if(!Full||Stack is not null)Pick(t);else EClass._zone.AddCard(t,p);}
 public void TryPickGroundItem(){}
}
public class TaskBuild{}
// Invoke the actual linked Ally Expansion prefix after EMP's ordered prefix.
// Unity detours are not installed in this boundary harness.
public static class AllyPickup {
 private static readonly Type Patch=typeof(AutoActAllyExpansion.Patches.SmoothPick);
 private static readonly System.Reflection.FieldInfo Field=Patch.GetField("SmoothPickChara",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
 private static readonly Func<Chara,Thing,bool,bool,bool> Pick=(Func<Chara,Thing,bool,bool,bool>)Patch.GetMethod("Pick_Patch",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.CreateDelegate(typeof(Func<Chara,Thing,bool,bool,bool>));
 public static Chara? Source{get=>(Chara?)Field.GetValue(null);set=>Field.SetValue(null,value);}
 public static bool PickForPC{get=>AutoActAllyExpansion.Settings.PickForPC;set=>AutoActAllyExpansion.Settings.PickForPC=value;}
 public static int Redirects;
 public static bool Prefix(Chara c,Thing t,bool msg,bool stack){var run=Pick(c,t,msg,stack);if(!run)Redirects++;return run;}
}
public class Progress_Custom {public Chara? owner;public void OnProgressComplete(){}}
namespace AutoActAllyExpansion {public static class Settings{public static bool PickForPC;}}
namespace AutoActMod {public static class Extensions{public static bool IsNull(this object? value)=>value is null;}}
public static class LayerInventory {public static void SetDirty(Thing t){}}
public static class WidgetCurrentTool {public static bool dirty;}
namespace ElinTogether {public sealed class ScopeExit:IDisposable {public Action? OnExit;public void Dispose()=>OnExit?.Invoke();} public static class EmpLog {public static void Debug(string s,params object?[] a){}public static void Warning(string s,params object?[] a){}public static void Verbose(string s,params object?[] a){}}}
namespace ElinTogether.Helper {public static class Placeholder{}}
namespace MessagePack {public class MessagePackObjectAttribute:Attribute{}public class KeyAttribute(int key):Attribute{public int Key=key;}}
namespace HarmonyLib {
 public class HarmonyBefore(params string[] before):Attribute{public string[] Before=before;}
 [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method,AllowMultiple=true)]public class HarmonyPatch:Attribute{public HarmonyPatch(params object[] args){}public HarmonyPatch(Type type,string name,Type[] args){}}
 public class HarmonyPrefix:Attribute{}public class HarmonyPostfix:Attribute{}public class HarmonyTranspiler:Attribute{}public class HarmonyReversePatch(HarmonyReversePatchType t):Attribute{public HarmonyReversePatchType Kind=t;}public enum HarmonyReversePatchType{Snapshot}
 public class CodeInstruction{}public class CodeMatcher(IEnumerable<CodeInstruction> input){public CodeMatcher End()=>this;public CodeMatcher MatchStartBackwards(params object[] pattern)=>this;public CodeMatcher EnsureValid(string s)=>this;public CodeMatcher SetInstructionAndAdvance(CodeInstruction i)=>this;public IEnumerable<CodeInstruction> InstructionEnumeration()=>input;}
 public static class Transpilers{public static CodeInstruction EmitDelegate<T>(T d)=>new();}
}
namespace EModding.Helper {public class OperandContains(System.Reflection.Emit.OpCode op,string name){public string Name=name;public System.Reflection.Emit.OpCode Op=op;}}
namespace ElinTogether.Net {
 public class Buffer {public List<ElinDelta> Items=[];public void AddRemote(ElinDelta d)=>Items.Add(d);public void DeferLocal(ElinDelta d)=>Items.Add(d);}
 public class ElinNetBase {public bool IsHost=>this is ElinNetHost;public bool IsClient=>!IsHost;public Buffer Delta=new();}
 public class ElinNetClient:ElinNetBase{}public class ElinNetHost:ElinNetBase{public Dictionary<int,Chara> ActiveRemoteCharas=[];}
 public class NetSession {public static NetSession Instance=new();public ElinNetBase? Connection;public bool IsHost=>Connection?.IsHost==true;}
}
namespace ElinTogether.Models {
 public class CharaProgressCompleteDelta:ElinDelta {
  public static bool IsReplaying; public Action Replay=null!;
  protected override void OnApply(ElinNetBase net){var old=IsReplaying;IsReplaying=true;try{Replay();}finally{IsReplaying=old;}}
 }
 public static class ShopTrade{public static void DetachPaidDrag(Thing t,Card c){}}
 public class Position {public int X,Z;public static bool operator ==(Point? a,Position? b)=>a is not null&&b is not null&&a.x==b.X&&a.z==b.Z;public static bool operator !=(Point? a,Position? b)=>!(a==b);public override bool Equals(object? o)=>ReferenceEquals(this,o);public override int GetHashCode()=>HashCode.Combine(X,Z);public static implicit operator Position?(Point? p)=>p is null?null:new(){X=p.x,Z=p.z};public static implicit operator Point?(Position? p)=>p is null?null:new(p.X,p.Z);}
 public class LZ4Bytes {public static LZ4Bytes Create(Card c)=>new();}
 public class RemoteCard(Card card){public int Uid=>card.uid;public LZ4Bytes? Data;public int Num;public Card? Find()=>CardCache.Items.GetValueOrDefault(Uid);public static RemoteCard Create(Card c)=>new(c);[return:System.Diagnostics.CodeAnalysis.NotNullIfNotNull("c")]public static implicit operator RemoteCard?(Card? c)=>c is null?null:new(c);}
 public abstract class ElinDelta:EClass {
 internal int OriginPeer;private static int depth;public static bool IsApplying=>depth>0;public static bool IsRemoteStateLanding=>IsApplying;
 protected virtual void OnApply(ElinNetBase net){}protected virtual bool OnRefresh()=>true;public void Apply(ElinNetBase net){depth++;try{OnApply(net);}finally{depth--;}}
 internal static ElinTogether.ScopeExit Simulate(){var old=depth;depth=0;return new(){OnExit=()=>depth=old};}
 }
 public static class CardCache{public static Dictionary<int,Card> Items=[];public static void Forget(Card c){if(Items.GetValueOrDefault(c.uid)==c)Items.Remove(c.uid);}public static bool Contains(Card c)=>Items.GetValueOrDefault(c.uid)==c;public static bool TryAdopt(Card c){Items[c.uid]=c;return true;}public static void KeepAlive(Card c){}public static void UndoDestroy(Card c){}public static void DelayDestroy(Card c){} }
 public static class PendingUid {public static bool IsPending(int uid)=>uid<0;}
 public static class PendingSplit {public static int Resolve(int uid)=>-999;}
 public static class TaskCache {public static int Cancelled;public static void CancelClientAct(ElinNetBase n,ElinDelta d,RemoteCard c)=>Cancelled++;}
 public static class RemoteCraft {public static Chara? ProductReceiver=>null;}
 public class InvPlaceAbilityDelta:ElinDelta {public List<AbilityTokenSlot> Layout=[];public class AbilityTokenSlot{public string Alias="";public int InvX,InvY;}}
}
namespace ElinTogether.Patches {
 public static class QuickTransferTrace {public static string Item(Card c)=>c.uid.ToString();}
 public static class CharaProgressCompleteEvent {public static Chara? Chara;public static object? Action;public static List<ElinDelta> Results=[];public static bool ShouldPack(bool remoteOnly)=>NetSession.Instance.IsHost&&Chara is not null;public static void Pack(ElinDelta d)=>Results.Add(d);}
 public static class ZoneActivateEvent {public static bool IsHappening=>false;}
 public static class SpatialGenEvent {public static Zone? TryPop(int uid)=>null;}
 public static class InvSortEvent {public static HashSet<int>? QueuedSources;}
}
