using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

// Native recipe construction sets these fields after adding the card to the
// zone. Publish the final result, including for queued/NPC/Agent construction
// which does not have a held-item CharaBuildDelta replay.
[MessagePackObject]
public sealed class CardConstructionState : ElinDelta
{
    [Key(0)] public required RemoteCard Card { get; init; }
    [Key(1)] public int ZoneUid { get; init; }
    [Key(2)] public PlaceState Placement { get; init; }
    [Key(3)] public int Direction { get; init; }
    [Key(4)] public int Altitude { get; init; }
    [Key(5)] public bool Roof { get; init; }
    [Key(6)] public bool IgnoreStackHeight { get; init; }
    [Key(7)] public bool FreePosition { get; init; }
    [Key(8)] public float X { get; init; }
    [Key(9)] public float Y { get; init; }
    [Key(10)] public bool PlayerCreation { get; init; }
    [Key(11)] public bool Subset { get; init; }
    [Key(12)] public int ParentUid { get; init; }
    [Key(13)] public bool Masked { get; init; }
    internal static CardConstructionState Capture(global::Card card) => new() {
        Card = card, ZoneUid = _zone.uid, Placement = card.placeState, Direction = card.dir, Altitude = card.altitude,
        Roof = card.isRoofItem, IgnoreStackHeight = card.ignoreStackHeight, FreePosition = card.freePos,
        X = card.fx, Y = card.fy, PlayerCreation = card.isPlayerCreation, Subset = card.isSubsetCard,
        ParentUid = card.parentCard?.uid ?? 0, Masked = card.isMasked,
    };
    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || ZoneUid != _zone.uid) return;
        var card = Card.Find();
        if (card == null) { net.Delta.DeferLocal(this); return; }
        if (card.isDestroyed || (ParentUid == 0 ? card.parent != _zone : card.parentCard?.uid != ParentUid)) return;
        card.dir = Direction;
        if (card.placeState != Placement) card.Stub_SetPlacedState(Placement, byPlayer: true);
        card.altitude = Altitude; card.isRoofItem = Roof; card.ignoreStackHeight = IgnoreStackHeight;
        card.freePos = FreePosition; card.fx = X; card.fy = Y;
        card.isPlayerCreation = PlayerCreation; card.isSubsetCard = Subset;
        card.isMasked = Masked;
        card.renderer?.RefreshSprite();
    }
}
