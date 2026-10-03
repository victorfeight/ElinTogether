using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class MapBoundsDelta : ElinDelta
{
    [Key(0)] public int ZoneUid { get; init; }
    [Key(1)] public int MapSize { get; init; }
    [Key(2)] public int X { get; init; }
    [Key(3)] public int Z { get; init; }
    [Key(4)] public int MaxX { get; init; }
    [Key(5)] public int MaxZ { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is not ElinNetClient || game?.activeZone?.map is not { } map ||
            game.activeZone.uid != ZoneUid || map.Size != MapSize ||
            X < 1 || Z < 1 || MaxX < X || MaxZ < Z || MaxX >= MapSize - 1 || MaxZ >= MapSize - 1) return;
        map.bounds.SetBounds(X, Z, MaxX, MaxZ);
        map.RefreshAllTiles();
        WidgetMinimap.UpdateMap();
        EmpLog.Information("Map bounds synchronized: zone {Zone}, bounds {X},{Z}..{MaxX},{MaxZ}", ZoneUid, X, Z, MaxX, MaxZ);
    }
}
