using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
public enum BlessedState { Cursed=-1,Normal=0,Blessed=1 }
public class Card { public int uid,Num=1; public bool isDestroyed,isAcidproof; public Card? parent; public Trait trait=new(); public BlessedState blessedState; public ElementContainer elements=new(); public Card GetRootCard()=>parent?.GetRootCard()??this; public Thing Split(int n)=>throw new NotImplementedException(); }
public class Thing:Card { public bool Equipped; public int encLV; public void SetBlessedState(BlessedState s)=>blessedState=s; }
public class Chara:Card { public bool IsInActiveMap=true,IsDisabled; public object? party=new object(); public int Distance=1; public int Dist(Chara c)=>Distance; }
public class Trait { public Card owner=null!; }
public class TraitDrink:Trait { public int Kind; public bool CanBlend(Thing t)=>Kind!=0||!t.isAcidproof; public void OnBlend(Thing t,Chara c){} }
public class InvOwnerBlend(Card source) { public Card owner=source; private static int next=100; public void _OnProcess(Thing target){
 var drink=(TraitDrink)owner.trait;BlendOutcomePatch.Before(drink,target,out var capture);
 try {
 if(drink.Kind==0){if(owner.blessedState==BlessedState.Cursed)target.encLV--;else target.isAcidproof=true;}
 else {
 var changed=target;
 if(target.Num>1){target.Num--;changed=new Thing{uid=next++,Num=1,parent=target.parent,blessedState=target.blessedState};CardCache.Add(changed);}
 BlendSplitPatch.After(changed);
 if(drink.Kind==1)changed.SetBlessedState(owner.blessedState);
 else changed.elements.SetBase(drink.Kind==2?702:703,5);
 }
 owner.Num--;if(owner.Num==0)owner.isDestroyed=true;
 }finally{BlendOutcomePatch.After(capture);}
} }
public class ElementContainer { private Dictionary<int,int> values=new(); public int Base(int id)=>values.GetValueOrDefault(id); public void SetBase(int id,int value)=>values[id]=value; }
public class EClass { public static Player player=new();public static Chara pc=>player.chara;public static Zone _zone=new(); }
public class Player { public Chara chara=new(); }
public class Zone { public int uid=7; }
public class GameIOContext {}
public class ElinPreLoadAttribute:Attribute {}
public static class LayerInventory {public static void SetDirty(Thing t){} }
public static class EmpLog {public static void Information(string s,params object?[] a){} public static void Debug(string s,params object?[] a){} }
namespace ElinTogether.Helper { public class Placeholder{} }
namespace ElinTogether.Models {
 public abstract class ElinDelta:EClass {public int OriginPeer=1;public static bool IsApplying;public static bool IsRemoteStateLanding=>IsApplying&&!ThingRequest.IsReplayingIntent;protected abstract void OnApply(ElinNetBase n);public void Apply(ElinNetBase n)=>OnApply(n);public static IDisposable Simulate()=>new Scope();}
 public class Scope:IDisposable{public void Dispose(){}}
 public class RemoteCard {public int Uid;public Card? Find()=>CardCache.All.GetValueOrDefault(Uid); public static implicit operator RemoteCard(Card c)=>new(){Uid=c.uid};}
 public static class CardCache {public static Dictionary<int,Thing> All=new();public static void Add(Thing t)=>All[t.uid]=t; public static bool Contains(Card t)=>All.ContainsKey(t.uid);}
 public static class PendingSplit {public static RemoteCard Split(Thing t)=>t;}
 public static class ThingRequest { public static bool IsReplayingIntent; }
 public static class PendingUid {public static bool IsPending(int uid)=>uid<0;}
 public static class PlayerControl {public static bool LocalInputBlocked;}
 public class MsgSayDelta:ElinDelta {public string Text="";public int R,G,B,A;protected override void OnApply(ElinNetBase n){} }
}
namespace ElinTogether.Net {
 public class ElinNetBase {public virtual bool IsHost=>false;public Queue Delta=new();}
 public class ElinNetHost:ElinNetBase {public override bool IsHost=>true; public bool Accept=true;public Dictionary<int,Chara> ActiveRemoteCharas=new();public bool AcceptsPlayerInput(int peer)=>Accept;public void SendDeltaTo(int p,ElinDelta d)=>Delta.AddRemote(d);}
 public class ElinNetClient:ElinNetBase{}
 public class Queue {public List<ElinDelta> Items=new();public void AddRemote(ElinDelta d)=>Items.Add(d);}
 public class NetSession {public static NetSession Instance=new();public ElinNetBase? Connection;}
}
namespace ElinTogether.Patches {
 public static class MsgRelayContext {public static IDisposable RedirectTo(Chara c)=>new Scope();}
 public static class CharaProgressCompleteEvent {public static bool Capturing;public static List<ElinDelta> Results=new();public static bool ShouldPack(bool r)=>Capturing;public static void Pack(ElinDelta d)=>Results.Add(d);}
}
namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method)]public class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string m){}} public class HarmonyPrefix:Attribute{}public class HarmonyPostfix:Attribute{}public class HarmonyFinalizer:Attribute{} }
namespace MessagePack {public class MessagePackObjectAttribute:Attribute{}public class KeyAttribute:Attribute {public KeyAttribute(int key){}}}
