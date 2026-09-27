using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

// The host's exact tile edit, not another loot-placement search on the client.
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

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || EClass._zone?.uid != ZoneUid || !Pos.IsInActiveMapBounds) return;
        Point point = Pos;
        if (Block) point.SetBlock(Material, Id);
        else point.SetObj(Id, Value, Direction);
        EmpLog.Debug("Applied boss loot terrain in zone {ZoneUid} at {@Pos}, block {Block}, id {Id}", ZoneUid, Pos, Block, Id);
    }
}
