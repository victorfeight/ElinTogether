using ElinTogether.Helper;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class MapRevealDelta : ElinDelta
{
    [Key(0)] public int ZoneUid { get; init; }
    [Key(1)] public int Size { get; init; }
    [Key(2)] public required int[] Cells { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (ZoneUid != _zone.uid || Size != _map.Size || Size <= 0 || Cells is null ||
            Cells.Length > (long)Size * Size) return;
        if (net is ElinNetHost host &&
            (!host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var actor) || !actor.IsInActiveMap)) return;
        // Validate the entire report before mutating any cells.
        foreach (var index in Cells) if (index < 0 || index >= (long)Size * Size) return;
        foreach (var index in Cells) _map.SetSeen(index % Size, index / Size, true, false);
        WidgetMinimap.UpdateMap();
        if (net.IsHost) net.Delta.AddRemote(this);
        EmpLog.Debug("Shared magic mapping: zone {ZoneUid}, {CellCount} cells", ZoneUid, Cells.Length);
    }
}
