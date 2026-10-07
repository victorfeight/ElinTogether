using ElinTogether.Models;
using ElinTogether.Patches;
public class EClass {public static Chara pc {get;set;}=null!; public static Zone _zone=new();public static Map _map=new();public static Game game=new();public static Player player=new();public static int rnd(int n)=>0;}
public class Game {public Cards cards=new();} public class Cards {public Dictionary<int,Chara> globalCharas=[];}
public class Player {public bool deathDialog;}
public class Point {public int x,z;public bool IsValid=true;public bool IsInBounds=>IsValid;public Point Copy()=>new(){x=x,z=z,IsValid=IsValid};public Point? GetNearestPoint(bool allowBlock=false,bool allowChara=false)=>Copy();public void Set(Point p){x=p.x;z=p.z;}public void Set(int a,int b){x=a;z=b;}}
public class Map {public List<Chara> charas=[];public Point GetCenterPos()=>new();}
public class Zone {public int uid;public List<Thing> Ground=[];public bool Simulated=true;public Card AddCard(Card c,Point p){Simulated&=!ElinDelta.IsApplying;if(c is Thing moving)c.parentCard?.things.Items.Remove(moving);c.parentCard=null!;c.pos=p.Copy();if(c is Thing t)Ground.Add(t);if(c is Chara a)a.currentZone=this;return c;}}
public class Inventory {public List<Thing> Items=[];public bool? LastAccessible;public void Foreach(Action<Thing> f,bool onlyAccessible){LastAccessible=onlyAccessible;foreach(var t in Items.ToArray()){f(t);t.things.Foreach(f,onlyAccessible);}}}
public class Card {public int uid,c_lockLv;public Point pos=new();public Card parentCard=null!;public Trait trait=new();public Inventory things=new();public bool IsPC=>ReferenceEquals(this,EClass.pc);public bool IsPCFaction=true,IsPCFactionMinion;public Chara? Chara=>this as Chara;}
public class Thing:Card {public bool IsContainer,ignoreAutoPick;public int SelfWeight;}
public class Trait {public bool CanBeDropped=true;}public class TraitChestPractice:Trait {}
public class Chara:Card {
 public bool isDead,isSummon,Remote,IsPCParty=true,c_wasInPcParty;public int faction=1,WeightLimit=100,Revives,Graves;public Zone? currentZone;public object? ai;
 public bool IsPlayer=>IsPC||Remote;public bool IsActiveRemoteChara=>Remote;public bool IsInActiveMap=>currentZone==EClass._zone&&pos.IsValid;
 public Party party=new();public void SetAI(object a)=>ai=a;public void Die(){}
 public void Revive(Point? p=null,bool msg=false){bool before=false;if(!CharaReviveEvent.OnCharaRevive(this,ref before))return;if(isDead){isDead=false;Revives++;if(p!=null)EClass._zone.AddCard(this,p);}CharaReviveEvent.OnCharaReviveEnd(this,before);}
 public void GetRevived(){}public void MakeGrave(string? s){Graves++;}
}
public class Party {public void AddMemeber(Chara a,bool showMsg=false)=>a.IsPCParty=true;}
public enum EffectId {Revive,Other}public enum BlessedState {Normal}public struct ActRef {}
public class ActEffect {public static void Proc(EffectId id,int power,BlessedState state,Card cc,Card? tc,ActRef actRef){}}
public static class Msg {public static void Say(string s,Thing t){}}
public class Scene {}
public class Dialog {
 public bool Closed;public void Close()=>Closed=true;public static implicit operator bool(Dialog d)=>!d.Closed;
 public static Dialog InputName(string langDetail,string text,Action<bool,string> onClose)=>new();
 public static Dialog List<T>(string langDetail,ICollection<T> items,Func<T,string> getString,Func<int,string,bool> onSelect,bool canCancel=false)=>new();
}
public static class Lang {public static string lang(this string s)=>s;}
public class ElinPreLoadAttribute:Attribute {}public class GameIOContext {}
namespace ElinTogether {public static class EmpLog {public static void Information(string s,params object?[] a){}public static void Debug(string s,params object?[] a){}}}
namespace ElinTogether.Helper {public class Stub {}}
namespace ElinTogether.Elements {public class GoalRemote {public static object Default=new();}}
namespace MessagePack {public class MessagePackObjectAttribute:Attribute {}public class KeyAttribute(int k):Attribute {public int Key=k;}}
namespace ElinTogether.Net {
 public class ElinNetBase {public bool IsHost=>this is ElinNetHost;public bool IsClient=>this is ElinNetClient;public Deltas Delta=new();}
 public class ElinNetHost:ElinNetBase {public Dictionary<int,Chara> ActiveRemoteCharas=[];public bool Accept=true;public bool AcceptsPlayerInput(int p)=>Accept;}
 public class ElinNetClient:ElinNetBase {}
 public class Deltas {public List<ElinDelta> Items=[];public void AddRemote(ElinDelta d)=>Items.Add(d);}
 public class NetSession {public static NetSession Instance=new();public ElinNetBase? Connection;}
}
namespace ElinTogether.Models {
 public class Position {public int X,Z;public bool IsInActiveMapBounds=true;public static implicit operator Point(Position p)=>new(){x=p.X,z=p.Z};public static implicit operator Position?(Point? p)=>p==null?null:new(){X=p.x,Z=p.z};}
 public class RemoteCard {public Card Card=null!;public Card Find()=>Card;public static implicit operator RemoteCard(Card c)=>new(){Card=c};}
 public class ElinDelta:EClass {public static bool IsApplying;public int OriginPeer=1;protected virtual void OnApply(ElinTogether.Net.ElinNetBase net){}public void Apply(ElinTogether.Net.ElinNetBase n){var b=IsApplying;IsApplying=true;try{OnApply(n);}finally{IsApplying=b;}}public static IDisposable Simulate()=>new Scope();private class Scope:IDisposable{readonly bool before=IsApplying;public Scope(){IsApplying=false;}public void Dispose()=>IsApplying=before;}}
}
