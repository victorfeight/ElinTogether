public class Card { public int uid; public Dictionary<string,string?> Strings=new(); public string? GetStr(string key)=>Strings.GetValueOrDefault(key); public void SetStr(string key,string? value)=>Strings[key]=value; public Dictionary<int,int> Values=new(); public int GetInt(int key,int? defaultInt=null)=>Values.GetValueOrDefault(key,defaultInt??0); public void SetInt(int key,int value)=>Values[key]=value; }
public class Chara : Card { public Chara? master; public Point pos=new(); public bool Disguised; public Chara? FindMaster()=>master; public bool HasCondition<T>()=>Disguised; }
public class Point {public int Witnesses; public void TryWitnessCrime(Chara c)=>Witnesses++;}
public class ConIncognito {}
public class EClass { public static Player player=new(); public static Chara pc=new(); public static Game game=new(); public static Zone _zone=new(); }
public class Player {public int karma;}
public class Game {public QuestManager quests=new();}
public class QuestManager {public List<Quest> list=new();}
public class Quest {public List<int> Events=new(); public int Negative; public void OnModKarma(int a) {Events.Add(a); if(a<0) Negative+=a;} }
public class Zone {public int Refreshes; public void RefreshCriminal()=>Refreshes++;}
public static class Msg {public static List<string> Lines=new(); public static void Say(string id)=>Lines.Add(id); public static void Say(string id,string value)=>Lines.Add(id+":"+value);}
public static class Tutorial {public static void Reserve(string id) {} }
public enum ID_Achievement {NERUN}
public static class Steam {public static void GetAchievement(ID_Achievement a) {}}
public static class EmpLog {public static void Debug(string s,params object[] args) {}}
namespace ElinTogether.Net {
 public class ElinNetBase { public Queue Delta=new(); public virtual bool IsHost=>false; public bool IsClient=>!IsHost; }
 public class Queue { public List<ElinTogether.Models.ElinDelta> Sent=new(); public List<ElinTogether.Models.ElinDelta> Deferred=new(); public void AddRemote(ElinTogether.Models.ElinDelta d)=>Sent.Add(d); public void DeferLocal(ElinTogether.Models.ElinDelta d)=>Deferred.Add(d); }
 public class ElinNetClient : ElinNetBase {}
 public class ElinNetHost : ElinNetBase { public override bool IsHost=>true; public Dictionary<int,Chara> ActiveRemoteCharas=new(); public List<(int,ElinTogether.Models.PersonalKarmaDelta)> Sent=new(); public bool SendDeltaTo(int peer,ElinTogether.Models.PersonalKarmaDelta delta) {Sent.Add((peer,delta)); return true;} }
 public class NetSession {public static NetSession Instance=new(); public ElinNetBase? Connection; public Chara? Player; public bool IsHost=>Connection?.IsHost is not false;}
}
namespace ElinTogether.Models {public static class ThingRequest {public static bool IsReplayingIntent;} public class ElinDelta : EClass {public static bool IsApplying; public static bool IsRemoteStateLanding=>IsApplying && !ThingRequest.IsReplayingIntent; public int OriginPeer;protected virtual void OnApply(ElinTogether.Net.ElinNetBase net) {} public void Apply(ElinTogether.Net.ElinNetBase net)=>OnApply(net);}}
namespace MessagePack {
 [AttributeUsage(AttributeTargets.Class)] public class MessagePackObjectAttribute : Attribute {}
 [AttributeUsage(AttributeTargets.Property)] public class KeyAttribute(int n) : Attribute {public int Number {get;}=n;}
}
