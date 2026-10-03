using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Card), nameof(Card.c_charges), MethodType.Setter)]
internal static class CardChargeEvent
{
    [HarmonyPrefix]
    internal static void Before(Card __instance, out int __state) => __state = __instance.c_charges;

    [HarmonyPostfix]
    internal static void OnChargeChanged(Card __instance, int __state)
    {
        if (__instance.c_charges == __state) {
            return;
        }

        if (NetSession.Instance.Connection is not { IsHost: true } connection) {
            return;
        }

        if (__instance.isDestroyed || !CardCache.Contains(__instance)) {
            return;
        }

        // Completion is replayed on clients before its captured results land.
        // Sending charges ahead of that replay lets OnRead decrement them again.
        // Keep charges with the other absolute results (including final ModNum).
        var delta = new CardChargeDelta {
            Card = __instance,
            Charges = __instance.c_charges,
        };
        if (CharaProgressCompleteEvent.ShouldPack(false)) CharaProgressCompleteEvent.Pack(delta);
        else connection.Delta.AddRemote(delta);
    }
}
