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
 public void Cancel(){Cancels++;status=Status.Fail;owner=null;child=null;}
}
public class AIProgress:AIAct { public int progress; public int MaxProgress=>7; public Action? Complete; public override void OnProgressComplete()=>Complete?.Invoke(); public virtual void OnProgressBegin(){} }
public class NoGoal:AIAct{}
public class Card { public Card GetRootCard()=>this; }
public class Chara:Card {
 public bool IsPC; public int uid=719; public AIAct ai=new NoGoal(); public Position pos=new(1,1); public Card? held; public int Resets;
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
namespace ElinTogether { public static class EmpLog {public static void Debug(string s,params object?[] a){} public static void Warning(string s,params object?[] a){} } }
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
 public class DelegateProgress:AIProgress {public static bool Represents(AIAct a,Type t)=>false;}
 public static class HeldProgress {public const int Held=-int.MaxValue;}
}
namespace ElinTogether.Models {
 public class Position(int x,int z) { public int X=x,Z=z; }
 public class RemoteCard(Chara c) {public int Uid=>c.uid;public Chara Find()=>c;public static implicit operator RemoteCard(Chara c)=>new(c);}
 public abstract class ElinDelta {
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
 public class ElinNetBase {public bool IsHost=>this is ElinNetHost;public bool IsClient=>this is ElinNetClient;public Buffer Delta=new();}
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
