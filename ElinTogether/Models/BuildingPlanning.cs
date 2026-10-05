using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class AreaSettings
{
    [Key(0)] public string? Name { get; set; }
    [Key(1)] public int Group { get; set; }
    [Key(2)] public int Height { get; set; }
    [Key(3)] public int Access { get; set; }
    [Key(4)] public bool Walls { get; set; }
    [Key(5)] public bool Atrium { get; set; }
    public static AreaSettings Capture(BaseArea a) => new() { Name = a.data.name, Group = a.data.group,
        Height = a.data.maxHeight, Access = (int)a.data.accessType, Walls = a.data.showWallItem, Atrium = a.data.atrium };
    internal int Difference(AreaSettings b) => (Name != b.Name ? 1 : 0) | (Group != b.Group ? 2 : 0) |
        (Height != b.Height ? 4 : 0) | (Access != b.Access ? 8 : 0) | (Walls != b.Walls ? 16 : 0) | (Atrium != b.Atrium ? 32 : 0);
    internal bool Valid => (Name?.Length ?? 0) <= 200 && Group >= 0 && Group <= 4 && Height >= 0 && Height <= 9 && Access >= 0 && Access <= 2;
    internal void Write(BaseArea a, int fields = 63)
    {
        void WriteData(AreaData d) {
            if ((fields & 1) != 0) d.name = Name;
            if ((fields & 2) != 0) d.group = Group;
            if ((fields & 4) != 0) d.maxHeight = Height;
            if ((fields & 8) != 0) d.accessType = (BaseArea.AccessType)Access;
            if ((fields & 16) != 0) d.showWallItem = Walls;
            if ((fields & 32) != 0) d.atrium = Atrium;
        }
        WriteData(a.data);
        if (a.plate != null) WriteData(a.plate.areaData);
    }
}

[MessagePackObject]
public sealed class PlanningArea
{
    [Key(0)] public int Uid { get; set; }
    [Key(1)] public bool Room { get; set; }
    [Key(2)] public string Type { get; set; } = "";
    [Key(3)] public AreaSettings Settings { get; set; } = new();
    [Key(4)] public Position[] Points { get; set; } = [];
    [Key(5)] public int[] Residents { get; set; } = [];
    internal static PlanningArea Capture(BaseArea a) => new() { Uid = a.uid, Room = a is Room, Type = a.type.id,
        Settings = AreaSettings.Capture(a), Points = a.points.Select(p => (Position)p).ToArray(), Residents = a.type.uidCharas.ToArray() };
}

[MessagePackObject]
public sealed class PlanningMarker
{
    [Key(0)] public int Index { get; set; }
    [Key(1)] public bool Block { get; set; }
    [Key(2)] public bool Working { get; set; }
}

[MessagePackObject]
public sealed class BuildingPlanningState : ElinDelta
{
    [Key(0)] public int ZoneUid { get; set; }
    [Key(1)] public long Revision { get; set; }
    [Key(2)] public PlanningArea[] Areas { get; set; } = [];
    [Key(3)] public PlanningMarker[] Markers { get; set; } = [];
    [Key(4)] public int NextAreaUid { get; set; }
    protected override void OnApply(ElinNetBase net) { if (!net.IsHost) BuildingPlanning.Receive(this); }
}

[MessagePackObject]
public sealed class BuildingPlanningRequest : ElinDelta
{
    [Key(0)] public int ZoneUid { get; set; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host && ZoneUid == _zone.uid && host.ActiveRemoteCharas.ContainsKey(OriginPeer))
            host.SendDeltaTo(OriginPeer, BuildingPlanning.Capture());
    }
}

