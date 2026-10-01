using ElinTogether.Models;
using ElinTogether.Net;
public static class Trace { public static List<string> Events=[]; }
public class EClass { public static Zone _zone=new(); }
public class Zone { public int uid=1; }
public class Card { public int LV,exp,feat;public void AddExp(){}public void LevelUp(){}public int uid,Num=1; public bool isDestroyed; public object? parent; public Trait trait=new(); public Card GetRootCard()=>parent is Card c?c.GetRootCard():this; }
public class Thing:Card {}
public class Chara:Card { public bool IsPC,IsRemotePlayer; public Card? held; public Position pos=new(); public void HoldCard(Card c){Trace.Events.Add("hold");held=c;} }
public class Trait { public Recipe GetRecipe()=>new(); }
public class Recipe { public int _dir; }
public class AIAct {}
public class AI_Eat:AIAct { public Thing? target; public bool cook; }
public class TaskBuild { public Chara owner=null!;public Card held=null!;public Recipe recipe=new();public Position pos=new(); public int dir,altitude,bridgeHeight;public bool useHeld=true;public Card? target;public static bool Throw; public static Action<TaskBuild>? Build;
 public void OnProgressComplete(){Trace.Events.Add("build");if(Throw)throw new Exception("native failure");Build?.Invoke(this);}
}
public struct Color {public float r,g,b,a;}
public static class Msg {public static Color currentColor;public static bool ignoreAll,alwaysVisible;public static void SetColor()=>currentColor=default;public static void SayRaw(){} }
public static class EmpLog {public static void Debug(string s,params object?[] a){}public static void Warning(string s,params object?[] a){}public static void Error(Exception e,string s,params object?[] a){} }
namespace MessagePack { public class MessagePackObjectAttribute:Attribute{}public class KeyAttribute(int key):Attribute{public int Key=key;} }
namespace HarmonyLib { [AttributeUsage(AttributeTargets.All,AllowMultiple=true)] public class HarmonyPatch:Attribute{public HarmonyPatch(){}public HarmonyPatch(Type t,string n){}public HarmonyPatch(Type t,string n,MethodType m){}}public class HarmonyPrefix:Attribute{}public class HarmonyPostfix:Attribute{}public enum MethodType{Setter}public static class AccessTools {public static System.Reflection.MethodInfo PropertySetter(Type t,string n)=>typeof(object).GetMethod("ToString")!;} }
namespace ElinTogether.Helper {}
namespace ElinTogether {
 public class ScopeExit:IDisposable {public Action? OnExit;public void Dispose()=>OnExit?.Invoke();}
 public static class EmpConfig {public static class Server {public static Entry AllowMoongateTheft=new(),SharedAverageSpeed=new(),TurnBasedCombat=new();}public class Entry {public bool Value;}}
}
namespace ElinTogether.Net.Steam {public interface ISteamNetPeer {}public class Peer:ISteamNetPeer{}}
namespace ElinTogether.Net {
 public class NetSession {public static NetSession Instance=new();public ElinNetBase? Connection;}
 public class ElinNetBase {public bool IsHost;public bool IsClient=>!IsHost;public DeltaManager Delta=new();public void ReportDesync(string s)=>Trace.Events.Add("desync");}
 public class DeltaManager {public List<ElinDelta> Sent=[];public void AddRemote(ElinDelta d){Sent.Add(d);Trace.Events.Add("send");}}
 public class ElinNetHost:ElinNetBase {public ElinNetHost(){IsHost=true;}public Dictionary<int,Chara> ActiveRemoteCharas=[];public bool Deliver=true;public List<(int Peer,MsgSayDelta Message)> Messages=[];public bool SendDeltaTo(int peer,MsgSayDelta d){if(!Deliver)return false;Messages.Add((peer,d));return true;}}
 internal partial class ElinNetClient:ElinNetBase {public Steam.ISteamNetPeer Host=new Steam.Peer();public bool Accept(object o,Steam.ISteamNetPeer? peer=null)=>ShouldReceiveHostPacket(o,peer??Host);public void Phase(NetHandshakePhase p)=>AdvanceHandshake(p);}
 public enum NetHandshakePhase{AwaitingVersion,AwaitingIntegrity,Joined}
 public class NetIntegrityRejected{}public class NetIntegrityRequest{}public class SourceValidationRequest{}public class SourceValidationFailed{}public class SteamLobbyRequest{}public class SessionNewPlayerRequest{}
}
namespace ElinTogether.Models {
 public class Position {public int Distance(Position p)=>0;}
 public abstract class ElinDelta:EClass {public static bool IsApplying;public static bool IsRemoteStateLanding=>IsApplying;public int OriginPeer;public void Apply(ElinNetBase n)=>OnApply(n);protected abstract void OnApply(ElinNetBase n);}
 public class RemoteCard {public Card? Value;public int Uid,Num;public Card? Find()=>Value;public static RemoteCard Create(Card c)=>new(){Value=c,Uid=c.uid,Num=c.Num};public static implicit operator RemoteCard(Card c)=>Create(c);public static implicit operator Thing?(RemoteCard? c)=>c?.Find() as Thing;}
 public class AutoActStepDelta:ElinDelta {public Guid RequestId;public RemoteCard Owner=null!;public Position Pos=null!;public int ZoneUid;public bool Reply,Success;protected override void OnApply(ElinNetBase n){} }
 public class MsgSayDelta {public string Text="";public float R,G,B,A;}
 public class SaveDataProbe{}
 public abstract class TaskArgsBase {public abstract AIAct CreateSubAct();}
 public static class PendingUid {public static bool IsPending(int uid)=>uid<0;}
 public static class CardCache {public static Dictionary<int,Card> Cards=[];public static Card? Find(int id)=>Cards.GetValueOrDefault(id);public static void DelayDestroy(Card c)=>c.isDestroyed=true;public static bool ThrowRebind;public static void Rebind(Card c,int id){if(ThrowRebind)throw new Exception("rebind failure");c.uid=id;Trace.Events.Add("rebind");}}
}
namespace ElinTogether.Patches {
 public static class RemoteHeldItem {public static bool TrySelect(Chara c,Card item){c.held=item;Trace.Events.Add("select");return true;}}
 public static class CharaProgressCompleteEvent {public static List<ElinDelta>? Into;public static ScopeExit CollectBuildSideEffects(List<ElinDelta> list){Into=list;return new(){OnExit=()=>Into=null};}}
 public static class AutoActTaskBridge {public static bool Active;public static object? FindController(Chara c,TaskBuild t)=>Active?new object():null;}
 public static class AutoActCustomActions {public static Guid BeginBuild(TaskBuild t)=>Guid.NewGuid();public static void Complete(Guid id,bool ok)=>Trace.Events.Add("ack:"+ok);}
}

namespace ElinTogether.Models {
 public class CharaLevelDelta:ElinDelta {public RemoteCard Owner=null!;public int Level,Exp;protected override void OnApply(ElinNetBase n){} }
 public class CharaFeatPointDelta:ElinDelta {public RemoteCard Owner=null!;public int Feat;protected override void OnApply(ElinNetBase n){} }
}
