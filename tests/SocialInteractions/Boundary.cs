// Unity/transport boundary only. Requests, scopes, patches, split mapping and
// result application below are linked directly from production sources.
using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
using System.Collections.Immutable;
public class EClass {
 public static Game game=new(); public static Player player=>game.player; public static Chara pc=>player.chara;
 public static Zone _zone=new();
}
public class Game { public Player player=new(); }
public class Player { public Chara chara=null!; }
public class Zone { public int uid=1; }
public class GameIOContext { }
public class ElinPreLoadAttribute:Attribute { }
public class Card {
 public int uid,Num=1; public Card? parent; public bool isDestroyed;
 public Card GetRootCard()=>parent?.GetRootCard()??this;
}
public class Thing:Card {
 public bool Important; public static int Next=1000;
 public Thing Split(int n) { if(n==Num)return this; Num-=n; var t=new Thing{uid=++Next,Num=n};CardCache.Add(t);return t; }
}
public class Chara:Card {
 public bool IsPC=>ReferenceEquals(this,EClass.pc); public bool IsDisabled,IsInActiveMap=true,IsHumanSpeak=true,Hostile,Full,Heavy,knowFav;
 public int Distance=1,_affinity,interest=100,GiftCalls,EquipCalls,ClearCalls,TalkCalls; public object ai=new(); public Thing nextUse=null!;
 public Dictionary<int,string?> Strings=[]; public List<Thing> Inventory=[]; public Action<Chara,Thing>? GiftEffect;
 public ElementContainer elements=new();
 public int TaskStarts;
 public void SetAI(AIAct g){HumanGiftInventoryPatch.Task(this,g);if(NetSession.Instance.Connection is ElinNetHost h&&h.ActiveRemoteCharas.ContainsValue(this)&&!h.IsCompanionControlled(this))return;ai=g;TaskStarts++;}
 public Affinity affinity { get { Affinity.CC=this;return new(); } }
 public int Dist(Chara c)=>Distance; public bool IsHostile(Chara c)=>Hostile; public bool HasElement(int id)=>false;
 public bool CanAcceptGift(Chara c,Card t)=>!Full&&t is Thing{Important:false};public bool IsValidGiftWeight(Card t,int n)=>!Heavy;
 public string? GetStr(int k)=>Strings.GetValueOrDefault(k);public void SetStr(int k,string? v)=>Strings[k]=v;
 public Thing AddThing(Thing t){(t.parent as Chara)?.Inventory.Remove(t); t.parent=this;if(!Inventory.Contains(t))Inventory.Add(t);return t;}
 public bool TryEquip(Thing t){if(!HumanGiftInventoryPatch.Equip(this))return false;EquipCalls++;return true;}
 public void TryClearInventory(){if(HumanGiftInventoryPatch.Clear(this))ClearCalls++;}
 public void GiveGift(Chara c,Thing t){
  if(!CharaGiveGiftEvent.OnGiveGift(this,c,t,out var scope))return;
  try {GiftCalls++;if(GiftEffect!=null){GiftEffect(c,t);return;}
   c.AddThing(t);c.nextUse=t;
   // Vanilla's self guard is the regression trigger under an incorrect PC.
   if(c!=EClass.pc)c._affinity+=10;
   c.TryEquip(t);c.TryClearInventory();
  } finally {CharaGiveGiftEvent.End(scope);}
 }
}
public class Affinity {
 public static Chara CC=null!;
 public void OnTalkRumor(){
  if(!SocialTalkEvent.Before(out var scope))return;
  try{CC.TalkCalls++;CC.interest-=10;if(CC!=EClass.pc)CC._affinity++;EClass.pc.elements.Get(291).vExp+=20;}
  finally{SocialTalkEvent.End(scope);}
 }
}
public class Element {public int id,vBase,vExp,vPotential,vTempPotential;}
public class ElementContainer {public Dictionary<int,Element> dict=[];public Element Get(int id){if(!dict.TryGetValue(id,out var e))dict[id]=e=new(){id=id};return e;}}
public class AIAct { }
public class AI_Massage:AIAct {public Chara target=null!;}
public class AI_ArmPillow:AI_Massage { }
namespace MessagePack {
 public class MessagePackObjectAttribute:Attribute { }
 public class KeyAttribute:Attribute {public KeyAttribute(int key){} }
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method,AllowMultiple=true)] public class HarmonyPatch:Attribute {public HarmonyPatch(){}public HarmonyPatch(Type t,string m){} }
 public class HarmonyPrefix:Attribute {} public class HarmonyFinalizer:Attribute {}
 public class HarmonyPriority:Attribute {public HarmonyPriority(int priority){} } public static class Priority {public const int First=800;}
}
namespace ElinTogether.Helper { public class Marker {} }
namespace ElinTogether {
 public sealed class ScopeExit:IDisposable {public Action? OnExit;public void Dispose()=>OnExit?.Invoke();}
 public static class EmpLog{public static void Information(string s,params object[] a){} }
}
namespace ElinTogether.Patches {
 internal static class MsgRelayContext {internal static ElinTogether.ScopeExit RedirectTo(Chara c)=>new();}
}
namespace ElinTogether.Models {
 public abstract class ElinDelta:EClass {
  public int OriginPeer=1;public static bool IsApplying; protected virtual void OnApply(ElinNetBase net){}
  public void Apply(ElinNetBase net){var old=IsApplying;IsApplying=true;try{OnApply(net);}finally{IsApplying=old;}}
  public static IDisposable Simulate(){var old=IsApplying;IsApplying=false;return new ElinTogether.ScopeExit{OnExit=()=>IsApplying=old};}
 }
 public class RemoteCard {
  public int Uid,Num;
  public Card? Find()=>CardCache.Find(Uid);
  public static RemoteCard Create(Card c)=>new(){Uid=c.uid,Num=c.Num};
  public static implicit operator RemoteCard(Card c)=>Create(c);
  public static implicit operator Chara(RemoteCard c)=>(Chara)c.Find()!;
 }
 public static class CardCache {
  public static Dictionary<int,Card> Cards=[];public static List<int> Delayed=[];
  public static void Add(Card c)=>Cards[c.uid]=c;public static Card? Find(int id)=>Cards.GetValueOrDefault(id);
  public static void DelayDestroy(Card c)=>Delayed.Add(c.uid);
 }
 public static class PendingUid {public static bool IsPending(int id)=>id>=0x40000000;}
 public class MsgSayDelta:ElinDelta {public string Text="";public float R,G,B,A;}
 public class ElementChangeDelta {
  public required RemoteCard Owner;public int Element;public ImmutableArray<int> Value;
  public static ElementChangeDelta Create(Card c,global::Element e)=>new(){Owner=c,Element=e.id,Value=[e.vBase,e.vExp,e.vPotential,e.vTempPotential]};
  public void Write(Chara c){var e=c.elements.Get(Element);e.vBase=Value[0];e.vExp=Value[1];e.vPotential=Value[2];e.vTempPotential=Value[3];}
  public void RefreshDerived(Chara c){}
 }
 public static class PersonalKarma {public static IDisposable For(Chara c)=>new ElinTogether.ScopeExit();}
 public static class PlayerControl {public static bool LocalInputBlocked;}
 public abstract class TaskArgsBase {public abstract AIAct CreateSubAct();}
}
namespace ElinTogether.Net {
 public class NetSession {public static NetSession Instance=new();public ElinNetBase? Connection;}
 public class DeltaQueue {public List<ElinDelta> Items=[];public void AddRemote(ElinDelta d)=>Items.Add(d);}
 public class ElinNetBase {public bool IsHost=>this is ElinNetHost;public bool IsClient=>!IsHost;public DeltaQueue Delta=new();}
 public class ElinNetClient:ElinNetBase { }
 public class ElinNetHost:ElinNetBase {
  public Dictionary<int,Chara> ActiveRemoteCharas=[];public HashSet<Chara> Companions=[];public bool Accept=true;
  public bool AcceptsPlayerInput(int peer)=>Accept;
  public bool IsCompanionControlled(Chara c)=>Companions.Contains(c);
  public void SendDeltaTo(int peer,ElinDelta delta)=>Delta.AddRemote(delta);
 }
}
