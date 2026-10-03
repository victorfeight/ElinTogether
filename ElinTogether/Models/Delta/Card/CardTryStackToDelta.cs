using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CardTryStackToDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Card { get; init; }

    [Key(1)]
    public required RemoteCard To { get; init; }

    [Key(2)]
    public required RemoteCard? Parent { get; init; }

    [Key(3)] public int ZoneUid { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        // Clients request a merge; the host publishes the native count/removal
        // results. Receivers must never perform that merge for a second time.
        if (!net.IsHost || Card.Find() is not Thing { isDestroyed: false } card) return;
        if (CardAddThingDelta.RejectForeignOwner(net, OriginPeer, Card, card)) return;
        if (ItemTransferContext.Reject(net, OriginPeer, ZoneUid, Card, card, To.Find())) return;

        if (To.Find() is not Thing { isDestroyed: false } to ||
            (Parent is not null ? Parent.Find() is not { } parent || parent != to.parent : to.parent is not Zone) ||
            !card.CanStackTo(to)) {
            // A missing/moved stack is not permission to force an item into its
            // old container. Preserve the actual host location and let UI retry.
            // A deferred craft/harvest product may not yet have any host parent.
            // Preserve it on the requesting player's tile if its stack vanished.
            if (card.parent is null && net is ElinNetHost host &&
                host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var requester)) {
                _zone.AddCard(card, requester.pos);
            }
            CardAddThingDelta.Rebind(net, Card, card);
            return;
        }

        card.TryStackTo(to);
    }
}
