using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Auto Act's custom steps have no AIProgress to synchronize. Keep navigation and
// timing local, then await one host-authoritative operation before choosing again.
[HarmonyPatch]
internal static class AutoActCustomActions
{
    internal sealed class Ticket { internal bool Done, Success; }
    private static readonly Dictionary<Guid, Ticket> Pending = [];
    private static ConditionalWeakTable<TaskBuild, Ticket> Builds = new();
    [ElinPreLoad]
    private static void Reset(GameIOContext _)
    {
        Pending.Clear();
        Builds = new();
    }
    internal static Guid BeginBuild(TaskBuild task)
    {
        var id = Guid.NewGuid();
        var ticket = new Ticket();
        Builds.Remove(task);
        Builds.Add(task, ticket);
        Pending.Add(id, ticket);
        return id;
    }

    internal static IEnumerable<AIAct.Status> WaitForBuild(TaskBuild task, IEnumerable<AIAct.Status> original)
    {
        var owner = task.owner;
        var controller = owner.ai;
        using var iterator = original.GetEnumerator();
        while (true) {
            var more = iterator.MoveNext();
            if (Builds.TryGetValue(task, out var ticket)) {
                while (!ticket.Done) yield return AIAct.Status.Running;
                Builds.Remove(task);
                if (!ticket.Success) { AutoActTaskBridge.Stop(owner, controller); yield break; }
            }
            if (!more) yield break;
            yield return iterator.Current;
        }
    }

    internal static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] {
            "AutoActMod.Actions.AutoActClean+SubActClean",
            "AutoActMod.Actions.AutoActWater+SubActWater",
            "AutoActMod.Actions.AutoActDisarm",
        }) {
            var type = AccessTools.TypeByName(name);
            if (type is not null) yield return AccessTools.Method(type, "Run");
        }
        yield return AccessTools.Method(typeof(DynamicAIAct), nameof(DynamicAIAct.Run));
    }

    [HarmonyPrefix]
    internal static bool Before(AIAct __instance, ref IEnumerable<AIAct.Status> __result)
    {
        if (NetSession.Instance.Connection is not ElinNetClient || ElinDelta.IsApplying ||
            __instance.owner is not { IsPC: true } owner || !AutoActTaskBridge.IsController(owner.ai)) return true;
        var name = __instance.GetType().FullName;
        AutoActStepDelta.Step kind;
        Point pos;
        Card? target = null;
        if (__instance is DynamicAIAct dynamic) {
            if (dynamic.lang != "SubActDrawWater_AutoAct") return true;
            kind = AutoActStepDelta.Step.Refill;
            pos = dynamic.pos;
        } else if (name == "AutoActMod.Actions.AutoActClean+SubActClean") {
            kind = AutoActStepDelta.Step.Clean;
            pos = (Point)AccessTools.Field(__instance.GetType(), "pos").GetValue(__instance);
        } else if (name == "AutoActMod.Actions.AutoActWater+SubActWater") {
            kind = AutoActStepDelta.Step.Water;
            pos = (Point)AccessTools.Field(__instance.GetType(), "dest").GetValue(__instance);
        } else if (name == "AutoActMod.Actions.AutoActDisarm") {
            kind = AutoActStepDelta.Step.Disarm;
            target = ((TraitTrap)AccessTools.Field(__instance.GetType(), "target").GetValue(__instance)).owner;
            pos = target.pos;
        } else return true;
        __result = Run(__instance, owner, kind, pos.Copy(), target);
        return false;
    }

    private static IEnumerable<AIAct.Status> Run(AIAct act, Chara owner, AutoActStepDelta.Step kind, Point pos, Card? target)
    {
        var controller = owner.ai;
        if (kind != AutoActStepDelta.Step.Disarm) yield return act.DoGoto(pos, 1, true);
        if (owner.Dist(pos) > 1) { AutoActTaskBridge.Stop(owner, controller); yield break; }
        if (kind == AutoActStepDelta.Step.Clean) {
            var duration = pos.cell.HasLiquid ? 5 : 1;
            for (var i = 0; i < duration; i++) {
                owner.LookAt(pos);
                owner.renderer.NextFrame();
                yield return AIAct.Status.Running;
            }
        }
        do {
            var id = Guid.NewGuid();
            var ticket = new Ticket();
            Pending.Add(id, ticket);
            try {
                NetSession.Instance.Connection!.Delta.AddRemote(new AutoActStepDelta {
                    RequestId = id, Owner = owner, Kind = kind, Pos = pos,
                    ZoneUid = EClass._zone.uid, Tool = owner.held, Target = target,
                });
                while (!ticket.Done) yield return AIAct.Status.Running;
                if (!ticket.Success) { AutoActTaskBridge.Stop(owner, controller); yield break; }
            } finally { Pending.Remove(id); }
            if (kind != AutoActStepDelta.Step.Disarm || target is null || target.isDestroyed) yield break;
            yield return AIAct.Status.Running;
        } while (ReferenceEquals(owner.ai, controller) && !owner.isDead);
    }

    internal static void Complete(Guid id, bool success)
    {
        if (Pending.TryGetValue(id, out var ticket)) { ticket.Success = success; ticket.Done = true; Pending.Remove(id); }
    }
}

[HarmonyPatch(typeof(TaskPoint), nameof(TaskPoint.Run))]
internal static class AutoActBuildWait
{
    [HarmonyPostfix]
    internal static void After(TaskPoint __instance, ref IEnumerable<AIAct.Status> __result)
    {
        if (__instance is TaskBuild build && build.owner is { } owner &&
            AutoActTaskBridge.FindController(owner, build) is not null) {
            __result = AutoActCustomActions.WaitForBuild(build, __result);
        }
    }
}
