using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(ActZap), nameof(ActZap.Perform), new System.Type[0])]
internal static class ActZapEvent
{
    [HarmonyPrefix]
    internal static void Before(ActZap __instance, out CharaActPerformDelta? __state)
    {
        __state = null;
        if (NetSession.Instance.Connection is not ElinNetClient || ElinDelta.IsApplying || !Act.CC.IsPC) return;
        // Ground aim must be copied before ProcAt changes the shared Act context.
        __state = CharaActPerformDelta.Create(__instance);
    }

    [HarmonyPostfix]
    internal static void After(bool __result, CharaActPerformDelta? __state)
    {
        if (!__result || __state is null || NetSession.Instance.Connection is not ElinNetClient client) return;
        client.Delta.AddRemote(__state);
        EmpLog.Information("Zap requested {ZapId}: caster {Caster}, wand {Wand}, aim {X},{Z}",
            __state.ZapId, __state.Owner.Uid, __state.Wand?.Uid, __state.Pos?.X, __state.Pos?.Z);
    }
}
