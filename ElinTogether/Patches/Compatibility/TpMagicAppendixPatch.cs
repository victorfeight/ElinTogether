using System;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Act), nameof(Act.Perform), new Type[] { })]
internal static class TpMagicAppendixPatch
{
    // Tp's own prefix replaces Act.Perform. Run first so a client never executes
    // its world mutations; Harmony still runs normal postfix/cast-cost handling.
    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyBefore("MagicIMagicAppendixndex")]
    internal static bool Before(Act __instance, ref bool __result, out TpSpellBridge.Execution? __state)
    {
        __state = null;
        if (NetSession.Instance.Connection is not { } net || !TpSpellBridge.IsManaged(__instance)) return true;
        if (net is ElinNetClient) {
            __result = false;
            if (!ElinDelta.IsRemoteStateLanding && Act.CC.IsPC && !PlayerControl.LocalInputBlocked) {
                var request = new TpSpellRequestDelta {
                    Id = Guid.NewGuid(), Owner = Act.CC, ActId = __instance.id,
                    ZoneUid = EClass._zone.uid, Pos = Act.TP, Target = Act.TC,
                    PowerMod = Act.powerMod, ForceTarget = Act.forcePt,
                };
                net.Delta.AddRemote(request);
                EmpLog.Information("TpSpell requested: id={Id} act={Act} actor={Actor} zone={Zone} x={X} z={Z}",
                    request.Id, request.ActId, request.Owner.Uid, request.ZoneUid, request.Pos.X, request.Pos.Z);
                __result = true;
            }
            return false;
        }
        // Keep Tp's original exclusion of AI casters. Remote humans enter here
        // only through their validated request, not arbitrary NPC spell replay.
        if (!Act.CC.IsPC && !PersonalKarma.IsHuman(Act.CC)) { __result = false; return false; }
        if (__instance.id == TpSpellBridge.Return) {
            if (!SharedTravel.CanBegin(Act.CC)) { __result = false; return false; }
            SharedTravel.Begin(Act.CC);
        }
        __state = new TpSpellBridge.Execution(__instance);
        return true;
    }

    [HarmonyFinalizer]
    internal static void After(TpSpellBridge.Execution? __state, Exception? __exception)
    {
        if (__state is null) return;
        // Restore pc/messages before publishing, even if the third-party spell throws.
        __state.Dispose();
        if (__state.ActId == TpSpellBridge.Return && SharedTravel.Current is { } journey) {
            SharedTravel.Adopt(journey);
            if (__exception != null) SharedTravel.Cancel("effect failed");
        }
        __state.Publish();
        if (__exception is not null) EmpLog.Warning(__exception, "TpSpell native execution failed");
    }
}
