using System;
using System.Collections.Generic;
using ElinTogether.API.SourceValidation;
using ElinTogether.Elements;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CharaProgressCompleteDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Owner { get; init; }

    [Key(1)]
    public required int CompletedActId { get; init; }

    [Key(2)]
    public required List<ElinDelta> DeltaList { get; init; }

    [Key(3)] public bool Failed { get; init; }

    public static CharaProgressCompleteDelta? Current { get; private set; }

    // progress replay, client local sim
    public static bool IsReplaying => Current is not null;

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost) return;

        var previous = Current;
        Current = this;
        Chara? chara = null;
        AIAct? ai = null;
        AIAct? autoAct = null;
        AIAct? root = null;
        var completed = false;
        try {
            try {
                chara = Owner.Find() as Chara;
                if (chara is null) return;
                if (!ActMappingValidator.Default.IdToActMapping.TryGetValue(CompletedActId, out var type)) {
                    net.ReportDesync($"Unknown completed action {CompletedActId} for {Owner.Uid}");
                    return;
                }

                root = chara.ai;
                ai = root.Current;
                while (ai is not null && ai.GetType() != type && !DelegateProgress.Represents(ai, type)) {
                    ai = ai.parent;
                }

                if (ai is not null) autoAct = AutoActTaskBridge.FindController(chara, ai);
                if (Failed) return; // Results and cancellation are handled in finally.
                var progress = ai as DelegateProgress ?? ai?.child;
                if (progress is null) {
                    EmpLog.Debug("Completed child {ActType} no longer running for {Uid}; applying {Count} results",
                        type.Name, Owner.Uid, DeltaList.Count);
                    return;
                }

                progress.OnProgressComplete();
                progress.Success();
                completed = true;
            } catch (Exception ex) {
                EmpLog.Error(ex, "Progress replay failed for {OwnerUid}, action {ActId}", Owner.Uid, CompletedActId);
                net.ReportDesync(ex.ToString());
            } finally {
                // Even missing/finished tasks and failed native callbacks must receive
                // the host's results. Keep Current set for all result replay guards.
                var resultsApplied = DeltaReplay.ApplyResults(net, DeltaList, Owner.Uid);
                if (autoAct is not null && (!completed || !resultsApplied)) {
                    AutoActTaskBridge.Stop(chara!, autoAct);
                } else if (Failed && ai is not null && chara is not null && ReferenceEquals(chara.ai, root)) {
                    ProgressFailure.StopTask(chara, ai);
                }
            }

            if (!completed || chara is null || !ReferenceEquals(chara.ai, root)) return;

            // Continue only after results land. Auto Act resumes on its normal tick;
            // a late result must never replace a newly selected task/controller.
            if (autoAct is null && ai is not DelegateProgress) {
                ai!.Tick();
                if (ai.status != AIAct.Status.Running && ReferenceEquals(chara.ai, root)) {
                    chara.SetNoGoal();
                }
            }

            if (chara.IsPC || chara.ai is not GoalRemote remote) return;
            remote.InsertAction(null);
            if (chara.held is { } held && held.GetRootCard() == chara) chara.held = null;
        } finally {
            Current = previous;
        }
    }
}
