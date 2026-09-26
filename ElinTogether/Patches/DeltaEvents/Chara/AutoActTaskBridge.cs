using System.Linq;
using ElinTogether.Models;
using ElinTogether.Net;

namespace ElinTogether.Patches;

// Optional integration: no dependency on Auto Act's DLL or its task selection code.
// Progress tasks share the normal serializer; direct/custom actions use their own hooks.
internal static class AutoActTaskBridge
{
    internal static AIAct? FindController(Chara owner, AIAct task)
    {
        if (!owner.IsPC || NetSession.Instance.Connection is not ElinNetClient) return null;

        return FindAttachedController(owner, task);
    }

    private static AIAct? FindAttachedController(Chara owner, AIAct task)
    {
        if (!IsController(owner.ai)) return null;

        // Reject detached/stale tasks. Completion and cancel target the current tree.
        for (var parent = task.parent; parent is not null; parent = parent.parent) {
            if (ReferenceEquals(parent, owner.ai)) return parent;
        }
        return null;
    }

    internal static bool IsController(AIAct act)
    {
        for (var type = act.GetType(); type is not null; type = type.BaseType) {
            if (type.FullName == "AutoActMod.Actions.AutoAct") return true;
        }
        return false;
    }

    internal static void PublishChild(Chara owner, AIAct task)
    {
        if (ElinDelta.IsApplying || FindAttachedController(owner, task) is null) return;
        // SetChild bypasses SetAI on both peers. Publish every locally authoritative
        // controller, including host NPCs, so receivers can replay terrain changes.
        // Remote humans remain client-controlled even if their host AI is stale.
        var canPublish = NetSession.Instance.Connection switch {
            ElinNetClient => owner.IsPC,
            ElinNetHost host => !host.ActiveRemoteCharas.Values.Contains(owner),
            _ => false,
        };
        if (!canPublish) return;
        // OnProgressBegin runs after Auto Act's SetNextTask has configured the target.
        // Serialize every run, even when the same task/Point is reused on another tile.
        var args = CharaTaskRemoteEvent.CreateTaskArgs(owner, task);
        if (args is FakeTask) {
            EmpLog.Warning("Auto Act task {TaskType} has no progress serializer", task.GetType().FullName);
            return;
        }
        CharaTaskRemoteEvent.PublishTask(owner, task, args);
        EmpLog.Debug("Auto Act child {TaskType} submitted by {Uid}", task.GetType().Name, owner.uid);
    }

    internal static void Stop(Chara owner, AIAct controller)
    {
        if (!ReferenceEquals(owner.ai, controller)) return;
        // Bypass Auto Act.Cancel's retry loop after authoritative rejection/cancellation.
        controller.Stub_Cancel();
        owner.SetNoGoal(); // Existing NoTask packet also clears any remaining host child.
        EmpLog.Debug("Auto Act stopped after task cancellation for {Uid}", owner.uid);
    }
}
