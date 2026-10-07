public class EClass { public static Player player=new(); public static Chara pc=new(){IsPC=true}; }
public enum Hostility { Neutral }
public enum MinionType { Default }
public class Stats {public int value;}
public class Card {public enum MoveType {Force} public enum MoveResult {Success,Fail}}
public class Point {public int X,Z;public bool IsValid=true;public Point Copy()=>this;public void Set(int x,int z){X=x;Z=z;} public int Distance(object o)=>0;
 public static bool operator !=(Point p,ElinTogether.Models.Position q)=>p.X!=q.X||p.Z!=q.Z;
 public static bool operator ==(Point p,ElinTogether.Models.Position q)=>!(p!=q);
 public override bool Equals(object? o)=>ReferenceEquals(this,o);public override int GetHashCode()=>base.GetHashCode();
}
public class Map {public List<Chara> charas=[];}
public class Zone {public int uid=1;public Map map=new();public void AddCard(Chara c,object p){} }
public class Chara {
 public Dictionary<string,int> Ints=[];public int GetInt(string k,int fallback=0)=>Ints.GetValueOrDefault(k,fallback);public void SetInt(string k,int value)=>Ints[k]=value;
 public int uid,hp,c_invest,c_uidMaster;public bool IsPC,IsPlayer,IsRemotePlayer,isDead;public object? master,host;
 public Stats hunger=new(),stamina=new(),mana=new(),sleepiness=new(),SAN=new(),depression=new(),bladder=new(),hygiene=new();public Point pos=new();public Zone currentZone=new();public Hostility hostility,c_originalHostility;public MinionType c_minionType;
 public int Stub_get_Speed()=>100;public void FindMaster(){} public void Revive(object p,bool b){} public Card.MoveResult Stub_Move(object p,Card.MoveType t)=>Card.MoveResult.Success;
}
namespace MessagePack {public class MessagePackObjectAttribute:Attribute{} public class KeyAttribute(int key):Attribute{public int Key=key;}}
namespace ElinTogether.Helper {public static class CardCache {public static void Set(Chara c){} }}
namespace ElinTogether.Patches {public static class PlayerActivity {public static int ReportAct(Chara c)=>0;}}
namespace ElinTogether {public static class EmpLog {public static void Debug(string m,params object[] args){} }}
namespace ElinTogether.Net {
 public class NetSession {public static NetSession Instance=new();public object? Connection;public int Tick;public Zone CurrentZone=new();}
 public class ElinNetHost {public HashSet<Chara> Companions=[];public bool IsCompanionControlled(Chara c)=>Companions.Contains(c);}
}
namespace ElinTogether.Models {
 public class RemoteCard {public Chara Actor=null!;public int Uid=>Actor.uid;public object Find()=>Actor;public static implicit operator RemoteCard(Chara c)=>new(){Actor=c};}
 public class Position {public int X,Z;public bool IsInActiveMapBounds=false;public static implicit operator Position(Point p)=>new(){X=p.X,Z=p.Z};public static implicit operator Point(Position p)=>new(){X=p.X,Z=p.Z};}
 public static class PlayerControl {public static bool WatchingOwnCharacter;}
 public static class CharaMoveDelta {public static bool HasRecentMove(Chara c)=>false;}
}

public class Player {public int staminaRecovery;}

namespace ElinTogether.Patches {public static class CombatPresentation {public static void SnapCorrection(Chara c,Point from){} }}
