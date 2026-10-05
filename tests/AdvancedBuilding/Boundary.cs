using ElinTogether.Models;
namespace MessagePack {
    public class MessagePackObjectAttribute : Attribute { }
    public class KeyAttribute(int key) : Attribute { public int Key = key; }
}
namespace ElinTogether.Helper { }
namespace ElinTogether.Patches {
    public static class MsgRelayContext { public static IDisposable RedirectTo(Chara c) => new Scope(); }
    public class Scope : IDisposable { public void Dispose() { } }
}
namespace ElinTogether.Net {
    public class ElinNetBase { public bool IsHost => this is ElinNetHost; public Deltas Delta = new(); }
    public class ElinNetClient : ElinNetBase { }
    public class ElinNetHost : ElinNetBase {
        public Dictionary<int, Chara> ActiveRemoteCharas = new(); public bool Accept = true; public List<ElinDelta> Replies = new();
        public bool AcceptsPlayerInput(int p) => Accept;
        public void SendDeltaTo(int p, ElinDelta d) => Replies.Add(d);
    }
    public class Deltas { public List<ElinDelta> Items = new(); public void AddRemote(ElinDelta d) => Items.Add(d); }
    public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; public bool IsClient => Connection is ElinNetClient; }
}
namespace ElinTogether.Models {
    // The native terrain boundary is exercised separately by tests/Construction.
    public static class TerrainManagement {
        public static void Execute(Chara actor, DesignationCommand command, Action<string> reject) {
            var p=(Point)command.End!;
            if(p.sourceBlock.tileType.CanInstaComplete)return;
            var task=new TaskMine();task.pos.Set(p);
            if(EClass._map.tasks.designations.mine.TryAdd(task))DesignationManagement.Remember(command.OriginPeer,[task]);
        }
    }
    public class ElinDelta : EClass { public int OriginPeer = 1; protected virtual void OnApply(ElinTogether.Net.ElinNetBase n) { } public static IDisposable Simulate() => new ElinTogether.Patches.Scope(); }
    public class MsgSayDelta : ElinDelta { public string Text = ""; public float R,G,B,A; }
    public class Position {
        public required int X { get; init; } public required int Z { get; init; }
        public bool IsInActiveMapBounds => X >= 0 && Z >= 0 && X < EClass._map.Size && Z < EClass._map.Size;
        public static implicit operator Position(Point p) => new() { X=p.x,Z=p.z };
        public static implicit operator Point(Position p) => new(p.X,p.Z);
    }
}
public class ElinPreLoadAttribute : Attribute { }
public class GameIOContext { }
public class EClass {
    public static Map _map = new(); public static Zone _zone = new(); public static Player player = new();
    public static Chara pc => player.chara; public static Sources sources = new();
}
public class Player { public Chara chara = new(); public bool instaComplete; }
public class Chara { public bool IsInActiveMap = true, isDead, IsDisabled; }
public class Zone { public int uid = 10; public bool IsPCFactionOrTent = true; }
public class Sources { public AreasSource areas = new(); }
public class AreasSource { public Dictionary<string,int> map = new() { ["public"]=1 }; }
public class Map {
    public int Size = 8; public Cell[,] cells = new Cell[8,8]; public RoomManager rooms = new(); public Tasks tasks = new();
    public Map() { for(int x=0;x<8;x++) for(int z=0;z<8;z++) cells[x,z]=new(); }
}
public class Cell { public Room? room; public Detail? detail; public bool isSeen=true; public SourceTile sourceBlock=new(),sourceFloor=new(); public Detail GetOrCreateDetail() => detail ??= new(); }
public class Detail { public Area? area; }
public class SourceTile { public TileType tileType=new(); }
public class TileType { public bool CanInstaComplete; }
public class Point(int x=0,int z=0) {
    public int x=x,z=z; public int index=>x+z*EClass._map.Size;
    public Cell cell=>EClass._map.cells[x,z]; public Detail? detail=>cell.detail;
    public bool IsValid=>x>=0&&z>=0&&x<EClass._map.Size&&z<EClass._map.Size; public bool IsInBoundsPlus=>IsValid;
    public SourceTile sourceBlock=>cell.sourceBlock; public SourceTile sourceFloor=>cell.sourceFloor;
    public void Set(Point p) { x=p.x;z=p.z; }
}
public class AreaData { public string? name; public AreaType? type; public int group,maxHeight; public BaseArea.AccessType accessType; public bool showWallItem,atrium; }
public class AreaType { public string id="public"; public BaseArea? owner; public HashSet<int> uidCharas=new(); }
public class TraitRoomPlate { public AreaData areaData=new(); }
public class BaseArea {
    public enum AccessType { Public,Resident,Private }
    public int uid; public AreaData data=new(); public AreaType type=new(); public List<Point> points=new(); public TraitRoomPlate? plate;
    public void ChangeType(string id) { type=new() { id=id,owner=this }; }
}
public class Area : BaseArea {
    public bool isDestroyed; public static int Destroyed;
    public void AddPoint(Point p) { points.Add(p); p.cell.GetOrCreateDetail().area=this; }
    public void RemovePoint(Point p) { if(points.Count<=1)return; points.RemoveAll(v=>v.index==p.index); p.cell.GetOrCreateDetail().area=null; }
    public static Area Create(string id) { var a=new Area { uid=EClass._map.rooms.uidRoom++ };a.ChangeType(id);return a; }
    public void OnRemove() { Destroyed++;isDestroyed=true;foreach(var p in points) p.cell.GetOrCreateDetail().area=null; }
}
public class Room : BaseArea { public void AddPoint(Point p) { points.Add(p);p.cell.room=this; } }
public class RoomManager {
    public List<Area> listArea=new(); public List<Room> listRoom=new(); public Dictionary<int,BaseArea> mapIDs=new(); public int uidRoom=1; public bool dirtyLots;
    public int Refreshes; public void RefreshAll() { Refreshes++; } public void Refresh() { }
    public void RemoveArea(Area a) {listArea.Remove(a);mapIDs.Remove(a.uid);a.OnRemove();}
}
public enum HitResult { Default,Valid,Warning,Invalid }
public class ActionMode {
    public bool Roof; public virtual bool IsRoofEditMode()=>Roof;
    public virtual HitResult _HitTest(Point p,Point start)=>p.detail?.area==null?HitResult.Valid:HitResult.Invalid;
    public virtual void OnProcessTiles(Point p,int dir) { }
}
public class AM_CreateArea : ActionMode {
    public Area area=new(); public override void OnProcessTiles(Point p,int dir) {
        if(!EClass._map.rooms.listArea.Contains(area)) { EClass._map.rooms.listArea.Add(area);EClass._map.rooms.mapIDs[area.uid]=area; }area.AddPoint(p);
    }
}
public class AM_ExpandArea : ActionMode { public Area area=new();public bool shrink;public override void OnProcessTiles(Point p,int d) {if(shrink)area.RemovePoint(p);else area.AddPoint(p);} }
public class AM_Mine : ActionMode {public TaskMine.Mode mode;public int ramp=3;}
public class AM_Dig : ActionMode {public TaskDig.Mode mode;public int ramp=3;}
public class AM_Cut : ActionMode { }
public class AM_Harvest : ActionMode { }
public class AM_RemoveDesignation : ActionMode { }
public class Tasks {public Designations designations=new();}
public class Designations {
    public Dictionary<int,TaskDesignation> mapAll=new(); public TaskList mine=new(),dig=new(),cut=new(),harvest=new();
    public void TryRemoveDesignation(Point p) {if(mapAll.Remove(p.index,out var t))t.Destroy();}
}
public class TaskDesignation {
    public Point pos=new(); public bool isBlock,Working,isDestroyed;public static int Destroyed;
    public virtual HitResult GetHitResult()=>HitResult.Valid;
    public void Destroy(){isDestroyed=true;Destroyed++;}
    public void Draw(int x,int z,RenderParam p){ }
}
public class TaskMine : TaskDesignation { public enum Mode {Default,Ramp} public Mode mode;public int ramp; }
public class TaskDig : TaskDesignation { public enum Mode {Default,RemoveFloor} public Mode mode;public int ramp; }
public class TaskCut : TaskDesignation { }
public class TaskHarvest : TaskDesignation { }
public class TaskList {public bool TryAdd(TaskDesignation t)=>EClass._map.tasks.designations.mapAll.TryAdd(t.pos.index,t);}
public class BaseTileMap {public int cx,cz;public Pass passGuideBlock=new(),passGuideFloor=new();}
public class Pass {public int Draws;public void Add(RenderParam p,int t,float v){Draws++;}}
public class RenderParam { }
public static class Msg {public static void Say(string s){ }}
public static class EmpLog {public static void Information(string s,params object[] args){ }}
