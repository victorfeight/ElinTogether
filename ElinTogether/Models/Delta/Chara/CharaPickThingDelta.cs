using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CharaPickThingDelta : ElinDelta
{
    public enum PickType : byte
    {
        Pick,
        PickOrDrop,
        TrySmoothPick,
    }

    [Key(0)]
    public required RemoteCard Owner { get; init; }

    [Key(1)]
    public required RemoteCard Thing { get; init; }

    [Key(2)]
    public required Position? Pos { get; init; }

    [Key(3)]
    public required PickType Type { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        // This is a host-created product handoff, not a report of attempted pickup.
        // Only the collecting client decides the destination using its vanilla
        // inventory settings. Everyone else receives ordinary inventory outcomes.
        if (net.IsHost || Owner.Find() is not Chara chara) return;

        if (Thing.Find() is not Thing { isDestroyed: false } thing) {
            TaskCache.CancelClientAct(net, this, Thing);
            return;
        }

        EmpLog.Debug("PickupTrace product handoff: type {PickType}, actor {ActorUid}, item {ItemUid}, state {State}",
            Type, chara.uid, thing.uid, QuickTransferTrace.Item(thing));

        // A late handoff must not move an item that has already been collected.
        if (thing.GetRootCard() is Chara { IsPlayer: true }) return;
        if (!chara.IsPC) {
            // Keep a newly generated product visible until its owner's outcome
            // arrives. Do not relocate an existing item on an observer.
            if (thing.parent is null) _zone.AddCard(thing, Pos ?? chara.pos);
            return;
        }

        // This delegated local decision must emit the same transfer/stack/drop
        // messages as ordinary pickup, rather than silently mutating during replay.
        using (Simulate()) {
            switch (Type) {
                case PickType.Pick:
                    chara.Pick(thing);
                    break;
                case PickType.PickOrDrop:
                    chara.PickOrDrop(Pos, thing);
                    break;
                case PickType.TrySmoothPick:
                    _map.TrySmoothPick(Pos, thing, chara);
                    break;
            }
        }

        // A rejected pickup of an already-grounded item has no mutation to send.
        // In particular, a queued stack/codex request can leave it grounded until
        // the host responds; never follow that request with a guessed ground result.
    }
}
