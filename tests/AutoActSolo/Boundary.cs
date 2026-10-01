using System.Reflection;
using ElinTogether.Models;
using ElinTogether.Net;
public class AIAct {
 public enum Status { Running, Fail, Success }
 public AIAct? parent,child; public Chara? owner; public Status status; public int Ticks,Cancels;
 public AIAct Current=>child?.Current??this;
 public virtual void OnProgressComplete(){}
 public void Success(){status=Status.Success;owner=null;child=null;}
 public void Tick(){Ticks++; status=Status.Success;owner=null;child=null;}
 public Action? OnCancelTest;public void Cancel(){OnCancelTest?.Invoke();Cancels++;status=Status.Fail;owner=null;child=null;}
}
public class AIProgress:AIAct { public int progress; public int MaxProgress=>7; public Action? Complete; public override void OnProgressComplete()=>Complete?.Invoke(); public virtual void OnProgressBegin(){} }
public class NoGoal:AIAct{}
public class EClass {public static Zone _zone=new();}
public enum PlaceState {none,installed}
public class Zone {public int uid=1;public void AddCard(Card c,Position p){c.parent=this;c.pos=p;}}
public class Thing:Card {public int invX,invY;}
public class Card {public int uid=719,Num=1,dir;public bool isDestroyed;public object? parent;public Position pos=new(1,1);public PlaceState placeState;public Card GetRootCard()=>parent is Card c?c.GetRootCard():this; }
public class Chara:Card {
 public bool IsPC,IsRemotePlayer; public AIAct ai=new NoGoal(); public Card? held; public int Resets;
 public void SetNoGoal(){Resets++;ai=new NoGoal();NetSession.Instance.Connection?.Delta.AddRemote(new CharaTaskDelta{Owner=this});}
}
public class BaseTaskHarvest:AIAct { public enum HarvestType{Normal,Seed} public Position pos=new(2,3); public RemoteCard target=new(new Chara()); }
public class TaskHarvest:BaseTaskHarvest {public int id; public HarvestType mode;}
public class TaskMine:BaseTaskHarvest {public int id;}
public static class ABILITY { public const int TaskHarvest=1,TaskMine=2; }
namespace AutoActMod.Actions { public class AutoAct:AIAct{} public class AutoActHarvestMine:AutoAct{} public class AutoActOther:AutoAct{} }
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Method)] public class HarmonyPrepare:Attribute{}
 public static class AccessTools {public static Type? TypeByName(string name)=>null;}
 [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method)] public class HarmonyPatch:Attribute{}
 [AttributeUsage(AttributeTargets.Method)] public class HarmonyPrefix:Attribute{}
}
namespace MessagePack { public class MessagePackObjectAttribute:Attribute{} public class KeyAttribute(int key):Attribute{public int Key=key;} }
namespace ElinTogether { public static class EmpLog {public static void Debug(string s,params object?[] a){} public static void Warning(string s,params object?[] a){}public static void Error(Exception e,string s,params object?[] a){} } }
namespace ElinTogether.Helper { public static class OverrideMethodComparer {public static IEnumerable<MethodBase> FindAllOverrides(Type t,string name)=>[];} }
namespace ElinTogether.API.SourceValidation {
 public class ActMappingValidator {
 public static ActMappingValidator Default=new();
 public Dictionary<Type,int> ActToIdMapping=new(){{typeof(TaskHarvest),1},{typeof(TaskMine),2},{typeof(NoGoal),0}};
 public Dictionary<int,Type> IdToActMapping=new(){{1,typeof(TaskHarvest)},{2,typeof(TaskMine)},{0,typeof(NoGoal)}};
 }
}
namespace ElinTogether.Elements {
 public class GoalRemote:NoGoal { public void InsertAction(AIAct? a){child=a;} }
 public class DelegateProgress:AIProgress {public Type ActType=typeof(TaskHarvest);public static bool Represents(AIAct a,Type t)=>false;}
 public static class HeldProgress {public const int Held=-int.MaxValue;}
}
namespace ElinTogether.Models {
 public class Position(int x,int z) { public int X=x,Z=z; }
 public class RemoteCard(Card c) {public int Uid=>c.uid;public bool Missing;public Card? Find()=>Missing?null:c;[return:System.Diagnostics.CodeAnalysis.NotNullIfNotNull("c")]public static implicit operator RemoteCard?(Card? c)=>c is null?null:new(c);public static RemoteCard? Create(Card? c,bool withData=false)=>c is null?null:new(c);}
 public abstract class ElinDelta {
 internal static IDisposable Simulate(bool active=true)=>new ElinTogether.ScopeExit();
 public static bool IsApplying; protected virtual void OnApply(ElinNetBase net){}
 public void Apply(ElinNetBase net){var old=IsApplying;IsApplying=true;try{OnApply(net);}finally{IsApplying=old;}}
 }
 public class FakeTask:TaskArgsBase {public override AIAct CreateSubAct()=>new NoGoal();}
 public abstract class TaskArgsBase {public abstract AIAct CreateSubAct();}
 public class CharaTaskDelta:ElinDelta {public required RemoteCard Owner{get;init;} public TaskArgsBase? TaskArgs{get;init;}}
 public class CharaProgressBeginDelta:ElinDelta {public required RemoteCard Owner{get;init;} public Position Pos=null!;public int MaxProgress,ActId;}
}
namespace ElinTogether.Net {
 public class Buffer {public List<ElinDelta> Items=[];public void AddRemote(ElinDelta d)=>Items.Add(d);}
 public class ElinNetBase {public int Desyncs;public void ReportDesync(string s)=>Desyncs++;public bool IsHost=>this is ElinNetHost;public bool IsClient=>this is ElinNetClient;public Buffer Delta=new();}
 public class ElinNetClient:ElinNetBase{} public class ElinNetHost:ElinNetBase{public Dictionary<int,Chara> ActiveRemoteCharas=[]; public HashSet<Chara> Companions=[]; public bool IsCompanionControlled(Chara actor)=>Companions.Contains(actor);}
 public class NetSession {public static NetSession Instance=new();public ElinNetBase? Connection;}
}
namespace ElinTogether.Patches {
 // Mapper boundary: production serializers are linked; other vanilla task types aren't needed here.
 public static class CharaTaskRemoteEvent {
 public static TaskArgsBase CreateTaskArgs(Chara owner,AIAct task)=>task is TaskHarvest h ? TaskHarvestArgs.Create(h) : TaskMineArgs.Create((TaskMine)task);
 public static void PublishTask(Chara owner,AIAct task,TaskArgsBase args)=>NetSession.Instance.Connection!.Delta.AddRemote(new CharaTaskDelta{Owner=owner,TaskArgs=args});
 }
 public static class ReverseCancel {public static void Stub_Cancel(this AIAct a)=>a.Cancel();}
}

