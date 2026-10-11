using ElinTogether.Models;
public class EClass { public static Zone _zone = new() {uid=10, IsPCFaction=true}; public static Player player = new(); public static Chara pc => player.chara; public static World world = new(); public static Game game = new(); }
public class Player { public Chara chara = new() {uid=1}; }
public class Chara { public int uid, LastNews, Distance=1; public bool isDead, IsInActiveMap=true, IsPCParty, Guest; public FactionMemberType memberType; public Trait trait = new(); public bool IsGuest()=>Guest; public int GetInt(int id)=>LastNews; public void SetInt(int id,int value)=>LastNews=value; public int Dist(Chara c)=>Distance; }
public enum FactionMemberType { Default, Livestock }
public class Trait { public bool HaveNews=true; }
public class World { public Region region = new() {uid=5}; public Date date = new(); }
public class Date { public int GetRaw()=>50000; }
public class Zone { public int uid, x,y,icon,idPrefix,_dangerLv,dateExpire,lv; public string? name; public string Name=>name??"cave"; public string ZoneFullName=>"Zone_dungeon@-1"; public Zone? parent; public bool isKnown,isRandomSite,IsPCFaction; public static (int,string,int) ParseZoneFullName(string name)=>(0,"dungeon",-1); }
public class Region : Zone { public Elo elomap = new(); public int Creates; public bool NoSite, Fail; public Chara? GenerationActor; public Zone? CreateRandomSite(Zone origin,int radius) { Creates++; GenerationActor=EClass.pc; if(Fail)throw new InvalidOperationException(); if(NoSite)return null; var site=(Zone)SpatialGen.Create("dungeon",this,false,8,9); site.name="Warded Cave";site.idPrefix=4;site._dangerLv=28;site.isRandomSite=true;site.dateExpire=60080;site.icon=12;site.lv=-1;return site; } }
public class Elo { public int Updates; public void SetZone(int x,int z,Zone site,bool update)=>Updates++; }
public static class SpatialGen { public static object Create(string id,Zone? parent,bool register,int x,int y)=>new Zone { uid=EClass.game.spatials.uidNext++, parent=parent,x=x,y=y }; }
public class Game { public Spatials spatials = new(); }
public class Spatials { public int uidNext=100; public Dictionary<int,Zone> map=[]; public Zone? Find(int uid)=>map.GetValueOrDefault(uid); }
public static class Msg { public static List<string> Lines=[]; public static void Say(string key,string name)=>Lines.Add(name); }
public static class GameLang { public static string refDrama1=""; }
public class GameIOContext { }
public class DramaEvent { public string step=""; }
public class DramaEventMethod : DramaEvent { public Action action=()=>{}; }
public class DramaEventTalk : DramaEvent { public string text=""; public string? idJump; }
public class DramaSequence { public bool isExited; public List<DramaEvent> tempEvents=[]; }
public class DramaManager { public DramaEventTalk? lastTalk; }
public class DramaCustomSequence { public List<DramaEvent> events=[]; public string StepDefault="default"; public DramaManager manager = new(); public DramaSequence sequence = new(); public void Build(Chara c){} public void _TempTalk(string actor,string text,string? jump) { manager.lastTalk=new(){text=text,idJump=jump};sequence.tempEvents.Add(manager.lastTalk); } public string GetTopic(Chara c,string topic)=>topic+GameLang.refDrama1; public void TempGoto(string step)=>sequence.tempEvents.Clear(); }
public class LayerDrama { public static LayerDrama? Instance; public DramaManager drama = new(); public static implicit operator bool(LayerDrama layer)=>true; }
namespace MessagePack { public class MessagePackObjectAttribute:Attribute { } public class KeyAttribute(int key):Attribute { public int Key = key; } }
namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method)] public class HarmonyPatch(Type type,string method):Attribute { public Type Type=type; public string Method=method; } public class HarmonyPostfix:Attribute { } }
namespace ElinTogether.Helper { }
namespace ElinTogether.Models {
 public class ElinPreLoadAttribute:Attribute { }
 public class RemoteCard { public required Chara Card; public object Find()=>Card; public static implicit operator RemoteCard(Chara c)=>new(){Card=c}; }
 public class Position { public int X,Z; }
 public class ElinDelta : EClass { public int OriginPeer; protected virtual void OnApply(ElinTogether.Net.ElinNetBase net){} public void Apply(ElinTogether.Net.ElinNetBase net)=>OnApply(net); public static IDisposable Simulate()=>new Scope(); private class Scope:IDisposable { public void Dispose(){} } }
 public static class PlayerControl { public static bool LocalInputBlocked; }
 public static class EmpLog { public static void Information(string text,params object?[] args){} public static void Warning(string text,params object?[] args){} }
}
namespace ElinTogether.Patches { public static class SpatialGenEvent { public static Dictionary<int,Zone> HeldRefZones=[]; } }
namespace ElinTogether.Net {
 public class NetSession { public static NetSession Instance=new(); public ElinNetBase? Connection; }
 public class Queue { public List<ElinDelta> Items=[]; public void AddRemote(ElinDelta item)=>Items.Add(item); }
 public class ElinNetBase { public bool IsHost=>this is ElinNetHost; public Queue Delta=new(); }
 public class ElinNetClient:ElinNetBase { }
 public class ElinNetHost:ElinNetBase { public Dictionary<int,Chara> ActiveRemoteCharas=[]; public bool Accepts=true,Companion; public bool IsCompanionControlled(Chara c)=>Companion; public bool AcceptsPlayerInput(int peer)=>Accepts; public List<ElinDelta> Directed=[]; public void SendDeltaTo(int peer,ElinDelta delta)=>Directed.Add(delta); }
}
