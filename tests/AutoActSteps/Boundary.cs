using System.Reflection;
using ElinTogether.Models;
public class EClass {
 public static Chara pc=>game.player.chara; public static Map _map=new();public static Zone _zone=new();public static Game game=new();
 public static int rnd(int n)=>0;
}
public class Zone{public int uid=7;}
public class Game{public Player player=new();}public class Player{public Chara chara=new();}
public class GameIOContext{} public class ElinPreLoadAttribute:Attribute{}
public class Card {public bool ShouldShowMsg=>false;
 public int uid,c_charges;public bool isDestroyed;public Point pos=new(1,1);public Trait? trait; public Card? root;
 public Card GetRootCard()=>root??this;public int Area;public int Evalue(int id)=>Area;public void SetCharge(int n)=>c_charges=n;
 private readonly Dictionary<int,int> ints=[];public void SetInt(int k,int v)=>ints[k]=v;public int GetInt(int k)=>ints.GetValueOrDefault(k);
}
public class Chara:Card {
 public bool IsPC,isDead,IsInActiveMap=true,IsHumanSpeak=true,Hostile;public Card? held;public Stat stamina=new();public int interest=50,_affinity;
 public AIAct ai=new();public Affinity affinity {get{Affinity.CC=this;return new();}}
 public Renderer renderer=new();public void LookAt(Point p){}
 public int Dist(Point p)=>pos.Distance(p);public bool IsHostile(Chara c)=>Hostile;public bool HasElement(int id)=>true;
 public void PlaySound(string s){}public void Say(string s,params object?[] a){bool show=ShouldShowMsg;ElinTogether.Patches.MsgRelayContext.ShowRecipientMessage(this,ref show);if(show){string result="";if(ElinTogether.Patches.MsgRelayContext.OnSayRaw(s,ref result))Msg.Local.Add(s);}}
}
public class Renderer{public void NextFrame(){}}
public class Stat{public int value=50;public void Mod(int n)=>value+=n;}
public class Trait{public Card owner=null!;}public class TraitBroom:Trait{}public class TraitToolWaterCan:Trait{public int MaxCharge=20;}
public class TraitToolWaterPot:Trait{}
public class TraitTrap:Trait{public bool CanDisarmTrap=true;public static int Attempts;public bool TryDisarmTrap(Chara c){string result="";if(ElinTogether.Patches.MsgRelayContext.OnSayRaw("trap",ref result))Msg.Local.Add("trap");Attempts++;owner.SetInt(60,owner.GetInt(60)+1);return true;}public void ActivateTrap(Chara c){}}
public class Affinity{public static Chara? CC;public void OnTalkRumor(){CC!.interest-=10;CC._affinity++;}}
public class Point(int x,int z){public int x=x,z=z;public Point Copy()=>new(x,z);public Cell cell=>EClass._map.Cells[x,z];public int Distance(Point p)=>Math.Max(Math.Abs(x-p.x),Math.Abs(z-p.z));}
public class Cell{public bool isWatered;public byte decal=2;public CellEffect? effect;public bool HasLiquid=>effect!=null;}
public class CellEffect{public int[] ints=[];public string[] strs=[];}
public class Map{
 public Cell[,] Cells=new Cell[10,10];public Map(){for(int x=0;x<10;x++)for(int z=0;z<10;z++)Cells[x,z]=new();}
 public void SetDecal(int x,int z,int n)=>Cells[x,z].decal=(byte)n;
 public void SetLiquid(int x,int z,CellEffect? e)=>Cells[x,z].effect=e;
 public List<Point> ListPointsInSquare(Point p,int r,bool a,bool b){var list=new List<Point>();for(int x=Math.Max(0,p.x-r);x<=Math.Min(9,p.x+r);x++)for(int z=Math.Max(0,p.z-r);z<=Math.Min(9,p.z+r);z++)list.Add(new(x,z));return list;}
}
public class AIAct{public enum Status{Running,Fail,Success}public Chara owner=null!;public AIAct? parent;public Status DoGoto(Point p,int d,bool b)=>Status.Running;public void SetOwner(Chara c)=>owner=c;public virtual IEnumerable<Status> Run(){yield break;}}
public class DynamicAIAct:AIAct{public string lang="";public Point pos=null!;}
public class TaskPoint:AIAct{} public class TaskBuild:TaskPoint{}
public class TaskPourWater:AIAct{public Point pos=null!;public TraitToolWaterPot? pot;}
public static class TaskClean{public static bool CanClean(Point p)=>p.cell.decal!=0;}
public static class TaskWater{public static bool ShouldWater(Point p)=>!p.cell.isWatered;}
public static class ActDrawWater{public static bool HasWaterSource(Point p)=>p.x==2;}
namespace AutoActMod.Actions {
 public class AutoActClean{public static bool Throw;public class SubActClean:AIAct{public Point pos=null!;public override IEnumerable<Status> Run(){owner.Say("clean");if(Throw)throw new Exception("custom clean failed");pos.cell.decal=0;pos.cell.effect=null;owner.stamina.Mod(-1);yield return Status.Running;}}}
 public class AutoActWater(Point p):AIAct {
 public TraitToolWaterCan waterCan=null!;
 public class SubActWater:AIAct{public Point dest=null!;public override IEnumerable<Status> Run(){owner.Say("water_farm");dest.cell.isWatered=true;dest.cell.effect=new(){ints=[1,2],strs=["water"]};owner.held!.c_charges--;yield break;}}
 }
 public class AutoActPourWater {public class SubActPourWater:TaskPourWater{public int targetCount;}}
}
namespace MessagePack{public class MessagePackObjectAttribute:Attribute{}public class KeyAttribute(int n):Attribute{public int N=n;}}
namespace HarmonyLib{
 [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method,AllowMultiple=true)] public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string n,MethodType m){}public HarmonyPatch(){}public HarmonyPatch(Type t,string n){}}public enum MethodType{Getter}
 public class HarmonyPrefix:Attribute{}public class HarmonyPostfix:Attribute{}public class HarmonyPrepare:Attribute{}
 public static class AccessTools{public static Type? TypeByName(string n)=>typeof(AccessTools).Assembly.GetType(n);public static FieldInfo Field(Type t,string n)=>t.GetField(n)!;public static MethodInfo Method(Type t,string n)=>t.GetMethod(n)!;}
}
namespace ElinTogether.Helper{}
namespace ElinTogether {public static class EmpLog{public static void Warning(string s,params object?[] a){}}}
namespace ElinTogether.Models {
 public class Position{public int X,Z;public bool IsInActiveMapBounds=>X>=0&&Z>=0&&X<10&&Z<10;public static implicit operator Position(Point p)=>new(){X=p.x,Z=p.z};public static implicit operator Point(Position p)=>new(p.X,p.Z);}
 public class RemoteCard(Card c){public static implicit operator Card(RemoteCard r)=>r.Find();public int Uid=>c.uid;public Card Find()=>c;public static implicit operator RemoteCard?(Card? c)=>c==null?null:new(c);}
 public abstract class ElinDelta:EClass{public static bool IsApplying;
 public int OriginPeer=1;protected abstract void OnApply(ElinTogether.Net.ElinNetBase net);public void Apply(ElinTogether.Net.ElinNetBase net)=>OnApply(net);
 protected IDisposable Simulate()=>new Scope();class Scope:IDisposable{public void Dispose(){}}
 }
 public abstract class TaskArgsBase{public abstract AIAct CreateSubAct();}
}
namespace ElinTogether.Net{
 public class Buffer{public List<ElinDelta> Items=[];public void AddRemote(ElinDelta d)=>Items.Add(d);}
 public class ElinNetBase{public Buffer Delta=new();}public class ElinNetClient:ElinNetBase{}
 public class ElinNetHost:ElinNetBase{public List<(int Peer,MsgSayDelta Message)> Messages=[];public bool SendDeltaTo(int peer,MsgSayDelta d){Messages.Add((peer,d));return true;}public bool IsCompanionControlled(Chara c)=>false;public Dictionary<int,Chara> ActiveRemoteCharas=[];}
 public class NetSession{public static NetSession Instance=new();public ElinNetBase? Connection;}
}
namespace ElinTogether.Patches{public static class AutoActTaskBridge{
public static int Stops;public static bool IsController(AIAct a)=>true;
public static AIAct? FindController(Chara c,AIAct a)=>c.ai;
public static void Stop(Chara c,AIAct a){Stops++;c.ai=new();}
}}

