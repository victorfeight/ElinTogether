using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
public class EClass {
 public static Map _map=new();public static Zone _zone=new(){uid=7};public static Player player=new();public static Chara pc=>player.chara;public static Screen screen=new();
}
public class Player{public Chara chara=null!;public bool haltMove;public object? returnInfo;}
public class Screen{public int Focus;public void FocusPC()=>Focus++;public void RefreshPosition(){}}
public class Zone{public int uid;}
public class Card{public int uid,Num=1;public string id="",c_idRefCard="";public bool isDestroyed;public enum MoveType{Force}public enum MoveResult{Success,Fail}}
public class Chara:Card {
 public bool isDead,IsDisabled,IsInActiveMap=true;public bool IsPC=>ReferenceEquals(this,EClass.pc);
 public int c_uidMaster;public string NameSimple=>id;public Point pos=new(1,1);public AI ai=new();public Renderer renderer=new();public Elements elements=new();
}
public class AI{public int Cancels;public void Cancel()=>Cancels++;}
public class Renderer{public void SetFirst(bool first,Point p){}}
public class Point(int x,int z){public int x=x,z=z;public Point Copy()=>new(x,z);public void Set(Point p){x=p.x;z=p.z;}public Point PositionCenter()=>this;public override bool Equals(object? o)=>o is Point p&&p.x==x&&p.z==z;public override int GetHashCode()=>HashCode.Combine(x,z);}
public class Map{public int Size=10;public List<Chara> charas=[];public List<Cell> cells=[];public void ForeachCell(Action<Cell> a){foreach(var c in cells)a(c);}}
public class Cell{public int x,z;public bool isSeen,HasObj;public object? FirstThing;}
public class Elements{public Dictionary<int,Element> dict=[];public Element? GetElement(int id)=>dict.GetValueOrDefault(id);}
public class Element{public int Value=1;public Act? act;}
public class Source{public string alias="";}
public class Act {
 public int id;public Source source=new();public static Chara CC=null!;public static Card? TC;public static Point TP=new(0,0);public static int powerMod=100;public static bool forcePt;public static Act? CurrentAct;
 public static Action<Act>? Native;public static int Executions,Costs;public static bool CanPerform=true;
 public bool Perform(Chara actor,Card? target,Point point){if(!CanPerform)return false;CC=actor;TC=target;TP.Set(point);CurrentAct=this;return Perform();}
 public bool Perform(){bool result=false;TpSpellBridge.Execution? scope=null;Exception? error=null;
  try{if(TpMagicAppendixPatch.Before(this,ref result,out scope)){Executions++;Native?.Invoke(this);result=true;}return result;}
  catch(Exception ex){error=ex;throw;}finally{TpMagicAppendixPatch.After(scope,error);}}
}
public class GameIOContext{}
public class ElinPreLoadAttribute:Attribute{}
public static class EmpLog{public static List<string> Lines=[];public static void Information(string s,params object[] a)=>Lines.Add(s);public static void Warning(string s,params object[] a)=>Lines.Add(s);public static void Warning(Exception e,string s)=>Lines.Add(s);public static void Debug(string s,params object[] a)=>Lines.Add(s);}
public static class EmpPop{public static void Information(string s){}}
namespace ElinTogether.Helper{public class Placeholder{}}
namespace ElinTogether.Net{
 public class Queue{public List<ElinDelta> Items=[];public void AddRemote(ElinDelta d)=>Items.Add(d);}
 public class ElinNetBase{public Queue Delta=new();}public class ElinNetClient:ElinNetBase{}
 public class ElinNetHost:ElinNetBase{public bool Accept=true;public Dictionary<int,Chara> ActiveRemoteCharas=[];public bool AcceptsPlayerInput(int id)=>Accept;public bool SendDeltaTo(int id,ElinDelta d){Delta.AddRemote(d);return true;}}
 public class NetSession{public static NetSession Instance=new();public ElinNetBase? Connection;}
}
namespace ElinTogether.Models{
 public abstract class ElinDelta:EClass{public int OriginPeer;public static bool IsRemoteStateLanding;public static int Depth;public void Apply(ElinNetBase n){Depth++;try{OnApply(n);}finally{Depth--;}}protected abstract void OnApply(ElinNetBase n);public static IDisposable Simulate(){var depth=Depth;Depth=0;return new Scope(()=>Depth=depth);}}
 public class Scope(Action end):IDisposable{public void Dispose()=>end();}
 public class RemoteCard{public int Uid;public static Dictionary<int,Card> Cards=[];public Card? Find()=>Cards.GetValueOrDefault(Uid);[return:System.Diagnostics.CodeAnalysis.NotNullIfNotNull("c")]public static implicit operator RemoteCard?(Card? c){if(c is null)return null;Cards[c.uid]=c;return new(){Uid=c.uid};}}
 public class Position{public int X,Z;public bool IsInActiveMapBounds=>X>=0&&Z>=0&&X<EClass._map.Size&&Z<EClass._map.Size;public static implicit operator Position(Point p)=>new(){X=p.x,Z=p.z};public static implicit operator Point(Position p)=>new(p.X,p.Z);}
 public static class PlayerControl{public static bool LocalInputBlocked;}
 public static class PersonalKarma{public static bool IsHuman(Chara c)=>NetSession.Instance.Connection is ElinNetHost h&&h.ActiveRemoteCharas.ContainsValue(c);}
 public static class MsgRelayContext{public static Chara? Owner;public static IDisposable RedirectTo(Chara c){var old=Owner;Owner=c;return new Scope(()=>Owner=old);}}
 public class CharaStateSnapshot{public required RemoteCard Owner;public int Master;public static CharaStateSnapshot Create(Chara c)=>new(){Owner=c,Master=c.c_uidMaster};public void ApplyReconciliation(){((Chara)Owner.Find()!).c_uidMaster=Master;}}
 public class WeatherStateSnapshot{public static int Current;public int Weather;public static WeatherStateSnapshot Create()=>new(){Weather=Current};public void Apply()=>Current=Weather;}
 public class MapRevealDelta:ElinDelta{public int ZoneUid,Size;public int[] Cells=[];protected override void OnApply(ElinNetBase n){}}
 public class MsgSayDelta:ElinDelta{public required string Text;public float R,G,B,A;protected override void OnApply(ElinNetBase n){}}
}
namespace ElinTogether.Patches{
 public static class PathTerrainSync{public static Map? Map;public static Action CaptureMap(Map map){var old=Map;Map=map;return()=>Map=old;}}
 public static class CharaMoveEvent{public static Card.MoveResult Stub_Move(this Chara c,Point p,Card.MoveType t){c.pos.Set(p);return Card.MoveResult.Success;}}
}
namespace TpMagicAppendix{public class MagicAppendix{}}
namespace HarmonyLib{
 public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string n,Type[] a){}}
 public class HarmonyPrefix:Attribute{}public class HarmonyFinalizer:Attribute{}
 public class HarmonyPriority(int p):Attribute{public int Value=p;}public class HarmonyBefore(string owner):Attribute{public string Owner=owner;}
 public static class Priority{public const int First=800;}
 public static class AccessTools{public static bool Available=true;public static Type? TypeByName(string n)=>Available?typeof(TpMagicAppendix.MagicAppendix):null;}
}
namespace MessagePack{public class MessagePackObjectAttribute:Attribute{}public class KeyAttribute(int key):Attribute{public int Key=key;}}
