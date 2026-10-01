using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElinTogether.API.SourceValidation;
using ElinTogether.Elements;
using ElinTogether.Helper;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class CharaProgressCompleteEvent
{
    internal sealed class Capture
    {
        internal required Chara Owner;
        internal required AIAct Action;
        internal Capture? Previous;
        internal readonly List<ElinDelta> Results = [];
        internal Exception? Failure;
    }

    private static Capture? _current;
    private static List<ElinDelta>? _sideDeltaList;
    internal static bool BuildFailed { get; private set; }
    internal static Chara? Chara => _current?.Owner;
    internal static AIAct? Action => _current?.Action;
    internal static bool IsHappening => _current is not null;

    // host packs side delta during ProgressComplete, clients replay
    // pick is for remote players only
    internal static bool ShouldPack(bool remoteOnly)
    {
        if (!IsHappening || NetSession.Instance.Connection is not { IsHost: true }) {
            return false;
        }

        return !remoteOnly || Chara is { IsRemotePlayer: true };
    }

    internal static void Pack(ElinDelta delta)
    {
        _current?.Results.Add(delta);
    }

    internal static ScopeExit CollectBuildSideEffects(List<ElinDelta> into)
    {
        var previous = _sideDeltaList;
        var failed = BuildFailed;
        BuildFailed = false;
        _sideDeltaList = into;
        return new() {
            OnExit = () => { _sideDeltaList = previous; BuildFailed = failed; },
        };
    }

    internal static IEnumerable<MethodBase> TargetMethods()
    {
        return OverrideMethodComparer
            .FindAllOverrides(typeof(AIAct), nameof(AIAct.OnProgressComplete))
            .Where(mi => typeof(AIProgress).IsAssignableFrom(mi.DeclaringType) || mi.DeclaringType == typeof(TaskBuild));
    }

    [HarmonyPrefix]
    internal static bool OnProgressComplete(AIAct __instance, out Capture? __state)
    {
        __state = null;
        if (NetSession.Instance.Connection is not { } connection || __instance.owner is not { } owner) return true;
        __state = new() { Owner = owner, Action = __instance, Previous = _current };
        _current = __state;
        if (__instance is not TaskBuild taskBuild) return true;
        if (connection.IsClient && owner.IsPC && !ElinDelta.IsApplying && taskBuild.held is not null) {
            connection.Delta.AddRemote(CharaBuildDelta.Create(taskBuild));
        }
        return connection.IsHost || ElinDelta.IsApplying;
    }

    // One exit owns the collected results, including when native completion throws.
    // Nested callbacks join their outer operation instead of clearing its capture.
    [HarmonyFinalizer]
    internal static void OnProgressCompleteCleanup(Exception? __exception, Capture? __state)
    {
        if (__state is null) return;
        _current = __state.Previous;
        __state.Failure ??= __exception;
        if (__state.Previous is { } outer) {
            outer.Results.AddRange(__state.Results);
            outer.Failure ??= __state.Failure;
            return;
        }
        if (NetSession.Instance.Connection is not ElinNetHost host) return;
        if (__state.Failure is null) {
            PublishSuccess(host, __state);
            return;
        }

        if (_sideDeltaList is not null) BuildFailed = true;

        // Do not replay a failed native operation as a successful completion.
        // Preserve the original exception even if recovery itself encounters a fault.
        try {
            EmpLog.Error(__state.Failure, "Host progress failed for {OwnerUid}, {ActType}; recovering {Count} captured results",
                __state.Owner.uid, __state.Action.GetType().Name, __state.Results.Count);
            var task = __state.Action is AIProgress ? __state.Action.parent : __state.Action;
            var type = task is DelegateProgress delegated ? delegated.ActType : task?.GetType();
            var actId = type is not null && ActMappingValidator.Default.ActToIdMapping.TryGetValue(type, out var id) ? id : -1;
            host.Delta.AddRemote(new CharaProgressCompleteDelta {
                Owner = __state.Owner, CompletedActId = actId, Failed = true,
                DeltaList = ProgressFailure.CaptureResults(__state.Owner, __state.Action, __state.Results),
            });
            if (task is not null) ProgressFailure.StopTask(__state.Owner, task);
        } catch (Exception recoveryError) {
            EmpLog.Error(recoveryError, "Host progress recovery failed for {OwnerUid}", __state.Owner.uid);
            host.ReportDesync(recoveryError.ToString());
        }
    }

    private static void PublishSuccess(ElinNetHost host, Capture state)
    {
        if (state.Action is TaskBuild taskBuild) {
            if (_sideDeltaList is { } collector) collector.AddRange(state.Results);
            else if (!state.Owner.IsRemotePlayer && taskBuild.held is not null && !ElinDelta.IsApplying)
                host.Delta.AddRemote(CharaBuildDelta.Create(taskBuild, state.Results));
            else foreach (var delta in state.Results) host.Delta.AddRemote(delta);
            return;
        }
        if (state.Action is DelegateProgress || state.Action.parent?.GetType() is not { } type ||
            !ActMappingValidator.Default.ActToIdMapping.TryGetValue(type, out var actId)) return;
        host.Delta.AddRemote(new CharaProgressCompleteDelta {
            Owner = state.Owner, CompletedActId = actId, DeltaList = state.Results,
        });
    }
}
