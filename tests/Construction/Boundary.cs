using ElinTogether.Models;
namespace MessagePack {public class MessagePackObjectAttribute:Attribute{} public class KeyAttribute(int key):Attribute{public int Key=key;}}
namespace ElinTogether.Helper { }
namespace UnityEngine { public enum KeyCode {LeftControl} public static class Input {public static bool GetKey(KeyCode k)=>true;} }
namespace ElinTogether.Patches {public static class MsgRelayContext {public static IDisposable RedirectTo(Chara c)=>new Scope();} public class Scope:IDisposable {public void Dispose(){}}}
namespace ElinTogether.Net {
    public class ElinNetBase {public bool IsHost=>this is ElinNetHost;public Deltas Delta=new();}
    public class Deltas {public List<ElinDelta> Items=new();public void DeferLocal(ElinDelta d)=>Items.Add(d);public void AddRemote(ElinDelta d)=>Items.Add(d);}
    public class ElinNetClient:ElinNetBase{}
    public class ElinNetHost:ElinNetBase {public bool Accept=true;public Dictionary<int,Chara> ActiveRemoteCharas=new();public List<ElinDelta> Replies=new();public bool AcceptsPlayerInput(int peer)=>Accept;public void SendDeltaTo(int peer,ElinDelta d)=>Replies.Add(d);}
    public class NetSession {public static NetSession Instance=new();public ElinNetBase? Connection;}
}
namespace ElinTogether.Models {
    public class ElinDelta:EClass {public int OriginPeer=1;public void Apply(ElinTogether.Net.ElinNetBase net)=>OnApply(net);protected virtual void OnApply(ElinTogether.Net.ElinNetBase net){}public static IDisposable Simulate()=>new ElinTogether.Patches.Scope();}
    public class MsgSayDelta:ElinDelta {public string Text="";public float R,G,B,A;}
    public class Position {public required int X{get;init;}public required int Z{get;init;}public bool IsInActiveMapBounds=>X>=0&&Z>=0&&X<8&&Z<8;[return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(p))] public static implicit operator Position?(Point? p)=>p==null?null:new(){X=p.x,Z=p.z};[return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(p))] public static implicit operator Point?(Position? p)=>p==null?null:new(p.X,p.Z);}
    public static class BuildingPlanning {public static void Dirty(){}}
    public static class DesignationManagement {public static List<TaskDesignation[]> Batches=new();public static void Remember(int peer,TaskDesignation[] tasks){if(tasks.Length>0)Batches.Add(tasks);}}
}
public class ElinPreLoadAttribute:Attribute{}
public class GameIOContext{}
public class EClass {public static Zone _zone=new();public static Map _map=new();public static Player player=new();public static Chara pc=>player.chara;public static Scene scene=new();public static Screen screen=new();}
public class Zone {public int uid=10;public bool IsPCFactionOrTent=true;}
public class Map {
    public Store Stocked=new();public Tasks tasks=new();public Bounds bounds=new();public int PutAways,Setters;
    public Cell[,] cells=new Cell[8,8];public Map(){for(int x=0;x<8;x++)for(int z=0;z<8;z++)cells[x,z]=new();}
    public void PutAway(Card c){PutAways++;c.parent=EClass.pc;c.parentCard=EClass.pc;c.isRoofItem=false;c.isMasked=false;}
    public void SetBlock(int x,int z,int mat,int id,int dir){Setters++;cells[x,z]._blockMat=mat;cells[x,z]._block=id;cells[x,z].blockDir=dir;}
    public void SetFloor(int x,int z,int mat,int id,int dir){Setters++;cells[x,z]._floorMat=mat;cells[x,z]._floor=id;cells[x,z].floorDir=dir;}
    public void SetObj(int x,int z,int mat,int id,int value,int dir,bool ignoreRandomMat){Setters++;cells[x,z].objMat=mat;cells[x,z].obj=id;cells[x,z].objVal=value;cells[x,z].objDir=dir;}
    public void SetBridge(int x,int z,int height,int mat,int id,int dir,byte pillar){Setters++;cells[x,z]._bridgeMat=mat;cells[x,z]._bridge=id;cells[x,z].bridgeHeight=height;cells[x,z].floorDir=dir;cells[x,z].bridgePillar=pillar;}
    public void SetRoofBlock(int x,int z,int mat,int id,int dir,int height){Setters++;cells[x,z]._roofBlockMat=mat;cells[x,z]._roofBlock=id;cells[x,z]._roofBlockDir=(byte)(dir+height*4);}
    public void SetDeco(int x,int z,int mat,int id){Setters++;cells[x,z]._decoMat=mat;cells[x,z]._deco=id;}
    public void SetLiquid(int x,int z,CellEffect? e){Setters++;cells[x,z].effect=e;}
    public void RefreshNeighborTiles(int x,int z){}public void RefreshShadow(int x,int z){}public void RefreshFOV(int x,int z){}
}
public class Bounds {public bool Contains(Position p)=>p.IsInActiveMapBounds;}
public class Cell {public bool isSeen=true,isModified,isHarvested;public int gatherCount;public int _blockMat,_block,blockDir,_floorMat,_floor,floorDir,objMat,obj,objDir,objVal,_bridgeMat,_bridge,bridgeHeight,_roofBlockMat,_roofBlock,_decoMat,_deco;public byte _roofBlockDir,bridgePillar;public CellEffect? effect;}
public class CellEffect {public int[] ints=[];public string[] strs=[];}
public class Tasks {public UndoManager undo=new();public Designations designations=new();}
public class Designations {public object build=new();public MoveList moveInstalled=new();}
public class UndoManager {public class Item {public List<TaskDesignation> list=new();}public List<Item> items=new();}
public class Player {public Chara chara=new();public Chara Agent=new();public bool instaComplete;public RecipeManager recipes=new();}
public class Scene {public ActionMode actionMode=new();}
public class Screen {public BaseTileSelector tileSelector=new();}
public class Point(int x=0,int z=0) {public int x=x,z=z;public Cell cell=>EClass._map.cells[x,z];public bool IsValid=>x>=0&&z>=0&&x<8&&z<8;public IEnumerable<Card> ListCards()=>CardCache.Items.Values.Where(c=>c.pos==this);public static bool operator ==(Point? a,Point? b)=>ReferenceEquals(a,b)||a is not null&&b is not null&&a.x==b.x&&a.z==b.z;public static bool operator !=(Point? a,Point? b)=>!(a==b);public override bool Equals(object? o)=>o is Point p&&this==p;public override int GetHashCode()=>HashCode.Combine(x,z);public Point Copy()=>new(x,z);public void Set(Point p){x=p.x;z=p.z;}}
public enum PlaceState {none,installed,roaming}
public class Card {public int uid,dir,altitude;public PlaceState placeState;public bool isRoofItem,ignoreStackHeight,freePos,isPlayerCreation,isSubsetCard,isMasked;public float fx,fy;public object? parent;public bool IsInstalled=>placeState==PlaceState.installed;public Renderer? renderer=new();public Material material=new();public object sourceCard=new();public int PlacementCalls;public void Stub_SetPlacedState(PlaceState p,bool byPlayer){PlacementCalls++;placeState=p;}public void PlaySound(string s){}public Card Duplicate(int num)=>new Thing{uid=uid,trait=trait,dir=dir,altitude=altitude};public bool isDestroyed,isEquipped,ExistsOnMap;public Card? parentCard;public int c_lockLv;public Trait trait=new();public Point pos=new();public void Destroy(){isDestroyed=true;}}
public class Thing:Card {public string id="log";public int Num=5;public Thing(){uid=11;}}
public class Chara:Card {public bool IsInActiveMap=true,isDead,IsDisabled;public Store things=new();public int Money=100;}
public class Trait {public bool CanBeDropped=true,CanOnlyCarry,CanPutAway=true;}
public class Store {public Dictionary<int,Thing> Items=new();public Thing? Find(int uid)=>Items.GetValueOrDefault(uid);}
public class ActionMode {public static AM_Build Build=new();public static AM_Mine Mine=new();
    public virtual void OnActivate(){}public virtual void OnBeforeProcessTiles(){EClass._map.tasks.undo.items.Add(new());}public virtual void OnProcessTiles(Point? p,int dir){}public bool IsActive=>EClass.scene.actionMode==this;public virtual string id=>"Adv";public virtual bool IsRoofEditMode(Card? c=null)=>false;public virtual bool IsFillMode()=>false;}
