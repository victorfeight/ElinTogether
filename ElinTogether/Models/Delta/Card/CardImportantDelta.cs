using ElinTogether.Helper;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class CardImportantDelta : ElinDelta
{
    [Key(0)] public required RemoteCard Card { get; init; }
    [Key(1)] public bool Important { get; init; }
    [Key(2)] public bool Request { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (Card.Find() is not Thing { isDestroyed: false } item) return;
        if (net is ElinNetHost host) {
            if (!Request) return;
            var before = item.c_isImportant;
            var accepted = host.AcceptsPlayerInput(OriginPeer) &&
                host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var actor) && CanMark(actor, item);
            if (accepted) item.c_isImportant = Important;
            // Absolute state makes repeated delivery harmless and rolls back
            // client prediction when the item changed hands before arrival.
            var result = new CardImportantDelta { Card = item, Important = item.c_isImportant };
            if (accepted) host.Delta.AddRemote(result);
            else host.SendDeltaTo(OriginPeer, result);
            EmpLog.Information("Important mark resolved: peer {Peer}, item {ItemUid}, requested {Requested}, before {Before}, after {After}, accepted {Accepted}",
                OriginPeer, item.uid, Important, before, item.c_isImportant, accepted);
        } else if (!Request) {
            item.c_isImportant = Important;
        } else return;
        LayerInventory.SetDirty(item);
    }

    private static bool CanMark(Chara actor, Thing item)
    {
        var root = item.GetRootCard();
        if (root == actor) return true; // Includes bags inside one's inventory.
        if (root is Chara { IsPlayer: true }) return false;
        // Preserve vanilla access to nearby allied inventories/containers, but
        // never permit editing another human's inventory or distant NPC stock.
        return actor.IsInActiveMap && root is { ExistsOnMap: true, IsPCFactionOrMinion: true, isNPCProperty: false } &&
            actor.Dist(root) <= 3;
    }
}
