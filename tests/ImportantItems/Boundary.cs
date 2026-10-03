using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
public class Card {
 public int uid;public bool isDestroyed,ExistsOnMap,IsPCFactionOrMinion,isNPCProperty;
 public Card? parent;public Dictionary<int,int> Saved=[];
 public Card GetRootCard()=>parent?.GetRootCard()??this;
 public bool c_isImportant{get=>Saved.GetValueOrDefault(109)!=0;set{CardImportantEvent.Before(this,out var old);Saved[109]=value?1:0;CardImportantEvent.After(this,old);}}
}
public class Thing:Card { }
public class Chara:Card {public bool IsPlayer=true,IsInActiveMap=true;public int Distance=1;public int Dist(Card c)=>Distance;}
public static class LayerInventory {public static List<int> Dirty=[];public static void SetDirty(Thing t)=>Dirty.Add(t.uid);}
namespace ElinTogether.Helper {public class Marker{} }
namespace ElinTogether {public static class EmpLog {public static void Information(string s,params object[] a){} } }
namespace MessagePack {public class MessagePackObjectAttribute:Attribute{}public class KeyAttribute:Attribute{public KeyAttribute(int key){}} }
namespace HarmonyLib {
 public enum MethodType{Setter}
 public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string n,MethodType m){}}
 public class HarmonyPrefix:Attribute{}public class HarmonyPostfix:Attribute{}
}
namespace ElinTogether.Models {
 public abstract class ElinDelta {
  public int OriginPeer=1;public static bool IsApplying;
  protected abstract void OnApply(ElinNetBase net);
  public void Apply(ElinNetBase net){var previous=IsApplying;IsApplying=true;try{OnApply(net);}finally{IsApplying=previous;}}
 }
 public class RemoteCard {
  public int Uid;public Card? Find()=>CardCache.Items.GetValueOrDefault(Uid);
  public static implicit operator RemoteCard(Card c)=>new(){Uid=c.uid};
 }
 public static class CardCache {public static Dictionary<int,Card> Items=[];public static bool Contains(Card c)=>Items.GetValueOrDefault(c.uid)==c;}
 public static class PendingUid {public static bool IsPending(int id)=>id>=0x40000000;}
}
namespace ElinTogether.Net {
 public class NetSession {public static NetSession Instance=new();public ElinNetBase? Connection;}
 public class DeltaQueue{public List<ElinDelta> Items=[];public void AddRemote(ElinDelta d)=>Items.Add(d);}
 public class ElinNetBase{public bool IsClient=>this is not ElinNetHost;public DeltaQueue Delta=new();}
 public class ElinNetClient:ElinNetBase{}
 public class ElinNetHost:ElinNetBase{
  public Dictionary<int,Chara> ActiveRemoteCharas=[];public bool Accept=true;public List<ElinDelta> Replies=[];
  public bool AcceptsPlayerInput(int peer)=>Accept;
  public void SendDeltaTo(int peer,ElinDelta d)=>Replies.Add(d);
 }
}
