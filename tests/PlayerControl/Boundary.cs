using ElinTogether.Models;
using ElinTogether.Net.Steam;
public class EClass {
 public static Chara pc=new(); public static object _zone=new(); public static Game game=new(); public static Core core=new(); public static Player player=new();
}
public class Core {public Game? game=EClass.game;public bool IsGameStarted=true;}
public class Game {public Player? player=new();public bool isLoading;}
public class Player {public Chara? chara=new();public Queues queues=new();}
public class Queues {public List<object> list=[];public void Remove(object q)=>list.Remove(q);}
public class Chara {public bool IsPC=>false;public int uid=7;public bool isDead;public object? host,ride,parasite,conSleep;public object currentZone=EClass._zone;public int Goals,Stops;public void RefreshFaithElement(){} public void ChooseNewGoal()=>Goals++;public void SetNoGoal()=>Stops++;public void SetAI(object ai){} }
namespace MessagePack {public class MessagePackObjectAttribute:Attribute{} public class KeyAttribute(int key):Attribute{public int Key=key;} }
namespace ElinTogether {public static class EmpLog {public static void Information(string msg,params object?[] args){} } }
namespace ElinTogether.Elements {public class GoalRemote {public static GoalRemote Default=>new();} public static class EmpPop {public static void Information(string text){} } }
namespace ElinTogether.Components {public class LayerElinTogether {public static LayerElinTogether? Instance;public void Reopen(){} } }
namespace ElinTogether.Patches {public static class ActionModeCombat {public static void ClearControlInput(){} } public static class PlayerControlPatch {public static void ChooseHostGoal()=>EClass.pc.ChooseNewGoal();} }
namespace ElinTogether.Models {
 public static class OwnerProgressionAwards {public static List<int> Pending=[];public static List<int> Read(Chara a)=>Pending;}
 public static class ShopTrade {public static void Release(Chara a){} }
 public static class WishInteraction {public static void ReleasePeer(int id){} }
 public static class DisconnectedPlayerCompanions {public static void StopActions(Chara a)=>a.SetNoGoal();public static void Dismount(Chara a){a.host=a.ride=a.parasite=null;} }
 public class WorldStateSnapshot {public static List<Snapshot> CachedRemoteSnapshots=[];}
 public class Snapshot {public Owner Owner=new();} public class Owner {public int Uid;}
 public class SessionPlayersSnapshot {public static SessionPlayersSnapshot Create()=>new();}
}
namespace ElinTogether.Net.Steam {public interface ISteamNetPeer {int Id {get;} bool Send(object message);} public class Peer:ISteamNetPeer {public int Id=>2;public List<object> Sent=[];public bool CanSend=true;public bool Send(object message){if(!CanSend)return false;Sent.Add(message);return true;} } }
namespace ElinTogether.Net {
 public class NetPeerState {public int CharaUid;public PlayerControlMode Control;}
 public class NetSession {public static NetSession Instance=new();public object? Connection;public List<NetPeerState> CurrentPlayers=[];}
 public class Buffer {public int Discards,Clears;public void DiscardIncomingPeer(int id)=>Discards++;public void ClearOut()=>Clears++;}
 internal partial class ElinNetHost:EClass {
  public Dictionary<int,Chara> ActiveRemoteCharas=[];public Dictionary<int,NetPeerState> States=[];private HashSet<int> _readyRemotePeers=[];public Buffer Delta=new();public int Probes;
  public void Broadcast(object message){} public void SendSaveProbe(Chara actor,ISteamNetPeer peer,Guid id){Probes++;_readyRemotePeers.Remove(peer.Id);}
  public void MarkReady(int id)=>_readyRemotePeers.Add(id);
  public void Request(PlayerControlRequest r,ISteamNetPeer p)=>OnPlayerControlRequest(r,p);
  public void Ready(PlayerControlReady r,ISteamNetPeer p)=>OnPlayerControlReady(r,p);
 }
 internal partial class ElinNetClient:EClass {
 public bool ZoneTransitionPending;
  public Peer Host=new();public Buffer Delta=new();public bool CanCheckpoint=true;
  public bool CheckpointPersonalProfile()=>CanCheckpoint; public void WorldStateDeltaUpdate(){}
  public void Reply(PlayerControlReply r)=>OnPlayerControlReply(r);public void Finish()=>FinishControlResume();
 }
}

namespace HarmonyLib {
 public enum MethodType { Getter }
 [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch:Attribute {public HarmonyPatch(Type type,string name,MethodType method){} }
 [AttributeUsage(AttributeTargets.Method)] public class HarmonyPrefix:Attribute {}
}
namespace ElinTogether.Models {public static class ElinDelta {public static bool IsApplying;} }
