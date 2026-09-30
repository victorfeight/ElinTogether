using ElinTogether.Helper;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(ActPray), nameof(ActPray.TryPray))]
internal static class ActPrayEvent
{
    [HarmonyPrefix]
    internal static bool OnTryPray(Chara c, bool passive, ref bool __result, out ScopeExit? __state)
    {
        __state = default;
        if (NetSession.Instance.Connection is null || FaithTransactions.Actor == c) {
            return true;
        }
        if (NetSession.Instance.IsClient) {
            if (c.IsPC) FaithTransactions.Submit(FaithAction.Pray, passive: passive);
            __result = true;
            return false;
        }
        if (!c.IsRemotePlayer) { __state = FaithTransactions.BeginLocal(c); return true; }
        __result = true;
        return false; // remote prayer must arrive through a validated request
    }

    [HarmonyFinalizer]
    internal static void AfterPrayer(ScopeExit? __state) => __state?.Dispose();
}
