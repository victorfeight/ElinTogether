using System;
using ElinTogether.Patches;
using ElinTogether.Helper;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CharaBuildDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Held { get; init; }

    [Key(1)]
    public required RemoteCard Owner { get; init; }

    [Key(2)]
    public required Position Pos { get; init; }

    [Key(3)]
    public required int Dir { get; init; }

    [Key(4)]
    public required int Altitude { get; init; }

    [Key(5)]
    public required int BridgeHeight { get; init; }

    [Key(6)]
    public int TargetUid { get; set; }

    [Key(7)] public Guid AutoActRequestId { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        var applied = false;
        try {
            ApplyBuild(net, ref applied);
        } finally {
            if (AutoActRequestId != Guid.Empty) {
                if (net.IsClient) AutoActCustomActions.Complete(AutoActRequestId, applied);
                else if (!applied) net.Delta.AddRemote(new AutoActStepDelta {
                    RequestId = AutoActRequestId, Owner = Owner, Pos = Pos,
                    ZoneUid = _zone.uid, Reply = true, Success = false,
                });
            }
        }
    }

    private void ApplyBuild(ElinNetBase net, ref bool applied)
    {
        EmpLog.Debug("Build request applying: host {Host}, peer {Peer}, owner {OwnerUid}, held {HeldUid}, pos {@Pos}",
            net.IsHost, OriginPeer, Owner.Uid, Held.Uid, Pos);
        if (Owner.Find() is not Chara chara || Held.Find() is not { } held) {
            EmpLog.Warning("Build request unresolved: owner {OwnerUid}, held {HeldUid}", Owner.Uid, Held.Uid);
            return;
        }

        // relay to clients
        if (held.parent is not Card) {
            EmpLog.Warning("Refusing stale {DeltaType} from peer {PeerIndex}, held {Uid} is no longer in inventory",
                nameof(CharaBuildDelta), OriginPeer, held.uid);
            return;
        }

        var taskBuild = new TaskBuild {
            owner = chara,
            recipe = held.trait.GetRecipe(),
            held = held,
            pos = Pos,
            dir = Dir,
            altitude = Altitude,
            bridgeHeight = BridgeHeight,
        };

        if (taskBuild.useHeld && chara.held != held) {
            // Remote held visuals are already driven by CharaSwitchHeldDelta. Calling
            // HoldCard here recreates the card renderer while its prior actor can still
            // be present in Scene.syncList, leaving a visible ghost after placement.
            if (chara.IsRemotePlayer && held.GetRootCard() == chara) {
                if (!RemoteHeldItem.TrySelect(chara, held)) return;
            } else {
                chara.HoldCard(held);
            }
        }

        taskBuild.recipe._dir = Dir;
        taskBuild.OnProgressComplete();
        EmpLog.Debug("Build request completed: owner {OwnerUid}, held {HeldUid}, target {TargetUid}, held root {RootUid}, distance {Distance}",
            chara.uid, held.uid, taskBuild.target?.uid, held.GetRootCard()?.uid,
            taskBuild.pos.Distance(chara.pos));
        applied = true;

        if (net.IsHost) {
            TargetUid = (taskBuild.target?.uid).GetValueOrDefault();
            // The client must replay the build against its pre-build held stack.
            // Host-side count changes are queued during OnProgressComplete, so
            // relay the build before those counts are refreshed into the batch.
            net.Delta.AddRemoteImmediate(this);
        } else if (TargetUid > 0 && taskBuild.target is { isDestroyed: false } target && target.uid != TargetUid) {
            if (CardCache.Find(TargetUid) is { } orphan && orphan != target) {
                CardCache.DelayDestroy(orphan);
            }

            CardCache.Rebind(target, TargetUid);
            EmpLog.Debug("Rebound built target of chara {OwnerUid} to host uid {Uid}",
                chara.uid, TargetUid);
        }
    }
}
