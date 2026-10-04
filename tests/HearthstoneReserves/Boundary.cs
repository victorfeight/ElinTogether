using ElinTogether.Models;
using ElinTogether.Patches;
namespace MessagePack {
 [AttributeUsage(AttributeTargets.Class)] public class MessagePackObjectAttribute:Attribute {}
 [AttributeUsage(AttributeTargets.Property)] public class KeyAttribute(int key):Attribute { public int Key=key; }
}
namespace ElinTogether.Helper { internal class Placeholder {} }
public class EClass {
 public static Player player=new(); public static Chara pc=>player.chara;
 public static Zone _zone=new(){uid=7,IsPCFaction=true}; public static Game game=new();
 public static Faction Home=new(){uid="home"}; public static FactionBranch? Branch=>_zone.branch;
 public static World world=new();
}
public class World { public Date date=new(); }
public class Date { public int Now=20000; public bool IsExpired(int time)=>time<=Now; public int GetRaw()=>Now; }
public static class Msg { public static string Last=""; public static void Say(string message)=>Last=message; }
public class LayerQuestBoard {}
public class Player { public Chara chara=null!; }
public class Game { public Spatials spatials=new(); public Factions factions=new(); }
public class Spatials { public Dictionary<int,Zone> zones=[]; public Zone? Find(int id)=>zones.GetValueOrDefault(id); }
public class Factions { public Dictionary<string,Faction> dictAll=[]; }
public class Zone {
 public void UpdateQuests(bool force=false){}
 public int uid; public bool IsPCFaction; public FactionBranch? branch;
 public void RemoveCard(Chara c){c.parent=null;c.currentZone=null;}
 public void AddCard(Chara c,int x,int z){c.parent=this;c.currentZone=this;c.pos.Set(x,z);}
}
public class Faction {
 public int Capacity=10; public int GetMaxReserve()=>Capacity;
 public FactionElements charaElements=new();
 public string uid=""; public List<HireInfo> listReserve=[]; public List<FactionBranch> branches=[];
 public List<FactionBranch> GetChildren()=>branches;
 public void RemoveReserve(Chara c){ReserveManagementPatch.BeforeRemove(this,c,out var was);listReserve.RemoveAll(i=>i.chara.uid==c.uid);ReserveManagementPatch.Removed(c,was);}
 public void AddReserve(Chara c){c.party?.Stub_RemoveMember(c);foreach(var b in branches)b.members.Remove(c);if(c.parent is Zone z)z.RemoveCard(c);if(c.memberType==FactionMemberType.Livestock)c.SetInt(36,14400);listReserve.Add(new(){chara=c});ReserveManagementPatch.Added(this,c);}
}
public class FactionElements { public int Adds,Removes; public void OnAddMemeber(Chara c){Adds++;} public void OnRemoveMember(Chara c){Removes++;} }
public class FactionBranch {
 public object elements=new(); public int TypeChanges;
 public void ChangeMemberType(Chara c,FactionMemberType type){TypeChanges++;c.ClearBed();c.memberType=type;c.c_wasInPcParty=false;RefreshEfficiency();c.RefreshWorkElements(elements);ResidentMemberTypePatch.Changed(this,c);}
 public Zone owner=null!; public List<Chara> members=[]; public int uidMaid,Recruits,Efficiency; public bool Throw;
 public void Recruit(Chara c){if(Throw)throw new InvalidOperationException();Recruits++;EClass.Home.RemoveReserve(c);members.Add(c);c.homeZone=owner;c.faction=EClass.Home;c.isRestrained=false;c.IsGlobal=true;owner.AddCard(c,5,6);}
 public void RemoveRecruit(Chara c){} public void RefreshEfficiency(){Efficiency++;}
}
public class HireInfo { public Chara chara=null!; public bool isNew; public int deadline; }
public enum FactionMemberType { Default,Livestock }
public enum Hostility { Enemy,Ally }
public class Pos { public int X,Z; public void Set(int x,int z){X=x;Z=z;} }
public class Chara {
 public bool c_wasInPcParty; public int BedsCleared,WorkRefreshes;
 public bool IsPCParty=>party!=null;
 public FactionBranch? homeBranch=>homeZone?.branch;
 public void ClearBed(){BedsCleared++;}
 public Party? party; public bool Dead,Guest; public int Timer;
 public bool IsAliveInCurrentZone=>!Dead&&IsInActiveMap;
 public bool IsGuest()=>Guest;
 public bool IsHomeMember()=>faction==EClass.Home;
 public int GetInt(int key)=>Timer; public void SetInt(int key,int value){Timer=value;}
 public int uid,Banishes; public string id="npc"; public bool IsDisabled,IsPlayer,IsGlobal,isRestrained,Remote;
 public bool IsInActiveMap=>currentZone==EClass._zone;
 public Zone? homeZone,currentZone; public object? parent,enemy,orgPos; public Faction? faction;
 public Pos pos=new(); public Trait trait=new(); public FactionMemberType memberType; public Hostility hostility,c_originalHostility;
 public bool GetBool(string key)=>Remote;
 public void OnBanish(){Banishes++;currentZone=null;parent=null;}
 public void SetFaction(Faction f){faction=f;} public void SetGlobal(){IsGlobal=true;} public void RemoveGlobal(){IsGlobal=false;}
 public void RefreshWorkElements(object? elements=null){WorkRefreshes++;}
}
public class Party {public List<Chara> members=[];public void Stub_RemoveMember(Chara c){members.Remove(c);c.party=null;} }
public class Trait { public bool CanBeBanished=true; }
public class Live { public static implicit operator bool(Live? v)=>v!=null; }
public class ListUI:Live { public int Refreshes; public void List(){Refreshes++;} }
public class BaseListPeople {
 public LayerPeople layer=new(); public ListUI list=new(); public int TabRefreshes; public void RefreshTab(){TabRefreshes++;} public virtual void OnClick(Chara c,object i){}
 public class ResidentClosure { public Chara c=null!; public void Maid(){EClass.Branch!.uidMaid=c.uid;} }
 public class CooldownClosure { public ResidentClosure Outer=null!; public void Role(){Outer.c.SetInt(36,123);EClass.Branch!.ChangeMemberType(Outer.c,FactionMemberType.Livestock);} }
}
public class ListPeopleCallReserve:BaseListPeople { public override void OnClick(Chara c,object i){} }
public class Multi { public List<BaseListPeople> owners=[]; }
public class LayerPeople:Live { public Multi multi=new(); public static LayerPeople CreateReserve()=>new(); }
namespace ElinTogether {
 public static class EmpLog {
  public static void Debug(string message,params object?[] args){}
  public static void Information(string message,params object?[] args){}
  public static void Warning(string message,params object?[] args){}
 }
}
namespace ElinTogether.Patches {
 public static class MsgRelayContext { public static IDisposable RedirectTo(Chara c)=>new Scope(); }
 public class Scope:IDisposable { public void Dispose(){} }
}
namespace ElinTogether.Net {
 public class ElinNetBase { public bool IsHost=>this is ElinNetHost; public Buffer Delta=new(); }
 public class ElinNetClient:ElinNetBase {}
 public class ElinNetHost:ElinNetBase {
  public bool Accept=true; public Dictionary<int,Chara> ActiveRemoteCharas=[];
  public List<ElinDelta> Sent=[]; public bool AcceptsPlayerInput(int peer)=>Accept;
  public void SendDeltaTo(int peer,ElinDelta delta){Sent.Add(delta);}
 }
 public class Buffer { public List<ElinDelta> Sent=[],Deferred=[]; public void AddRemote(ElinDelta d)=>Sent.Add(d); public void DeferLocal(ElinDelta d)=>Deferred.Add(d); }
 public class NetSession { public static NetSession Instance=new(); public ElinNetBase? Connection; }
}
namespace ElinTogether.Models {
 internal static class PartyMembership { internal static bool Protected(Chara c)=>c.IsPlayer||c.Remote; }
 public class RemoteCard {
  public int Uid; public Chara? Value;
  public Chara? Find()=>Value;
  public static RemoteCard Create(Chara c,bool addToCache=false,bool withData=false)=>new(){Uid=c.uid,Value=c};
 }
 public class Position { public int X,Z; public static implicit operator Position(Pos p)=>new(){X=p.X,Z=p.Z}; }
 public abstract class ElinDelta:EClass {
  public int OriginPeer=1; public void Apply(Net.ElinNetBase n)=>OnApply(n); protected abstract void OnApply(Net.ElinNetBase n);
  protected virtual bool OnRefresh()=>true; public bool Refresh()=>OnRefresh();
  internal static IDisposable Simulate()=>new Scope();
 }
 public class MsgSayDelta:ElinDelta {
  public required string Text{get;init;} public float R,G,B,A;
  protected override void OnApply(Net.ElinNetBase net){}
 }
}