public class AM_Build:ActionMode {
    public Recipe recipe=null!;public object? houseBoard;public int altitude;public object list=new();public int MaxAltitude=>63;public static bool ThrowBuild;public static int Builds;
    public void CreateNewMold(){if(recipe is RecipeCard c)c._mold=new();}
    public void SetAltitude(int value){altitude=value;}
    public void FixBridge(Point? p,Recipe r){}
    public override void OnBeforeProcessTiles(){EClass._map.tasks.undo.items.Add(new());}
    public override void OnProcessTiles(Point? p,int dir){if(ThrowBuild)throw new InvalidOperationException("native failure");Builds++;EClass._map.tasks.undo.items.Last().list.Add(new(){isDestroyed=EClass.player.instaComplete});}
    public void OnFinishProcessTiles(){}
}
public class TaskDesignation {public bool isDestroyed;public Point pos=new();public virtual void Destroy(){isDestroyed=true;}}
public class BaseTileSelector {
    public enum ProcessMode {Summary,Prpcess}
    public HitSummary summary=new();public Point? start;public Point temp=new();public bool processing,firstInMulti;
    public void ProcessTiles(Point? a,Point? b,ProcessMode mode){if(mode==ProcessMode.Summary){summary.countValid=1;summary.money=10;}else EClass.scene.actionMode.OnProcessTiles(b,-1);}
    public void RefreshMouseInfo(bool force){}
}
public class HitSummary {
    public static int Payments;public static bool Allowed=true;public Recipe? recipe;public int money,countValid;
    public void SetRecipe(Recipe? r){recipe=r;}
    public bool CanExecute()=>Allowed&&EClass.pc.Money>=money;
    public void Execute(){if(recipe!=null)ConstructionContext.RecordMaterial(recipe);Payments++;EClass.pc.Money-=money;foreach(var i in recipe?.ingredients??[])if(i.thing!=null)i.thing.Num--;}
}
public class BuildMenu {public static bool dirtyCat;public static BuildMenu Instance=null!;public Info info1=new();public static implicit operator bool(BuildMenu? m)=>m!=null;}
public class Info {public Dictionary<string,int> lastMats=new();public void Refresh(){}}
public class RecipeManager {
    public static Dictionary<string,RecipeSource> dict=new(){["wall"]=new(){id="wall"}};
    public HashSet<string> Known=new(){"wall"};public bool IsKnown(string id)=>Known.Contains(id);
}
public class RecipeSource {public string id="wall",type="Block";public bool noListing,alwaysKnown;public RenderRow row=new();}
public class RenderRow {public TileType tileType=new();public int[]? skins;}
public class TileType {public bool EditorTile,CanInstaComplete;}
public class Recipe {
    public class Ingredient {public int uid,mat;public bool optional;public Thing? thing;public bool IsValidIngredient(Thing t)=>t.id=="log";public void SetThing(Thing? t){thing=t;uid=t?.uid??0;}}
    public string id="wall";public int idSkin,_dir;public bool UseStock,IsBridge;public RecipeSource source=new();public RenderRow renderRow=>source.row;public TileType tileType=>source.row.tileType;public List<Ingredient> ingredients=new();
    public static Recipe Create(RecipeSource source)=>new(){source=source,id=source.id};
    public static Recipe Create(Thing stock){var r=new RecipeCard{UseStock=true};r.ingredients.Add(new(){uid=stock.uid,thing=stock});return r;}
    public void BuildIngredientList(){ingredients.Add(new());}
}
public class RecipeCard:Recipe {public bool freePos;public float fx,fy;public Card? _mold;}
public static class EmpLog {public static void Information(string s,params object[] args){}}

