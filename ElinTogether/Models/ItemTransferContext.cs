using ElinTogether.Net;

namespace ElinTogether.Models;

// Inventory outcomes may arrive after the host has left their source map.
// Native Zone.RemoveCard intentionally does not detach an inactive map's lists.
internal static class ItemTransferContext
{
    internal static bool Reject(ElinNetBase net, int peer, int requestZone,
        RemoteCard remote, Thing item, Card? destination = null, Zone? ground = null)
    {
        if (!net.IsHost || peer == 0) return false;
        var sourceZone = item.GetRootCard().parent as Zone;
        var destinationZone = destination?.GetRootCard().parent as Zone;
        if ((requestZone == 0 || requestZone == EClass._zone.uid) &&
            (sourceZone is null || sourceZone == EClass._zone) &&
            (destinationZone is null || destinationZone == EClass._zone) &&
            (ground is null || ground == EClass._zone)) return false;

        EmpLog.Warning("PickupTrace stale-zone rejected: peer {Peer}, item {Item}, requestZone {RequestZone}, sourceZone {SourceZone}, destinationZone {DestinationZone}, activeZone {ActiveZone}",
            peer, item.uid, requestZone, sourceZone?.uid, (ground ?? destinationZone)?.uid, EClass._zone.uid);
        CardAddThingDelta.Rebind(net, remote, item);
        return true;
    }

    // Client maps are always replaced by host snapshots on entry. An off-map
    // correction must undo prediction, not place a live object into an inactive
    // map (which would skip native registration and later leave stale entries).
    internal static void ForgetInactiveReplica(Card card)
    {
        card.parent?.RemoveCard(card);
        if (EClass.pc.held == card) {
            EClass.pc.held = null;
            WidgetCurrentTool.dirty = true;
        }
        if (card is Thing item) LayerInventory.SetDirty(item);
        CardCache.Forget(card);
        EmpLog.Debug("PickupTrace off-map replica released: item {Item}, activeZone {ActiveZone}",
            card.uid, EClass._zone.uid);
    }
}
