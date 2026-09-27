using System.Collections.Generic;
using ElinTogether.Models;

namespace ElinTogether.Elements;

internal class GoalRemote : NoGoal
{
    internal CharaSwitchHeldDelta? PendingHeld;

    internal static GoalRemote Default => new();

    public override bool IsIdle => true;

    public override bool IsNoGoal => true;

    public override int MaxRestart => 114_514 & 19_19_810;

    public override bool CancelWhenDamaged => false;

    public override bool PushChara => false;

    public override IEnumerable<Status> Run()
    {
        while (!owner.isDestroyed) {
            if (!IsChildRunning) {
                yield return Status.Running;
                HaltChildAct();
            }

            yield return Status.Running;
        }
    }

    public void InsertAction(AIAct? action, Card? taskTool = null, bool applyTaskTool = false)
    {
        HaltChildAct();

        if (action is null) {
            return;
        }

        if (applyTaskTool) {
            if (taskTool is null) owner.PickHeld();
            else owner.HoldCard(taskTool);
            EmpLog.Debug("Task tool selected for {Uid}: requested {RequestedUid}, held {HeldUid}", owner.uid, taskTool?.uid, owner.held?.uid);
        }

        if (action is BaseTaskHarvest t) {
            t.SetTarget(owner);
        }

        child = action;
        child.SetOwner(owner);

        Tick();
    }

    public void HaltChildAct()
    {
        child?.Reset();
        child = null;
        // Preserve switches received during progress and apply before the next task
        // snapshots owner.Tool. Never change a tool halfway through its current task.
        if (PendingHeld is { } held) {
            PendingHeld = null;
            held.ApplyHeld(owner);
            EmpLog.Debug("Applied deferred held tool for chara {Uid}: {HeldUid}", owner.uid, owner.held?.uid);
        }
    }
}