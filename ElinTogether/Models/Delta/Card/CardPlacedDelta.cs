using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CardPlacedDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Owner { get; init; }

    [Key(1)]
    public required PlaceState PlaceState { get; init; }

    [Key(2)]
    public required int Dir { get; init; }

    [Key(3)]
    public required bool ByPlayer { get; init; }

    [Key(4)] public int ZoneUid { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (Owner.Find() is not { } card) {
            return;
        }

        if (card is Thing item && ItemTransferContext.Reject(net, OriginPeer, ZoneUid, Owner, item)) return;

        if (net.IsHost) {
            net.Delta.AddRemote(this);
        }

        card.dir = Dir;
        card.Stub_SetPlacedState(PlaceState, ByPlayer);
    }
}