public class TaskDig:AIAct{public enum Mode{Ground,Ore}public int id;public Point pos=null!;public Mode mode;}
public class TaskPlow:AIAct{public int id;public Point pos=null!;}
public class TaskDrawWater:AIAct{public Point pos=null!;public TraitToolWaterPot? pot;}
public static class ABILITY{public const int TaskDig=1,TaskPlow=2;}
public class AI_Read:AIAct{public Card target=null!;}
public class AI_Shear:AIAct{public Card target=null!;}
public class AI_Slaughter:AIAct{public Card target=null!;}
public class AI_Steal:AIAct{public Card target=null!;}
public class AI_OpenLock:AIAct{public Card target=null!;}
public class AI_Fuck:AIAct{public enum FuckType{fuck,tame}public virtual FuckType Type=>FuckType.fuck;public Card target=null!;}
public class AI_TendAnimal:AI_Fuck{public override FuckType Type=>FuckType.tame;}
public class AI_PracticeDummy:AIAct{}
namespace ElinTogether.Elements {public class DelegateProgress:AIAct{public Type ActType=null!;public static DelegateProgress Create(Type t)=>new(){ActType=t};}}

public struct Color {public float r,g,b,a;}
public static class Msg {public static List<string> Local=[];public static Color currentColor;public static bool ignoreAll,alwaysVisible;public static void SetColor()=>currentColor=default;public static void SayRaw(){}}
namespace ElinTogether {public class ScopeExit:IDisposable {public Action? OnExit;public void Dispose()=>OnExit?.Invoke();}}
namespace ElinTogether.Models {public class MsgSayDelta {public string Text="";public float R,G,B,A;}}
