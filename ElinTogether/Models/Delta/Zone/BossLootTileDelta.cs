using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

// Host terrain edits from boss loot and path destruction. Keep the existing
// packet/union identity; clients never repeat the mining or generate its drops.
[MessagePackObject]
public class BossLootTileDelta : ElinDelta
{
    [Key(0)] public required int ZoneUid { get; init; }
    [Key(1)] public required Position Pos { get; init; }
    [Key(2)] public required bool Block { get; init; }
    [Key(3)] public required int Id { get; init; }
    [Key(4)] public int Material { get; init; }
    [Key(5)] public int Value { get; init; }
    [Key(6)] public int Direction { get; init; }
    [Key(7)] public bool Floor { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || EClass._zone?.uid != ZoneUid || !Pos.IsInActiveMapBounds) return;
        Point point = Pos;
        if (Floor) EClass._map.SetFloor(point.x, point.z, Material, Id, Direction);
        else if (Block) EClass._map.SetBlock(point.x, point.z, Material, Id, Direction);
        else point.SetObj(Id, Value, Direction);
        EClass._map.RefreshShadow(point.x, point.z);
        EClass._map.RefreshShadow(point.x, point.z - 1);
        EClass._map.RefreshFOV(point.x, point.z);
        EmpLog.Debug("Applied authoritative terrain in zone {ZoneUid} at {@Pos}, block {Block}, id {Id}", ZoneUid, Pos, Block, Id);
    }
}
