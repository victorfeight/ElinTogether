using ElinTogether.Models;
namespace MessagePack {
 [AttributeUsage(AttributeTargets.Class)] public class MessagePackObjectAttribute:Attribute {}
 [AttributeUsage(AttributeTargets.Property)] public class KeyAttribute(int key):Attribute { public int Key=key; }
}
public class ElinPreLoadAttribute:Attribute {}
public class GameIOContext {}
namespace ElinTogether.Helper { internal class Placeholder {} }
public class EClass {
 public static Player player=new(); public static Chara pc=>player.chara;
 public static Zone _zone=new(){uid=7,IsPCFaction=true}; public static Game game=new();
}
public class Player {
 public Chara chara=null!; public PartySetup?[] partySetups=new PartySetup?[5];
 public class PartySetup { public List<int> uids=[];public int ride,parasite; }
}
public class Game { public Zone activeZone=>EClass._zone; public Spatials spatials=new(); public Cards cards=new(); }
public class Cards { public Dictionary<int,Chara> globalCharas=[]; }
public class Spatials { public Dictionary<int,Zone> zones=[]; public Zone? Find(int id)=>zones.GetValueOrDefault(id); }
public class Zone {
 public int uid; public string Name="home"; public bool IsPCFaction;
 public void RemoveCard(Chara c){c.parent=null;c.currentZone=null;}
 public void AddCard(Chara c,int x,int z){c.parent=this;c.currentZone=this;c.pos.Set(x,z);}
}
public enum FactionMemberType { Default,Livestock,Guest }
public class Pos { public int X,Z;public void Set(int x,int z){X=x;Z=z;}public Pos? GetNearestPoint(bool allowBlock,bool allowChara)=>new(){X=X+1,Z=Z}; }
public class Card { public bool isFav {get;set;} }
public class ConSuspend {}
public class Chara:Card {
 public int uid,CHA,Moves,RendererRefresh,SpeedRefresh; public bool isDead,IsDisabled,Human,IsPCFaction=true,IsGlobal=true,IsMultisize,Suspended,Remote,Solo,ThrowOnMove;
 public bool IsPlayer {get=>ReferenceEquals(EClass.pc,this)||(Human&&uid!=1);set=>Human=value;}
 public bool IsInActiveMap=>currentZone==EClass._zone;
 public Party party=null!; public Chara? host,ride,parasite; public Zone? homeZone,currentZone; public object? parent;
 public Pos pos=new(); public Trait trait=new(); public FactionMemberType memberType;
 public bool GetBool(string key)=>key=="remote_chara"&&Remote;
 public bool HasCondition<T>()=>Suspended;
 public void Say(string key,Chara c,string name){}
 public void MoveZone(Zone z){if(ThrowOnMove)throw new InvalidOperationException("native failure");Moves++;if(parent is Zone old)old.RemoveCard(this);z.AddCard(this,1,2);}
 public void MoveImmediate(Pos p){pos=p;}
 public void _CreateRenderer(){RendererRefresh++;}public void SyncRide(){}public void SetDirtySpeed(){SpeedRefresh++;}public void Refresh(){}
}
public class Trait { public bool CanJoinParty=true;public int RequiredCHA;public bool CanJoinPartyResident=>EClass.pc.CHA>=RequiredCHA; }
public class Party {
 public List<Chara> members=[];public List<int> uidMembers=[];
 public void AddMemeber(Chara c,bool showMsg=false){if(c.party==this)return;members.Add(c);uidMembers.Add(c.uid);c.party=this;}
 public void RemoveMember(Chara c){if(c.host is {} h){if(h.ride==c)h.ride=null;if(h.parasite==c)h.parasite=null;c.host=null;}members.Remove(c);uidMembers.Remove(c.uid);c.party=null!;}
 public void Stub_RemoveMember(Chara c)=>RemoveMember(c); public void Disband(){}
}
public static class ActRide {
 public static int Calls;
 public static void Ride(Chara actor,Chara target,bool parasite=false,bool talk=true){Calls++;if(parasite)actor.parasite=target;else actor.ride=target;target.host=actor;actor.party.AddMemeber(target);}
}
public static class WidgetRoster {public static int Updates;public static void SetDirty(){Updates++;} }
public static class Msg {public static List<string> Messages=[];public static void Say(string s){Messages.Add(s);} }
public class Live {public static implicit operator bool(Live? o)=>o!=null;}
public class ListPeopleParty {
 public bool main;public Live layer=new(),list=new();public int Refreshes;
 public void RefreshAll(bool freeze=true){Refreshes++;}
 public void OnClick(Chara c,object i){}public void JoinParty(Chara c){}
}
public class Multi {public List<ListPeopleParty> owners=[];}
public class LayerPeople {public Multi multi=new();public static LayerPeople CreateParty()=>new();}
namespace ElinTogether {
 public static class EmpLog {public static void Debug(string m,params object?[] args){}public static void Information(string m,params object?[] args){}public static void Warning(string m,params object?[] args){} }
}
namespace ElinTogether.Patches {
 public static class MsgRelayContext {public static IDisposable RedirectTo(Chara c)=>new Scope();}
 public class Scope:IDisposable {public void Dispose(){} }
}
namespace ElinTogether.Net {
 public class ElinNetBase {public bool IsHost=>this is ElinNetHost;public Buffer Delta=new();}
 public class ElinNetClient:ElinNetBase {}
 public class ElinNetHost:ElinNetBase {
  public bool Accept=true;public Dictionary<int,Chara> ActiveRemoteCharas=[];public List<ElinDelta> Sent=[];
  public bool AcceptsPlayerInput(int peer)=>Accept;public void SendDeltaTo(int peer,ElinDelta d){Sent.Add(d);}
 }
 public class Buffer {public List<ElinDelta> Sent=[],Deferred=[];public void AddRemote(ElinDelta d)=>Sent.Add(d);public void AddRemoteImmediate(ElinDelta d)=>Sent.Add(d);public void DeferLocal(ElinDelta d)=>Deferred.Add(d);}
 public class Peer {public int CharaUid;}
 public class NetSession {public static NetSession Instance=new();public ElinNetBase? Connection;public Chara? Player;public List<Peer> CurrentPlayers=[];}
}
namespace ElinTogether.Models {
 public static class SoloCompanions {public static bool IsCompanion(Chara c)=>c.Solo;}
 public class RemoteCard {
  public required int Uid {get;init;}public Chara? Value;public Chara? Find()=>Value;
  public static RemoteCard Create(Chara c,bool addToCache=false,bool withData=false)=>new(){Uid=c.uid,Value=c};
  public static implicit operator RemoteCard(Chara c)=>Create(c);
 }
 public class Position {public int X,Z;public bool IsInActiveMapBounds=>X>=0&&Z>=0&&X<100&&Z<100;public static implicit operator Position(Pos p)=>new(){X=p.X,Z=p.Z};public static implicit operator Pos(Position p)=>new(){X=p.X,Z=p.Z};public static bool operator ==(Pos p,Position q)=>p.X==q.X&&p.Z==q.Z;public static bool operator !=(Pos p,Position q)=>!(p==q);public override bool Equals(object? o)=>base.Equals(o);public override int GetHashCode()=>base.GetHashCode();}
 public abstract class ElinDelta:EClass {
  public int OriginPeer=1;public void Apply(Net.ElinNetBase net)=>OnApply(net);protected abstract void OnApply(Net.ElinNetBase net);
  protected virtual bool OnRefresh()=>true;public bool Refresh()=>OnRefresh();internal static IDisposable Simulate()=>new Patches.Scope();
 }
 public class MsgSayDelta:ElinDelta {public required string Text{get;init;}public float R,G,B,A;protected override void OnApply(Net.ElinNetBase net){} }
}
