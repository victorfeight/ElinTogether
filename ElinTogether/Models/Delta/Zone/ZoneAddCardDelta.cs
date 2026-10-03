using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class ZoneAddCardDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Card { get; init; }

    [Key(1)]
    public required int ZoneUid { get; init; }

    [Key(2)]
    public required Position Pos { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (game?.activeZone?.map is null) {
            net.Delta.DeferLocal(this);
            return;
        }

        var zone = game.spatials.Find(ZoneUid) ?? SpatialGenEvent.TryPop(ZoneUid);
        if (zone is null) {
            return;
        }

        if (Card.Find() is not { } card) {
            TaskCache.CancelClientAct(net, this, Card);
            return;
        }

        if (card.isDestroyed) {
            EmpLog.Warning("Attempting to add destroyed item {Uid} to the zone at {@Pos}", card.uid, Pos);
            TaskCache.CancelClientAct(net, this, Card);
            return;
        }

        if (CardAddThingDelta.RejectForeignOwner(net, OriginPeer, Card, card)) return;
        if (card is Thing item && ItemTransferContext.Reject(net, OriginPeer, ZoneUid, Card, item, ground: zone)) return;

        if (net.IsClient && zone != _zone && card is Thing) {
            ItemTransferContext.ForgetInactiveReplica(card);
            return;
        }

        // relay to other clients
        if (net.IsHost) {
            net.Delta.AddRemote(this);
        }

        // do not add client characters from this delta
        if (card.IsPC) {
            return;
        }

        // do not update it again if same position
        // we derive all other state changes to sub deltas
        var before = QuickTransferTrace.Item(card);
        if (card.parent != zone || card.pos != Pos) {
            zone.AddCard(card, Pos.X, Pos.Z);
            EmpLog.Debug("Zone add card {Uid} at {@Pos}", card.uid, Pos);
        }
        EmpLog.Debug("PickupTrace ground-result: origin {OriginPeer}, item {ItemUid}, zone {ZoneUid}, requestedPos {@Pos}, before {Before}, after {After}, matchesGround {MatchesGround}",
            OriginPeer, card.uid, ZoneUid, Pos, before, QuickTransferTrace.Item(card),
            card.parent == zone && card.pos == Pos);
    }
}
