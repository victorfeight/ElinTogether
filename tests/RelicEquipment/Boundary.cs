using ElinTogether.Models;
using ElinTogether.Patches;
public class EClass {public static Chara pc {get;set;}=null!;}
public class Card {
 public int uid;private int _feat;public Card? parent;public bool IsPC=>ReferenceEquals(this,EClass.pc);
 public int feat {get=>_feat;set {CharaFeatPointEvent.OnSetFeat(this,out var before);_feat=value;CharaFeatPointEvent.OnSetFeatEnd(this,before);}}
 public Dictionary<int,bool> Bools=[];public Dictionary<string,string> Strings=[];
 public bool GetBool(int id)=>Bools.GetValueOrDefault(id);public bool GetBool(string id)=>false;public void SetBool(int id,bool value)=>Bools[id]=value;
 public string? GetStr(string id)=>Strings.GetValueOrDefault(id);public Card GetRootCard()=>parent?.GetRootCard()??this;
}
public class Chara:Card {public CharaBody body;public AIAct ai=new();public int Power;public Chara(){body=new(this);}public void OnSleep(int power,int days,bool sun){if(IsPC)foreach(var s in body.slots)s.thing?.SetBool(135,false);RelicEquipmentPatch.Slept(this);}}
public class Thing:Card {public bool isDestroyed;public DNA? c_DNA;public int c_equippedSlot;public Category category=new();}
public class Category {public int slot=46;}
public class DNA {public int cost=5,power=3;public int Applies,Removes;public void Apply(Chara actor,bool reverse){if(reverse)Removes++;else Applies++;actor.Power+=reverse?-power:power;}}
public class BodySlot {public int elementId,index;public Thing? thing;}
public class CharaBody(Chara owner) {
 public Chara owner=owner;public List<BodySlot> slots=[];public int Equips,Unequips;public bool ThrowEquip;
 public BodySlot? GetSlot(int id,bool onlyEmpty=true)=>slots.FirstOrDefault(s=>s.elementId==id&&(!onlyEmpty||s.thing==null));
 public void AddBodyPart(int id){slots.Add(new(){elementId=id,index=slots.Count});}
 public void RefreshBodyParts(){for(int i=0;i<slots.Count;i++){slots[i].index=i;if(slots[i].thing is{} t)t.c_equippedSlot=i+1;}}
 public bool IsEquippable(Thing thing,BodySlot slot)=>!(slot.thing?.GetBool(135)??false)&&(thing.c_DNA==null||thing.c_DNA.cost<=RelicEquipmentPatch.BudgetOwner(this).feat);
 public bool Equip(Thing thing,BodySlot? slot=null,bool msg=true){
  bool success=false;var run=RelicEquipmentPatch.BeforeEquip(this,thing,slot,out var scope);
  try {
   if(!run)return false;if(ThrowEquip)throw new InvalidOperationException("fixture native failure");
   slot??=GetSlot(thing.category.slot)??GetSlot(thing.category.slot,false);if(slot==null||!IsEquippable(thing,slot))return false;
   if(slot.thing!=null){if(slot.thing==thing){Unequip(slot);return false;}Unequip(slot);}
   thing.parent=owner;slot.thing=thing;thing.c_equippedSlot=slot.index+1;Equips++;
   if(thing.c_DNA is{} dna){dna.Apply(owner,false);owner.feat-=dna.cost;thing.SetBool(135,true);}
   success=true;return true;
  } finally {CharaEquipEvent.OnEquip(this,thing,success);RelicEquipmentPatch.End(scope);}
 }
 public void Unequip(BodySlot slot,bool refresh=true){
  RelicEquipmentPatch.BeforeUnequip(this,slot,out var scope);Thing? before=null;CharaEquipEvent.OnUnequipCapture(slot,ref before);
  try{if(slot.thing is not{} thing)return;thing.c_equippedSlot=0;slot.thing=null;Unequips++;if(thing.c_DNA is{} dna){dna.Apply(owner,true);owner.feat+=dna.cost;}}
  finally{CharaEquipEvent.OnUnequip(this,slot,before);RelicEquipmentPatch.End(scope);}
 }
}
public class AIAct {public AIAct? child;} public class AI_Equip:AIAct {}
namespace ElinTogether.Elements {public class GoalRemote:AIAct {}}
public class DramaOutcome {public void reward_sorin(){}}
public class DramaManager {public bool CheckIF(string IF)=>false;}
public class TraitSorin {public bool ShouldShowQuestIcon()=>false;}
public static class WidgetEquip {public static int Refreshes;public static void OnChangeBodyPart()=>Refreshes++;}
public static class LayerInventory {public static void SetDirty(Thing t){}}
public class ElinPreLoadAttribute:Attribute {} public class GameIOContext {}
namespace MessagePack {public class MessagePackObjectAttribute:Attribute {} public class KeyAttribute(int key):Attribute {public int Key=key;}}
namespace ElinTogether {public static class EmpLog {public static List<string> Warnings=[];public static void Warning(string m,params object[] args)=>Warnings.Add(m);}}
namespace ElinTogether.Net {
 public class ElinNetBase {public bool IsHost=>this is ElinNetHost;public bool IsClient=>this is ElinNetClient;public Deltas Delta=new();}
 public class Deltas {public List<ElinDelta> Items=[];public void AddRemote(ElinDelta d)=>Items.Add(d);}
 public class ElinNetClient:ElinNetBase {}
 public class ElinNetHost:ElinNetBase {public Dictionary<int,Chara> ActiveRemoteCharas=[];public bool Accept=true;public bool AcceptsPlayerInput(int peer)=>Accept;}
 public class NetSession {public static NetSession Instance=new();public ElinNetBase? Connection;}
}
namespace ElinTogether.Models {
 public class RemoteCard {public int Uid;public Card? Find()=>CardCache.Cards.GetValueOrDefault(Uid);public static implicit operator RemoteCard(Card c)=>new(){Uid=c.uid};}
 public static class CardCache {public static Dictionary<int,Card> Cards=[];public static bool Contains(Card c)=>Cards.ContainsKey(c.uid);}
 public static class PendingUid {public static bool IsPending(int uid)=>false;}
 public static class PlayerControl {public static bool WatchingOwnCharacter;}
 public static class SoloPlayerProfile {public const string SaveKey="profile";}
 public class ElinDelta {
  public static bool IsApplying;public int OriginPeer=1;public void Apply(ElinTogether.Net.ElinNetBase net){var previous=IsApplying;IsApplying=true;try{OnApply(net);}finally{IsApplying=previous;}}
  protected virtual void OnApply(ElinTogether.Net.ElinNetBase net){}
 }
 public class CharaFeatPointDelta:ElinDelta {public RemoteCard Owner=null!;public int Feat;}
}
