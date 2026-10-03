using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
using System.Reflection;
public class EClass { public static Zone _zone = new(){uid=7}; public static Map _map = new(); }
public class Map { public List<Chara> charas = new(); }
public class Zone { public int uid; public bool IsActiveZone=true; public void AddCard(Card c,int x,int z) { ZoneAddCardEvent.OnAddCardToZone(this,c,x,z); c.pos=new(x,z); if(c is Chara a) EClass._map.charas.Add(a); } }
public class Point(int x=0,int z=0) { public int x=x,z=z; public int Distance(Point p)=>Math.Max(Math.Abs(x-p.x),Math.Abs(z-p.z)); }
public class Card { public int uid; public bool isDestroyed; public bool IsPC; public bool isOn=true;public Trait trait=null!;public Point pos=new(); public Card? parent; public Card GetRootCard()=>parent?.GetRootCard()??this; }
public class Thing:Card { public int c_charges; }
public class Chara:Card { public bool isDead; public bool IsInActiveMap=true; public Elements elements=new(); }
public class Elements { public Element? GetElement(int id)=>null; }
public class Element { public Act? act; }
public class TraitRod:Trait { public Thing owner=null!; public string IdEffect="Summon",N1="shadow"; }
public class TraitAbility:Trait {}
public class Trait { public virtual bool OnUse(Chara c)=>true; }
public class ShrineData {public string id="knowledge";}
public class TraitShrine:Trait {
 public Card owner=null!;public ShrineData Shrine=new();public int Uses;public bool SawReplay,Fail;
 public bool CanUse(Chara c)=>owner.isOn;
 public override bool OnUse(Chara c){Uses++;SawReplay=ElinDelta.IsApplying;if(Fail)throw new InvalidOperationException("reward failed");
  var reward=new Thing{uid=7000+Uses};CardGenEvent.OnCardGenCreate(reward);EClass._zone.AddCard(reward,13,14);owner.isOn=false;return true;}
}
public class Act {
 public int id; public static Chara CC=null!; public static Card? TC; public static Point TP=new();
 public virtual bool Perform(Chara c,Card? t,Point? p) { CC=c;TC=t;TP=p??c.pos; return true; }
 public virtual bool Perform()=>true;
}
public class ActZap:Act {
 public TraitRod? trait; public static int Casts,Conditions; public static Point? LastAim;
 public override bool Perform(Chara c,Card? t,Point? p) {
  base.Perform(c,t,p); Casts++; LastAim=new(TP.x,TP.z);
  var rod=trait??throw new Exception("Missing rod context");
  if(rod.owner.c_charges<=0)return true;
  rod.owner.c_charges--;
  if(rod.IdEffect=="Summon") {
   var spawn=new Chara{uid=900+Casts}; CardGenEvent.OnCardGenCreate(spawn);
   EClass._zone.AddCard(spawn,TP.x,TP.z);
  } else Conditions++;
  TC=c; TP=new(99,99); // Vanilla mutates shared action context during effects.
  CharaActPerformEvent.OnCharaActPerform(this,true);
  return true;
 }
}
public class ActThrow:Act {} public class ActRanged:ActThrow {}
public class DynamicAct:Act {} public class DynamicAIAct:Act {} public class ActQuickCraft:Act {}
public static class ACT { public static Act Wait=new(),Melee=new(),Throw=new(),Ranged=new(),Kick=new(),Chat=new(),Pick=new(),Item=new(); public static Act Create(int id)=>throw new Exception("Zap must not use generic ACT.Create"); }
public static class ABILITY { public const int ActWait=1,ActMelee=2,ActThrow=3,ActRanged=4,ActKick=5,ActChat=6,ActPick=7,ActItem=8,ActRide=9,ActParasite=10,ActZap=5051; }
public class GameIOContext {} public class ElinPreLoadAttribute:Attribute {}
public static class EmpLog { public static void Warning(string s,params object?[] a){} public static void Information(string s,params object?[] a){} public static void Debug(string s,params object?[] a){} }
public static class CharaGen { public static Chara Create()=>new(); } public static class ThingGen { public static Thing _Create()=>new(); }
namespace ElinTogether.Helper { public static class OverrideMethodComparer { public static IEnumerable<MethodBase> FindAllOverrides(Type t,string n)=>[]; } }
namespace ElinTogether.Net {
 public class Queue { public List<ElinDelta> Items=new(); public void AddRemote(ElinDelta d)=>Items.Add(d); }
 public class ElinNetBase { public bool IsHost=>this is ElinNetHost;public bool IsClient=>this is ElinNetClient; public Queue Delta=new(); }
 public class ElinNetHost:ElinNetBase { public Dictionary<int,Chara> ActiveRemoteCharas=new(); }
 public class ElinNetClient:ElinNetBase {}
 public class NetSession { public static NetSession Instance=new(); public ElinNetBase? Connection; }
}
namespace ElinTogether.Models {
 public class Position { public required int X{get;init;} public required int Z{get;init;} public bool IsInActiveMapBounds=>X>=0&&Z>=0&&X<100&&Z<100; public static implicit operator Position?(Point? p)=>p==null?null:new(){X=p.x,Z=p.z};public static implicit operator Point?(Position? p)=>p==null?null:new(p.X,p.Z); }
 public class RemoteCard { public int Uid; public Card? Card; public Card? Find()=>Card; public static implicit operator RemoteCard?(Card? c)=>c==null?null:new(){Uid=c.uid,Card=c}; public static RemoteCard Create(Card c)=>c; }
 public class ElinDelta { public int OriginPeer; public static bool IsApplying; protected virtual void OnApply(ElinNetBase n){} public void Apply(ElinNetBase n){var old=IsApplying;IsApplying=true;try{OnApply(n);}finally{IsApplying=old;}} protected static IDisposable Simulate(){var old=IsApplying;IsApplying=false;return new Exit(()=>IsApplying=old);} }
 public sealed class Exit(Action a):IDisposable {public void Dispose()=>a();}
 public class CardGenDelta:ElinDelta {public Card Card=null!;public static CardGenDelta Create(Card c){CardCache.Add(c);return new(){Card=c};}}
 public class ZoneAddCardDelta:ElinDelta {public required RemoteCard Card{get;init;} public int ZoneUid{get;init;} public required Position Pos{get;init;}}
 public static class CardCache {public static HashSet<Card> Cards=new();public static int DestroyQueued;public static void Add(Card c)=>Cards.Add(c);public static bool Contains(Card c)=>Cards.Contains(c);public static void DelayDestroy(Card c)=>DestroyQueued++;}
 public static class PendingUid {public static int GetNext()=>-2;}
 public static class PendingContext {public static bool IsActive;}
 public static class CharaProgressCompleteDelta {public static bool IsReplaying;}
}
namespace ElinTogether.Patches {
 public static class ZoneActivateEvent {public static bool IsHappening;}
 public static class CharaProgressCompleteEvent {public static object? Action;public static bool ShouldPack(bool b)=>false;public static void Pack(ElinDelta d){}}
}
namespace MessagePack {public class MessagePackObjectAttribute:Attribute{} public class KeyAttribute(int n):Attribute { public int Value=>n; }}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method,AllowMultiple=true)] public class HarmonyPatch:Attribute {public HarmonyPatch(){}public HarmonyPatch(Type t,string s,params Type[] a){}}
 public class HarmonyPrefix:Attribute{} public class HarmonyPostfix:Attribute{}
 public static class AccessTools {public static MethodInfo Method(Type t,string n)=>t.GetMethod(n)!;}
}

public enum EffectId { Summon, Silence }
public struct ActRef { public Thing? refThing; }
public static class ActEffect { public static void ProcAt(){} }

public class ActMeleeCounter:Act {} public class ActMeleeParry:Act {} public class ActPray:Act {} public class TaskBuild {}

namespace ElinTogether.Models { internal static class TpSpellBridge { internal static bool IsManagedId(int id)=>false; internal static bool IsManaged(Act act)=>false; internal static Capture? Current; internal sealed class Capture{internal void ObserveGeneration(Card card){}} } }