public class Material {public string GetSoundDead(object o)=>"sound";}
public class Renderer {public int Refreshes;public void RefreshSprite(){Refreshes++;}}
public static class CardCache {public static Dictionary<int,Card> Items=new();public static Card? Find(int uid)=>Items.GetValueOrDefault(uid);}
namespace ElinTogether.Models {
    public class RemoteCard {public int Uid;public Card? Find()=>CardCache.Find(Uid);public static implicit operator RemoteCard(Card c)=>new(){Uid=c.uid};}
    public enum DesignationOperation {Mine,Dig,Cut,Harvest,Cancel,Undo}
    public class DesignationCommand {public int OriginPeer=1;public DesignationOperation Operation;public bool Roof,Instant;public Position? Start,End;public int Mode,Ramp=3;}
}
public enum HitResult {Default,Valid,Warning,Invalid}
public class AM_Mine:ActionMode {public TaskMine.Mode mode;public int ramp=3;public override void OnProcessTiles(Point? p,int d)=>TerrainBoundary.Work();}
public class AM_Dig:ActionMode {public TaskDig.Mode mode;public int ramp=3;public override void OnProcessTiles(Point? p,int d)=>TerrainBoundary.Work();}
public class AM_Cut:ActionMode {public override void OnProcessTiles(Point? p,int d)=>TerrainBoundary.Work();}
public class AM_Harvest:AM_Cut{}
public class TaskMine {public enum Mode {Default,Ramp}}
public class TaskDig {public enum Mode {Default,RemoveFloor}}
public static class TerrainBoundary {public static bool Throw,Roof;public static Chara? Actor;public static int WorkCount;public static void Work(){if(Throw)throw new InvalidOperationException("native terrain failure");Actor=EClass.pc;Roof=ActionMode.Mine.IsRoofEditMode();WorkCount++;EClass._map.tasks.undo.items.Last().list.Add(new(){isDestroyed=EClass.player.instaComplete});}}
public class MoveList {public List<TaskMoveInstalled> items=new();}
public class TaskMoveInstalled:TaskDesignation {public Card? target;public int dir,altitude;public static bool Allowed=true;public HitResult GetHitResult()=>Allowed?HitResult.Valid:HitResult.Invalid;public override void Destroy(){base.Destroy();EClass._map.tasks.designations.moveInstalled.items.Remove(this);}}
public class AM_MoveInstalled:ActionMode {
    public Card? target,moldCard;public MoveList list=new();public TaskMoveInstalled mold=new();public void CreateNewMold(){mold=new(){target=target};}
    public override void OnProcessTiles(Point? p,int d){if(target==null)return;if(EClass.player.instaComplete)target.pos.Set(p!);else{var task=new TaskMoveInstalled{target=target};list.items.Add(task);EClass._map.tasks.undo.items.Last().list.Add(task);}target.dir=moldCard!.dir;}
}
public class AM_Deconstruct:ActionMode {public bool ignoreInstalled;}
