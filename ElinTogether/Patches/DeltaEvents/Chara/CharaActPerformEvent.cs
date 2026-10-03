using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using ElinTogether.Helper;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class CharaActPerformEvent
{
    private static readonly HashSet<int> _alwaysSuccessfulActs = [
        // for some reason these return false
        ABILITY.ActRide,
        ABILITY.ActParasite,
    ];

    internal static IEnumerable<MethodBase> TargetMethods()
    {
        return OverrideMethodComparer.FindAllOverrides(typeof(Act), nameof(Act.Perform))
            .Where(mi => mi.DeclaringType != typeof(DynamicAct) &&
                         mi.DeclaringType != typeof(DynamicAIAct) &&
                         mi.DeclaringType != typeof(ActQuickCraft));
    }

    [HarmonyPostfix]
    internal static void OnCharaActPerform(Act __instance, bool __result)
    {
        if (NetSession.Instance.Connection is not { } connection) {
            return;
        }

        // to save bandwidth, only propagate successful act perform events
        if (!__result && !_alwaysSuccessfulActs.Contains(__instance.id)) {
            return;
        }

        // Item-bound zaps capture their context before vanilla mutates Act.TC/TP.
        // Throws have their own existing event as well.
        // Native melee creates these nested retaliation actions without source IDs.
        // Their authoritative damage/conditions already use the result deltas;
        // replaying them as standalone ability 0 cannot reconstruct the action.
        if (__instance is ActMeleeCounter or ActMeleeParry ||
            __instance is ActPray || __instance is ActZap || __instance is ActThrow and not ActRanged) {
            return;
        }

        // host propagates every act perform event
        // clients only propagate self
        if (connection.IsHost || Act.CC.IsPC) {
            var delta = CharaActPerformDelta.Create(__instance);
            if (delta.ActId == 0) {
                // Only anomalous actions pay for a caller trace. Do not read act.source:
                // it is lazy and the diagnostic must not change action initialization.
                var route = string.Join(" <- ", (new StackTrace(1, false).GetFrames() ?? [])
                    .Take(10).Select(frame => frame.GetMethod())
                    .Select(method => $"{method?.DeclaringType?.FullName}.{method?.Name}"));
                EmpLog.Warning("ActionTrace sending zero-ID action: type {ActType}, actor {ActorUid}, target {TargetUid}, applying {Applying}, route {Route}",
                    delta.DiagnosticActType, delta.Owner.Uid, delta.TargetCard?.Uid,
                    ElinDelta.IsApplying, route);
            }
            connection.Delta.AddRemote(delta);
            EmpLog.Debug("Act {ActId} by chara {OwnerUid} at {@Pos}, target {TargetUid}",
                delta.ActId, delta.Owner.Uid, delta.Pos, delta.TargetCard?.Uid);
        }
    }
}
