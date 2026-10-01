using System;
using System.Collections.Generic;
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

    [Key(8)] public List<ElinDelta> DeltaList { get; set; } = [];

    protected override void OnApply(ElinNetBase net)
    {
        var applied = false;
        try {
            ApplyBuild(net, ref applied);
        } finally {
            try {
                // Native replay can fail or return early; authoritative results still land.
                if (net.IsClient) {
                    if (!DeltaReplay.ApplyResults(net, DeltaList, Owner.Uid)) applied = false;
                }
            } finally {
                // Release AutoAct only after the entire result bundle has been processed.
                if (AutoActRequestId != Guid.Empty) {
                    if (net.IsClient) AutoActCustomActions.Complete(AutoActRequestId, applied);
                    else if (!applied) net.Delta.AddRemote(new AutoActStepDelta {
                        RequestId = AutoActRequestId, Owner = Owner, Pos = Pos,
                        ZoneUid = _zone.uid, Reply = true, Success = false,
                    });
                }
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

        // Recheck native held-build prerequisites before selecting anything. A stale
        // request must not take another actor's item or move it while out of reach.
        if (held.isDestroyed || held.GetRootCard() != chara || !Pos.IsInActiveMapBounds ||
            chara.pos.Distance(Pos) > 1) {
            EmpLog.Warning("Refusing invalid build for {OwnerUid}, held {HeldUid}, pos {@Pos}",
                chara.uid, held.uid, Pos);
            return;
        }

        if (net is ElinNetHost host && OriginPeer != 0 &&
            (!host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var requester) || requester != chara)) return;

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
        var blockDir = taskBuild.pos.cell.blockDir;
        var roofDir = taskBuild.pos.cell._roofBlockDir;
        if (net.IsHost) {
            DeltaList = [];
            using (CharaProgressCompleteEvent.CollectBuildSideEffects(DeltaList)) {
                taskBuild.OnProgressComplete();
            }
        } else {
            taskBuild.OnProgressComplete();
        }
        EmpLog.Debug("Build request completed: owner {OwnerUid}, held {HeldUid}, target {TargetUid}, held root {RootUid}, distance {Distance}",
            chara.uid, held.uid, taskBuild.target?.uid, held.GetRootCard()?.uid,
            taskBuild.pos.Distance(chara.pos));
        // This is a fresh TaskBuild. Vanilla sets lastPos after its rejection
        // checks; early block rotation succeeds before that assignment.
        applied = taskBuild.lastPos is not null || taskBuild.pos.cell.blockDir != blockDir ||
            taskBuild.pos.cell._roofBlockDir != roofDir;
        if (!applied) {
            EmpLog.Warning("Build returned without completion for {OwnerUid}, held {HeldUid}, pos {@Pos}",
                chara.uid, held.uid, Pos);
            return;
        }
        // Keep acknowledgement false if UID reconciliation or publishing throws.
        applied = false;
        if (net.IsHost) {
            TargetUid = (taskBuild.target?.uid).GetValueOrDefault();
            net.Delta.AddRemote(this);
        } else if (TargetUid > 0 && taskBuild.target is { isDestroyed: false } target && target.uid != TargetUid) {
            if (CardCache.Find(TargetUid) is { } orphan && orphan != target) {
                CardCache.DelayDestroy(orphan);
            }

            CardCache.Rebind(target, TargetUid);
            EmpLog.Debug("Rebound built target of chara {OwnerUid} to host uid {Uid}",
                chara.uid, TargetUid);
        }
        applied = true;
    }

    internal static CharaBuildDelta Create(TaskBuild taskBuild, List<ElinDelta>? deltaList = null)
    {
        return new() {
            Held = taskBuild.held,
            Owner = taskBuild.owner,
            Pos = taskBuild.pos,
            Dir = taskBuild.recipe._dir,
            Altitude = taskBuild.altitude,
            BridgeHeight = taskBuild.bridgeHeight,
            TargetUid = (taskBuild.target?.uid).GetValueOrDefault(),
            DeltaList = deltaList ?? [],
            AutoActRequestId = NetSession.Instance.Connection is ElinNetClient && taskBuild.owner.IsPC &&
                !ElinDelta.IsApplying && AutoActTaskBridge.FindController(taskBuild.owner, taskBuild) is not null
                ? AutoActCustomActions.BeginBuild(taskBuild) : Guid.Empty,
        };
    }
}