public class TaskBuild:AIAct {public Card? held=new(),target;}
namespace HarmonyLib {public class HarmonyPostfix:Attribute{}public class HarmonyFinalizer:Attribute{}}
namespace ElinTogether {public class ScopeExit:IDisposable {public Action? OnExit;public void Dispose()=>OnExit?.Invoke();}}
namespace ElinTogether.Models {public class CharaBuildDelta:ElinDelta {public List<ElinDelta> Results=[];public static CharaBuildDelta Create(TaskBuild t,List<ElinDelta>? deltas=null)=>new(){Results=deltas??[]};}}

namespace ElinTogether.Models {
 public class CharaPickThingDelta:ElinDelta {public RemoteCard Thing=null!;}
 public class ThingDelta:ElinDelta {public RemoteCard? Thing;}
 public class ZoneAddCardDelta:ElinDelta {public RemoteCard Card=null!;public int ZoneUid;public Position Pos=null!;protected override void OnApply(ElinNetBase n){if(Card.Find() is {} c)EClass._zone.AddCard(c,Pos);}}
 public class CardModNumDelta:ElinDelta {public RemoteCard Card=null!;public int Num;protected override void OnApply(ElinNetBase n){if(Card.Find() is {} c)c.Num=Num;}}
 public class CardAddThingDelta:ElinDelta {public RemoteCard Thing=null!,Parent=null!;public bool TryStack;public int DestInvX,DestInvY;protected override void OnApply(ElinNetBase n){if(Thing.Find() is {} c)c.parent=Parent.Find();}}
 public class CardPlacedDelta:ElinDelta {public RemoteCard Owner=null!;public PlaceState PlaceState;public int Dir;public bool ByPlayer;}
}
