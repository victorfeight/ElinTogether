public class EClass {public static int rnd(int n)=>0;public static Player player=new();public static Chara pc=>player.chara;}
public class Player {public Chara chara=new();public int staminaRecovery;}
public class BaseStats {public static Chara CC=null!;}
public class Stats {public int value,max=100;public int Calls;public void Mod(int amount){Calls++;value=Math.Min(max,value+amount);}}
public class Chara {
 public bool isDead;public bool IsPC=>ReferenceEquals(this,EClass.pc);public object? ai;private Stats _stamina=new();public Stats stamina {get {BaseStats.CC=this;return _stamina;}}
 public Dictionary<string,int> Ints=[];public int GetInt(string k,int fallback=0)=>Ints.GetValueOrDefault(k,fallback);public void SetInt(string k,int v)=>Ints[k]=v;
 public void TickConditions(){} public void OnSleep(int power,int days,bool isSunLit){} public void Revive(){}
}
namespace ElinTogether.Models {public static class PlayerControl {public static bool WatchingOwnCharacter;}}
namespace ElinTogether.Elements {public class GoalRemote {}}
namespace ElinTogether.Net {public class ElinNetBase {}public class ElinNetClient:ElinNetBase {}public class ElinNetHost:ElinNetBase {public bool Human=true;public Dictionary<int,Chara> ActiveRemoteCharas=[];public bool AcceptsPlayerInput(int peer)=>Human;}public class NetSession {public static NetSession Instance=new();public object? Connection;}}

namespace MessagePack {public class MessagePackObjectAttribute:Attribute {}public class KeyAttribute(int key):Attribute {public int Key=key;}}
namespace ElinTogether.Models {
 public class RemoteCard {public Chara Actor=null!;public Chara Find()=>Actor;public static implicit operator RemoteCard(Chara actor)=>new(){Actor=actor};}
 public class ElinDelta {public int OriginPeer=1;public void Apply(ElinTogether.Net.ElinNetBase net)=>OnApply(net);protected virtual void OnApply(ElinTogether.Net.ElinNetBase net){}}
}
