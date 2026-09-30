using Newtonsoft.Json;
using ElinTogether.Models;
using ElinTogether.Net;
public class EClass {
 public static Game game=new(); public static Player player=>game.player;public static Chara pc=>player.chara;
 public static Core core=new();public static World world=>game.world;
 public static Sources sources=new();
}
public class Core{public bool IsGameStarted=true;}
public class World{public Date date=new();}public class Date{public int Day=20;public int GetRawDay()=>Day;}
public class Game {
 public static string id="fixture";public static string Saved="";public static int SaveCalls,LoadCalls;public static bool FailSave,FailNextLoad,SkipSelectionHook;
 public Player player=new();public Cards cards=new();public World world=new();public Religions religions=new();public bool isCloud,isLoading;
 public bool Save(bool silent=false){SaveCalls++;if(FailSave)return false;SoloCharacterSelection.CaptureCurrent();Saved=JsonConvert.SerializeObject(this,GameIOContext.Settings);return true;}
 public static void Load(string name,bool cloud){LoadCalls++;if(FailNextLoad){FailNextLoad=false;throw new IOException("fixture reload failed");}
  EClass.game=JsonConvert.DeserializeObject<Game>(Saved,GameIOContext.Settings)!;id=name;EClass.game.isCloud=cloud;
  Party.RejectLiveChanges=true;
  try{if(!SkipSelectionHook)SoloCharacterSelection.ApplyPending();}finally{Party.RejectLiveChanges=false;}
  EClass.player.chara=EClass.game.cards.globalCharas.Find(EClass.player.uidChara)!;
  PersonalFaith.RestoreJoinedPlayer();}
}
public class Cards{public CharaList globalCharas=new();}
public class CharaList:Dictionary<int,Chara>{public Chara? Find(int uid)=>this.GetValueOrDefault(uid);}
public class Player{
 public Chara chara=null!;public int uidChara,karma=30,bankMoney=900;public Zone? zone;
 public Stats stats=new();public Pref pref=new();public HotbarManager hotbars=new();
 public int totalFeat,expKnowledge,hotbarPage,safeTravel,fished,fishArtifact;
 public bool dailyGacha,wellWished,prayed;
 public Dictionary<string,int> knowledges=[],nums=[],knownCraft=[],knownSongs=[],sketches=[],favAbility=[],layerAbilityConfig=[],priorityActions=[],lastRecipes=[],trackedCategories=[],trackedCards=[],trackedElements=[];
 public string title="",memo="",memo2="";public int[] partySetups=[];public ReturnInfo? returnInfo;
 public RecipeManager recipes=new();public HashSet<int> knownBGMs=[1,8];public string WorldQuest="guild phase 12";
 public HotItemNoItem currentHotItem=new();public Thing? eqBait;public Chara? target;public int DomainsRefreshes;
 public void RefreshDomain()=>DomainsRefreshes++;
}
public class Stats{public int kills;}public class Pref{public int sort;}
public class HotbarManager{public int Slot;public bool Initialized;public List<HotItem> Items=[];public void OnCreateGame()=>Initialized=true;}
public class HotItem{}public class HotItemAbility:HotItem{public string Alias="";}public class NumLog{}public class HotItemNoItem:HotItem{}public class ReturnInfo{public int turns;}
public class RecipeManager{public HashSet<string> knownRecipes=["axe"];public HashSet<string> craftedRecipes=["axe"];}
public class Thing{public int uid,Num,invX,invY;public string id="",c_idAbility="";public bool isDestroyed;public Trait trait=new();public void Destroy()=>isDestroyed=true;}
public class Trait{}public class TraitAbility:Trait{}
public static class CardBlueprint{public static void SetNormalRarity(){}}
public static class WidgetCurrentTool{public static bool dirty;}
public static class ThingGen{public static int Next=1000;public static Thing Create(string id)=>new(){uid=Next++,id=id,Num=1,trait=new TraitAbility()};}
public class Sources{public SourceElements elements=new();}public class SourceElements{public Dictionary<string,ElementSource> alias=[];}public class ElementSource{public int id;}
public class Point{public int x,z;public void Set(Point p){x=p.x;z=p.z;}}
public class Zone{public int uid;}
public class ZoneTransition{public enum EnterState{Exact}public EnterState state;public int x,z;}
public class Global{public ZoneTransition? transition;}
public class Chara{
 public int uid,LV=10,c_daysWithGod;public string NameSimple="player",faction="home";public bool isDead,isSale,c_wasInPcParty;
 public Chara? host,ride,parasite;public Party? party;public Zone? homeZone,currentZone;
 public Global global=new();public Point pos=new();public List<Thing> things=[];public Thing? equipment;public Religion faith=new();
 public Dictionary<string,string?> Strings=[];public Dictionary<string,int> Ints=[];
 [JsonIgnore]public bool IsPC=>EClass.pc==this;
 public int GetInt(string k,int fallback=0)=>Ints.GetValueOrDefault(k,fallback);
 public int GetInt(int k,int fallback=0)=>GetInt(k.ToString(),fallback);
 public void SetInt(string k,int n)=>Ints[k]=n;public void SetInt(int k,int n)=>SetInt(k.ToString(),n);
 public bool GetBool(string k)=>GetInt(k)!=0;public void SetBool(string k,bool b)=>SetInt(k,b?1:0);public void SetBool(int k,bool b)=>SetInt(k,b?1:0);
 public string? GetStr(string k)=>Strings.GetValueOrDefault(k);public void SetStr(string k,string? s)=>Strings[k]=s;
 public void RefreshFaithElement(){}
 public HashSet<int> Elements=[];public bool HasElement(int id)=>Elements.Contains(id);public void AddThing(Thing t,bool stack,int x,int y){t.invX=x;t.invY=y;things.Add(t);}
}
public static class CINT{public const int IsPC=56;}
public class Party{
 public static bool RejectLiveChanges;
 public Chara? leader;public List<int> uidMembers=[];
 [JsonIgnore]public List<Chara>? _members;
 [JsonIgnore]public List<Chara> members=>_members??=uidMembers.Select(uid=>EClass.game.cards.globalCharas.Find(uid)!).ToList();
 public void AddMemeber(Chara c){if(RejectLiveChanges)throw new InvalidOperationException("Branch owners are not initialized during hydration");members.Add(c);uidMembers.Add(c.uid);c.party=this;}
 public void SetLeader(Chara c)=>leader=c;
 public void RemoveMember(Chara c){if(RejectLiveChanges)throw new InvalidOperationException("Branch owners are not initialized during hydration");members.Remove(c);uidMembers.Remove(c.uid);c.party=null;}
}
public class Religions{public List<Religion> list=[];}
public class Religion{public string id="earth";public int giftRank,mood,relation;public Source source=new();public void OnChangeHour()=>mood++;}
public class Source{public int relation;}
public static class CorePath{public static string PathBackup=Path.Combine(Path.GetTempPath(),"ElinTogether-solo-tests-"+Guid.NewGuid().ToString("N"));}
public class GameIndex{public string id="";public bool cloud;public GameIndex Create(Game g)=>this;}
public static class GameIO{public static int Backups;public static bool FailBackup;public static void MakeBackup(GameIndex i){if(FailBackup)throw new IOException("fixture backup failed");Backups++;}}
public static class GameIOContext{public static JsonSerializerSettings Settings=new(){PreserveReferencesHandling=PreserveReferencesHandling.Objects,TypeNameHandling=TypeNameHandling.Auto,ObjectCreationHandling=ObjectCreationHandling.Replace};}
public static class EmpLog{public static void Information(string message,params object[] args){}public static void Warning(string m,params object[] a){}public static void Debug(string m,params object[] a){}}
namespace ElinTogether.Helper{public class Placeholder{}}
namespace ElinTogether.Net.Steam{public interface ISteamNetPeer{bool IsConnected{get;}}public class FakeProfilePeer:ISteamNetPeer{public bool Connected=true;public bool IsConnected=>Connected;}}
namespace ElinTogether.Helper.Extensions{public static class Ext{public static IEnumerable<Thing> Flatten(this List<Thing> things)=>things;}}
namespace MessagePack{public class MessagePackObjectAttribute:Attribute{}public class KeyAttribute(int n):Attribute{public int N=n;}}
namespace ElinTogether.Net{
 public class ElinNetBase{}public class ElinNetHost:ElinNetBase{public Queue Delta=new();public Dictionary<int,Chara> ActiveRemoteCharas=[];}public class ElinNetClient:ElinNetBase{}
 public class Queue{public void AddRemote(object d){}}
 public class NetSession{public static NetSession Instance=new();public ElinNetBase? Connection;public bool IsClient=>Connection is ElinNetClient;}
}
namespace ElinTogether.Models{
 public class ElinDelta:EClass{public int OriginPeer;protected virtual void OnApply(ElinNetBase net){}}
 public static class CardCache{public static void UndoDestroy(Thing t){}}
 public static class FaithTransactions{public static Chara? Actor;}
 public class FaithResultDelta{public Chara? Owner;public string FaithId="",PersonalState="";}
 public static class OwnerProgressionAwards{public static int Pending;public static List<int> Read(Chara c)=>Pending==0?[]:[1];}
 public static class PersonalKarma{
  public const int ValueKey=20;public static void Initialize(Chara c,int n){if(!c.Ints.ContainsKey(ValueKey.ToString()))c.SetInt(ValueKey,n);}
  public static void Join(Chara c)=>EClass.player.karma=c.GetInt(ValueKey);
 }
 public class LZ4Bytes{public string Text="";public static LZ4Bytes Create<T>(T value)=>new(){Text=JsonConvert.SerializeObject(value,GameIOContext.Settings)};public string DecompressToString()=>Text;}
}
