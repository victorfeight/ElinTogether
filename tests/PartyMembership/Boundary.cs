using ElinTogether.Models;
namespace MessagePack {
 [AttributeUsage(AttributeTargets.Class)] public class MessagePackObjectAttribute:Attribute {}
 [AttributeUsage(AttributeTargets.Property)] public class KeyAttribute(int key):Attribute {}
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch(Type type,string name):Attribute {}
 [AttributeUsage(AttributeTargets.Method)] public class HarmonyPostfix:Attribute {}
}
namespace ElinTogether.Helper { internal class Placeholder {} }
public class EClass {
 public static Player player=new(); public static Chara pc=>player.chara;
 public static Zone _zone=new(){uid=7}; public static Game game=new();
}
public class Player { public Chara chara=null!; }
public class Game { public Zone activeZone=>EClass._zone; public Spatials spatials=new(); }
public class Spatials { public Dictionary<int,Zone> zones=[]; public Zone? Find(int id)=>zones.GetValueOrDefault(id); }
public class Zone { public int uid; public string Name="home"; public void RemoveCard(Chara c){c.Removed=true;c.parent=null;} public void AddCard(Chara c,int x,int z){c.parent=this;c.currentZone=this;c.pos.Set(x,z);} }
public enum FactionMemberType { Default,Livestock }
public class Pos { public int X,Z; public void Set(int x,int z){X=x;Z=z;} public Pos? GetNearestPoint(bool allowBlock,bool allowChara)=>this; }
public class Chara {
 public int uid,CHA; public bool isDead,IsDisabled,IsPlayer,isSummon,Removed,ThrowOnMove;
 public bool IsInActiveMap=>currentZone==EClass._zone; public bool HomeMember=true;
 public Party? party; public Chara? host,ride,parasite; public Zone? homeZone,currentZone; public object? parent;
 public bool GetBool(string key)=>false; public void MoveImmediate(Pos p){pos=p;}
 public Pos pos=new(); public Trait trait=new(); public FactionMemberType memberType;
 public int Distance=1; public int Dist(Chara other)=>Distance;
 public bool IsHomeMember()=>HomeMember;
 public void Say(string key,Chara c,string name){}
 public void MoveZone(Zone z){if(ThrowOnMove)throw new InvalidOperationException("move failure");currentZone=z;}
}
public class Trait {
 public bool CanJoinParty=true; public int RequiredCHA;
 public bool CanJoinPartyResident=>EClass.pc.CHA>=RequiredCHA;
}
public class Party {
 public List<Chara> members=[];
 public void AddMemeber(Chara c,bool showMsg=false){if(c.party==this)return;members.Add(c);c.party=this;}
 public void RemoveMember(Chara c){members.Remove(c);c.party=null;}
 public void Stub_RemoveMember(Chara c)=>RemoveMember(c);
}
public class DramaEvent { public string step=""; }
public class DramaEventMethod:DramaEvent { public Action action=()=>{}; }
public class DramaCustomSequence {
 public List<DramaEvent> events=[]; public string StepEnd="_end",Jump="";
 public void Build(){} public void TempGoto(string step){Jump=step;}
}
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
  public List<ElinDelta> Sent=[];
  public bool AcceptsPlayerInput(int peer)=>Accept;
  public void SendDeltaTo(int peer,ElinDelta delta){Sent.Add(delta);}
 }
 public class Buffer { public List<ElinDelta> Sent=[]; public void AddRemote(ElinDelta d)=>Sent.Add(d); }
 public class Peer {public int CharaUid;}
 public class NetSession { public static NetSession Instance=new(); public ElinNetBase? Connection; public Chara? Player;public List<Peer> CurrentPlayers=[]; }
}
namespace ElinTogether.Models {
 public class SoloCompanions { public static bool IsCompanion(Chara c)=>false; }
 public class RemoteCard(Chara c) {
  public int Uid=>c.uid; public Chara Find()=>c;
  public static implicit operator RemoteCard(Chara c)=>new(c);
 }
 public class Position { public int X,Z; public static implicit operator Position(Pos p)=>new(){X=p.X,Z=p.Z}; }
 public abstract class ElinDelta:EClass {
  public int OriginPeer=1;
  public void Apply(Net.ElinNetBase n)=>OnApply(n); protected abstract void OnApply(Net.ElinNetBase n);
  protected virtual bool OnRefresh()=>true; public bool Refresh()=>OnRefresh();
  protected static IDisposable Simulate()=>new Patches.Scope();
 }
 public class MsgSayDelta:ElinDelta {
  public required string Text{get;init;} public float R,G,B,A;
  protected override void OnApply(Net.ElinNetBase net){}
 }
}
