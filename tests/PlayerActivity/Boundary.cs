public class AIAct {
 public bool running=true; public virtual bool IsNoGoal=>false; public bool IsRunning=>running;
}
public class NoGoal:AIAct {public override bool IsNoGoal=>true;}
public class TaskHarvest:AIAct {}
public class AutoAct:AIAct {}
public class UnknownAct:AIAct {}
public class Chara {
 public bool isDead,IsPC,IsRemotePlayer=true,IsInActiveMap=true;
 public AIAct ai=new NoGoal(); public Party party=new();
 public bool HasNoGoal=>ai.IsNoGoal;public int Resets;
 public void SetNoGoal(){Resets++;ai=new NoGoal();}
}
public class Party {public List<Chara> members=[];}
public class EClass {public static Chara pc=new();}
public class AM_Adv {public bool ShouldPauseGame=>true;}
public class UI {public bool IsPauseGame=>true;}
public static class Game {public static bool isPaused;}
namespace HarmonyLib {
 public enum MethodType{Getter}
 [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method)] public class HarmonyPatch:Attribute{
  public HarmonyPatch(){} public HarmonyPatch(Type type,string name,MethodType kind){}
 }
 public class HarmonyPostfix:Attribute{}
}
namespace ElinTogether.Elements {public class GoalRemote:NoGoal {public AIAct? child;}}
namespace ElinTogether.API.SourceValidation {
 public class ActMappingValidator {
  public static ActMappingValidator Default=new();
  public Dictionary<Type,int> ActToIdMapping=new(){{typeof(NoGoal),0},{typeof(AutoAct),1},{typeof(TaskHarvest),2}};
  public Dictionary<int,Type> IdToActMapping=new(){{0,typeof(NoGoal)},{1,typeof(AutoAct)},{2,typeof(TaskHarvest)},{3,typeof(string)}};
 }
}
namespace ElinTogether.Net {
 public class NetPeerState{public int LastAct;}
 public class ElinNetHost {public Dictionary<int,Chara> ActiveRemoteCharas=[];public Dictionary<int,NetPeerState> States=[];}
 public class NetSession{public static NetSession Instance=new();public object? Connection;public bool HasActiveConnection=>Connection!=null;}
}
namespace ElinTogether.Patches {
 public static class ActionModeCombat {
  public enum CombatPhase {Inactive,Deciding,Executing,Advancing}
  public static CombatPhase Phase;public static bool Paused=>Phase==CombatPhase.Deciding;
 }
}