// Snapshot data contains neither executable tasks nor their item resources.
// Mutation hooks invalidate the cache; existing world snapshots advertise its
// revision so join/rejoin requests a baseline without a new polling timer.
internal sealed class BuildingPlanning : EClass
{
    private static Map? _mapOwner;
    private static bool _dirty = true;
    private static long _revision;
    private static BuildingPlanningState? _cached;
    private static long _received;
    private static long _requested = -1;
    private static int _clientZone;
    private static PlanningArea[]? _clientAreas;
    private static readonly Dictionary<int, PlanningMarker> Markers = [];
    internal static long ClientRevision => _received;
    internal static void Reset() { _mapOwner = null; _cached = null; _dirty = true; _received = 0; _requested = -1; _clientZone = 0; _clientAreas = null; Markers.Clear(); }
    private static bool SameAreas(PlanningArea[] a, PlanningArea[] b) => a.Length == b.Length && a.Zip(b, (x, y) =>
        x.Uid == y.Uid && x.Room == y.Room && x.Type == y.Type && x.Settings.Difference(y.Settings) == 0 &&
        x.Residents.SequenceEqual(y.Residents) && x.Points.Length == y.Points.Length &&
        x.Points.Zip(y.Points, (p, q) => p.X == q.X && p.Z == q.Z).All(equal => equal)).All(equal => equal);
    internal static void Dirty() { if (NetSession.Instance.Connection is ElinNetHost) _dirty = true; }
    internal static BuildingPlanningState Capture()
    {
        if (_mapOwner != _map) { _mapOwner = _map; _dirty = true; }
        if (!_dirty && _cached != null && _cached.Markers.Any(m =>
                !_map.tasks.designations.mapAll.TryGetValue(m.Index, out var t) || t.Working != m.Working)) _dirty = true;
        if (!_dirty && _cached != null) return _cached;
        _dirty = false;
        var rooms = _map.rooms;
        var next = new BuildingPlanningState { ZoneUid = _zone.uid, NextAreaUid = rooms.uidRoom,
            Areas = rooms.listArea.Cast<BaseArea>().Concat(rooms.listRoom).Where(a => a.points.Count > 0).Select(PlanningArea.Capture).ToArray(),
            Markers = _map.tasks.designations.mapAll.Select(p => new PlanningMarker { Index = p.Key, Block = p.Value.isBlock, Working = p.Value.Working }).ToArray() };
        if (_cached != null && _cached.ZoneUid == next.ZoneUid && _cached.NextAreaUid == next.NextAreaUid && SameAreas(_cached.Areas, next.Areas) &&
            _cached.Markers.Length == next.Markers.Length && _cached.Markers.Zip(next.Markers,
                (a, b) => a.Index == b.Index && a.Block == b.Block && a.Working == b.Working).All(equal => equal)) return _cached;
        next.Revision = ++_revision; _cached = next;
        return _cached;
    }
    internal static long Publish()
    {
        var previous = _cached;
        var state = Capture();
        if (previous != state && NetSession.Instance.Connection is ElinNetHost host) host.Delta.AddRemote(state);
        return state.Revision;
    }
    internal static void Observe(int zoneUid, long revision)
    {
        if (zoneUid != _zone.uid || revision <= 0 || NetSession.Instance.Connection is not ElinNetClient client) return;
        if (_clientZone == zoneUid && _received >= revision || _requested == revision) return;
        _requested = revision;
        client.Delta.AddRemote(new BuildingPlanningRequest { ZoneUid = zoneUid });
    }
    internal static bool HasState => NetSession.Instance.IsClient && _clientZone == _zone.uid && _received > 0;
    internal static void Receive(BuildingPlanningState state)
    {
        if (state.ZoneUid != _zone.uid || _clientZone == state.ZoneUid && state.Revision <= _received) return;
        if (state.Areas.Any(a => a.Points.Length == 0 || a.Points.Any(p => !p.IsInActiveMapBounds)) ||
            state.Areas.Select(a => a.Uid).Distinct().Count() != state.Areas.Length) return;
        if (_clientAreas == null || !SameAreas(_clientAreas, state.Areas)) ApplyAreas(state);
        _map.rooms.uidRoom = state.NextAreaUid;
        _clientAreas = state.Areas;
        Markers.Clear();
        foreach (var m in state.Markers) if (m.Index >= 0 && m.Index < _map.Size * _map.Size) Markers[m.Index] = m;
        _clientZone = state.ZoneUid; _received = state.Revision; _requested = -1;
    }
    private static void ApplyAreas(BuildingPlanningState state)
    {
        var rooms = _map.rooms;
        var old = rooms.listArea.Cast<BaseArea>().Concat(rooms.listRoom).ToDictionary(a => a.uid);
        // Detach geometry silently: Area.OnRemove destroys its tasks and must
        // never execute as part of applying another machine's display state.
        foreach (var a in old.Values) foreach (var p in a.points) {
            if (!p.IsValid) continue;
            if (a is Room && p.cell.room == a) p.cell.room = null;
            else if (p.detail?.area == a) p.detail.area = null;
        }
        rooms.listArea.Clear(); rooms.listRoom.Clear(); rooms.mapIDs.Clear();
        foreach (var s in state.Areas) {
            if (!old.TryGetValue(s.Uid, out var a) || (a is Room) != s.Room) a = s.Room ? new Room() : new Area();
            a.uid = s.Uid; a.points.Clear();
            foreach (var p in s.Points) {
                Point point = p;
                if (a is Room room) room.AddPoint(point); else ((Area)a).AddPoint(point);
            }
            if (a is Room r) rooms.listRoom.Add(r); else { ((Area)a).isDestroyed = false; rooms.listArea.Add((Area)a); }
            rooms.mapIDs[a.uid] = a;
        }
        rooms.uidRoom = state.NextAreaUid;
        rooms.RefreshAll();
        foreach (var s in state.Areas) {
            var a = rooms.mapIDs[s.Uid];
            if (a.type.id != s.Type) a.ChangeType(s.Type);
            a.type.owner = a; a.type.uidCharas = new(s.Residents);
            if (a.plate != null) a.plate.areaData.type = a.type;
            s.Settings.Write(a);
        }
        rooms.dirtyLots = true; rooms.Refresh();
    }
    internal static void Draw(BaseTileMap tileMap, RenderParam param)
    {
        if (!HasState || !Markers.TryGetValue(tileMap.cx + tileMap.cz * _map.Size, out var marker)) return;
        (marker.Block ? tileMap.passGuideBlock : tileMap.passGuideFloor).Add(param, marker.Working ? 20 : 19, 0f);
    }
    internal static void DrawNative(TaskDesignation task, int x, int z, RenderParam param)
    {
        if (!HasState) task.Draw(x, z, param);
    }
}
