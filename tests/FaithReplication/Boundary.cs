using System.Collections.Immutable;
using ElinTogether.Models;
using ElinTogether.Net;
public class EClass {
 public static Game game=new();public static Player player=>game.player;public static Chara pc=>player.chara;
 public static World world=new();public static Zone _zone=new();public static Map _map=new();
}
public class Game{public Player player=new();public Religions religions=new();}
public class Player{public Chara chara=new();public bool prayed;public int totalFeat,Karma;public void ModKarma(int n)=>Karma+=n;}
public class World{public Date date=new();}public class Date{public const int DayToken=1440;public int Day=10;public int GetRawDay()=>Day*DayToken;}
public class Zone{public int uid=1;}public class Map{public List<Thing> things=[];public List<Chara> charas=[];}
public class Point{}
public class Card{
 public int uid,Num=1;public bool isDestroyed;public Card? parent;public Trait trait=new();public Point pos=new();
 public bool ExistsOnMap=true;public Card GetRootCard()=>parent?.GetRootCard()??this;
}
public class Trait{}public enum BlessedState{Cursed=-1,Normal=0,Blessed=1}
public class Thing:Card{
 public BlessedState blessedState;public void SetBlessedState(BlessedState b)=>blessedState=b;
 public Thing Split(int n){if(n==Num)return this;Num-=n;return new Thing{uid=uid+1000,Num=n,blessedState=blessedState};}
 public void Destroy()=>isDestroyed=true;
}
public class Chara:Card{
 public bool IsPC=>EClass.pc==this;public bool IsRemotePlayer=>NetSession.Instance.Connection is ElinNetHost h&&h.ActiveRemoteCharas.ContainsValue(this);
 public bool isDead,IsInActiveMap=true;public int Distance=1,c_daysWithGod,hp=10,Refreshes;
 public Mana mana=new();public ElementContainer elements=new();public Religion faith=new();public Dictionary<string,string?> Saved=[];
 public int Dist(Card c)=>Distance;public bool HasElement(int id)=>Evalue(id)>0;public int Evalue(int id)=>elements.GetOrCreateElement(id).Value;
 public string? GetStr(string key)=>Saved.GetValueOrDefault(key);public void SetStr(string key,string? value)=>Saved[key]=value;
 public void RefreshFaithElement()=>Refreshes++;public Thing AddThing(Thing t,bool stack){t.parent=this;return t;}
}
public class Mana{public int value=10;}
public class Element{public int id,vBase,vExp,vPotential,vTempPotential;public int Value=>vBase;}
public class ElementContainer{
 public Dictionary<int,Element> dict=[];public List<(int Id,int Amount)> ExpCalls=[];
 public Element GetOrCreateElement(int id){if(!dict.TryGetValue(id,out var e))dict[id]=e=new Element{id=id};return e;}
 public void SetBase(int id,int n)=>GetOrCreateElement(id).vBase=n;
 public void ModExp(int id,int n){ExpCalls.Add((id,n));GetOrCreateElement(id).vBase+=n;}
}
public class Religion{
 public enum ConvertType{Default,Campaign}
 public string id="eyth",TextGodGender="god";public bool CanJoin=true;public int giftRank,mood,relation,Joins;public Source source=new();
 public void JoinFaith(Chara c,ConvertType type=ConvertType.Default){Joins++;c.faith=this;if(type!=ConvertType.Campaign)c.c_daysWithGod=0;EClass.game.religions.list[0].mood=77;}
 public int GetGiftRank()=>-1;public void Talk(string s){}
 public void OnChangeHour()=>mood++;
}
public class Source{public int relation=3;}
public class Religions{public List<Religion> list=[];public Religion Healing=>list[0];public Religion? Find(string id)=>list.Find(g=>g.id==id);}
public class TraitAltar:Trait{
 public Card owner=new();
 public bool IsBranchAltar;public Religion Deity=new();public string idDeity=>Deity.id;public Action<Chara,Thing>? Run;public int Offers,Offered;
 public void SetDeity(string id)=>Deity=EClass.game.religions.Find(id)!;
 public void OnOffer(Chara c,Thing t){Offers++;Offered+=t.Num;if(Run!=null)Run(c,t);else t.Destroy();}
}
public class Act{public static Chara? CC;public static Card? TC;public static Point? TP;}
public static class ActPray{
 public static int Calls;public static bool Throw;
 public static void TryPray(Chara c,bool passive){Calls++;if(Throw)throw new Exception("fixture failure");
  if(!EClass.player.prayed){EClass.player.prayed=true;c.hp=100;c.mana.value=100;c.faith.giftRank++;}}
}
public static class FEAT{public const int featModelBeliever=1655;}
public static class Msg{public static List<string> Lines=[];public static void Say(string s,params object[] args)=>Lines.Add(s);}
public static class LayerInventory{public static void SetDirty(Thing t){}}
public static class EmpLog{public static void Error(Exception e,string s,params object[] a){}public static void Information(string s,params object[] a){}}
public class ElinPreLoadAttribute:Attribute{}public class GameIOContext{}
namespace ElinTogether.Helper{public class Placeholder{}}
namespace ElinTogether{public class ScopeExit:IDisposable{public Action? OnExit;public void Dispose()=>OnExit?.Invoke();}}
namespace Newtonsoft.Json{public static class JsonConvert{
 static readonly System.Text.Json.JsonSerializerOptions Options=new(){IncludeFields=true};
 public static string SerializeObject(object o)=>System.Text.Json.JsonSerializer.Serialize(o,o.GetType(),Options);
 public static T? DeserializeObject<T>(string s)=>System.Text.Json.JsonSerializer.Deserialize<T>(s,Options);
}}
namespace UnityEngine{public static class Mathf{public static int Max(int a,int b)=>Math.Max(a,b);public static int Clamp(int n,int min,int max)=>Math.Clamp(n,min,max);}}
namespace MessagePack{public class MessagePackObjectAttribute:Attribute{}public class KeyAttribute(int n):Attribute{public int N=n;}}
namespace ElinTogether.Net{
 public class ElinNetBase{public bool IsHost=>this is ElinNetHost;public bool IsClient=>!IsHost;public Queue Delta=new();}
 public class ElinNetHost:ElinNetBase{public Dictionary<int,Chara> ActiveRemoteCharas=[];public List<(int Peer,ElinDelta Delta)> Replies=[];public bool SendDeltaTo(int p,ElinDelta d){Replies.Add((p,d));return true;}}
 public class ElinNetClient:ElinNetBase{}
 public class Queue{public List<ElinDelta> Items=[];public void AddRemote(ElinDelta d)=>Items.Add(d);}
 public class NetSession{public static NetSession Instance=new();public ElinNetBase? Connection;}
}
namespace ElinTogether.Models{
 public class RemoteCard(Card card){public int Uid=>card.uid;public Card Find()=>card;
  [return:System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(c))]public static implicit operator RemoteCard?(Card? c)=>c==null?null:new(c);
  public static RemoteCard Create(Card c,bool withData=false)=>new(c);
 }
 public class ElinDelta:EClass{public int OriginPeer;protected virtual void OnApply(ElinNetBase n){}public void Apply(ElinNetBase n)=>OnApply(n);public static IDisposable Simulate()=>new Scope();public class Scope:IDisposable{public void Dispose(){}}}
 public class ElementChangeDelta{public required RemoteCard Owner;public int Element;public ImmutableArray<int> Value;
  public static ElementChangeDelta Create(Chara c,global::Element e)=>new(){Owner=c,Element=e.id,Value=[e.vBase,e.vExp,e.vPotential,e.vTempPotential]};
  public void Write(Chara c){var e=c.elements.GetOrCreateElement(Element);e.vBase=Value[0];e.vExp=Value[1];e.vPotential=Value[2];e.vTempPotential=Value[3];}public void RefreshDerived(Chara c){}
 }
 public static class PendingSplit{public static RemoteCard? Split(Thing? t)=>t;}
 public static class PendingUid{public static bool IsPending(int id)=>id>=1000000;}
 public static class ThingRequest{public static Card? Origin;public static Card? GetDanglingOrigin(Thing t,int peer)=>Origin;}
 public static class PersonalKarma{public static IDisposable For(Chara c)=>new ElinDelta.Scope();}
 public static class OwnerProgressionAwards{public static Dictionary<Chara,List<int>> Pending=[];public static List<int> Read(Chara c)=>Pending.GetValueOrDefault(c)??[];}
}

namespace ElinTogether.Patches { public static class MsgRelayContext { public static ElinTogether.ScopeExit RedirectTo(Chara actor) => new(); } }
