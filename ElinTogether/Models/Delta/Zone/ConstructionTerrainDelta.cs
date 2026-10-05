using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

public enum ConstructionTerrainKind { Block, Floor, Object, Bridge, Roof, Deco, Liquid, Pillar, Rotation, Elevation }

[MessagePackObject]
public sealed class ConstructionTerrainDelta : ElinDelta
{
    [Key(0)] public int ZoneUid { get; init; }
    [Key(1)] public required Position Pos { get; init; }
    [Key(2)] public ConstructionTerrainKind Kind { get; init; }
    [Key(3)] public int Material { get; init; }
    [Key(4)] public int Id { get; init; }
    [Key(5)] public int Direction { get; init; }
    [Key(6)] public int Value { get; init; }
    [Key(7)] public int Pillar { get; init; }
    [Key(8)] public int[]? Effect { get; init; }
    [Key(9)] public string[]? Strings { get; init; }
    [Key(10)] public int ObjectDirection { get; init; }
    [Key(11)] public bool Modified { get; init; }
    [Key(12)] public int GatherCount { get; init; }
    [Key(13)] public bool Harvested { get; init; }
    internal static ConstructionTerrainDelta Capture(Point p, ConstructionTerrainKind kind)
    {
        var c = p.cell;
        var (material, id, dir, value) = kind switch {
            ConstructionTerrainKind.Block => ((int)c._blockMat, (int)c._block, c.blockDir, 0),
            ConstructionTerrainKind.Floor => ((int)c._floorMat, (int)c._floor, c.floorDir, 0),
            ConstructionTerrainKind.Object => ((int)c.objMat, (int)c.obj, c.objDir, (int)c.objVal),
            ConstructionTerrainKind.Bridge => ((int)c._bridgeMat, (int)c._bridge, c.floorDir, (int)c.bridgeHeight),
            ConstructionTerrainKind.Roof => ((int)c._roofBlockMat, (int)c._roofBlock, c._roofBlockDir % 4, c._roofBlockDir / 4),
            ConstructionTerrainKind.Deco => ((int)c._decoMat, (int)c._deco, 0, 0),
            ConstructionTerrainKind.Rotation => (0, 0, c.blockDir, (int)c._roofBlockDir),
            // Value carries ground height; Direction carries bridge elevation.
            ConstructionTerrainKind.Elevation => (0, 0, (int)c.bridgeHeight, (int)c.height),
            _ => (0, 0, 0, 0),
        };
        return new() { ZoneUid = _zone.uid, Pos = p, Kind = kind, Material = material, Id = id, Direction = dir, Value = value, Pillar = c.bridgePillar,
            ObjectDirection = c.objDir, Modified = c.isModified, GatherCount = c.gatherCount, Harvested = c.isHarvested,
            Effect = kind == ConstructionTerrainKind.Liquid && c.effect != null ? (int[])c.effect.ints.Clone() : null,
            Strings = kind == ConstructionTerrainKind.Liquid && c.effect != null ? (string[])c.effect.strs.Clone() : null };
    }
    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || ZoneUid != _zone.uid || !Pos.IsInActiveMapBounds) return;
        var x = Pos.X; var z = Pos.Z;
        switch (Kind) {
            case ConstructionTerrainKind.Block: _map.SetBlock(x, z, Material, Id, Direction); break;
            case ConstructionTerrainKind.Floor: _map.SetFloor(x, z, Material, Id, Direction); break;
            case ConstructionTerrainKind.Object:
                _map.SetObj(x, z, Material, Id, Value, Direction, ignoreRandomMat: true);
                _map.cells[x, z].gatherCount = GatherCount; _map.cells[x, z].isHarvested = Harvested; break;
            case ConstructionTerrainKind.Bridge: _map.SetBridge(x, z, Value, Material, Id, Direction, (byte)Pillar); break;
            case ConstructionTerrainKind.Roof: _map.SetRoofBlock(x, z, Material, Id, Direction, Value); break;
            case ConstructionTerrainKind.Deco: _map.SetDeco(x, z, Material, Id); break;
            case ConstructionTerrainKind.Liquid: _map.SetLiquid(x, z, Effect == null ? null : new CellEffect { ints = Effect, strs = Strings! }); break;
            case ConstructionTerrainKind.Pillar: _map.cells[x, z].bridgePillar = (byte)Pillar; break;
            case ConstructionTerrainKind.Elevation:
                _map.cells[x, z].height = (byte)Value;
                _map.cells[x, z].bridgeHeight = (byte)Direction;
                _map.cells[x, z].room?.SetDirty();
                break;
            case ConstructionTerrainKind.Rotation:
                _map.cells[x, z].blockDir = Direction; _map.cells[x, z]._roofBlockDir = (byte)Value;
                _map.cells[x, z].objDir = ObjectDirection; break;
        }
        _map.cells[x, z].isModified = Modified;
        _map.RefreshNeighborTiles(x, z); _map.RefreshShadow(x, z); _map.RefreshShadow(x, z - 1); _map.RefreshFOV(x, z);
    }
}
