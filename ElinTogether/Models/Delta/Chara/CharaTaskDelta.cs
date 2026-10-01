using ElinTogether.Elements;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CharaTaskDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Owner { get; init; }

    [Key(1)]
    public required TaskArgsBase? TaskArgs { get; init; }

    // Capture the tool with the task; display updates can arrive after task creation.
    [Key(2)] public RemoteCard? Tool { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (Owner.Find() is not Chara { IsPC: false } chara) {
            return;
        }

        var act = TaskArgs?.CreateSubAct();

        if (chara.isDead) {
            if (net.IsHost && act is not null) {
                TaskCache.RequestCancel(net, Owner, act);
            }

            return;
        }

        if (net.IsHost && TaskCache.GetRequiredPos(act) is { } pos &&
            TaskCache.IsPosTaken(pos, chara)) {
            EmpLog.Debug("Task {ActType} on {@Pos} refused for chara {Uid}, pos already taken",
                act!.GetType().Name, pos, Owner.Uid);
            TaskCache.RequestCancel(net, Owner, act!);
            return;
        }

        // Enforce host policy even when the requesting client has stale UI.
        if (net.IsHost && act is AI_Steal && MoongateTheftPatch.IsTheftBlocked(EClass._zone)) {
            TaskCache.RequestCancel(net, Owner, act);
            return;
        }

        var taskTool = Tool?.Find();
        if (act is BaseTaskHarvest && Tool is not null &&
            (taskTool is not Thing || taskTool.isDestroyed || taskTool.GetRootCard() != chara)) {
            EmpLog.Warning("Task {ActType} rejected for {Uid}: selected tool {ToolUid} missing or not owned", act.GetType().Name, chara.uid, Tool.Uid);
            if (net.IsHost) TaskCache.RequestCancel(net, Owner, act);
            return;
        }

        // relay to clients
        if (net.IsHost) {
            net.Delta.AddRemote(this);
        }

        if (chara.ai is not GoalRemote) {
            // assume client GoalRemote is a light year away and drop it may desync the goals
            if (!net.IsClient) {
                return;
            }

            chara.SetAI(GoalRemote.Default);
        }

        if (chara.ai is not GoalRemote remote) {
            return;
        }

        if (remote.owner is null) {
            remote.SetOwner(chara);
        }

        // now assign new task or reset
        using (Simulate(net.IsHost && RemoteCraft.IsHostRun(act))) {
            remote.InsertAction(act, taskTool, act is BaseTaskHarvest);
        }
    }
}