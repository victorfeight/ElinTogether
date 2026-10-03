using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Card), nameof(Card.c_isImportant), MethodType.Setter)]
internal static class CardImportantEvent
{
    [HarmonyPrefix]
    internal static void Before(Card __instance, out bool __state) => __state = __instance.c_isImportant;

    [HarmonyPostfix]
    internal static void After(Card __instance, bool __state)
    {
        if (__instance is not Thing { isDestroyed: false } item || item.c_isImportant == __state ||
            ElinDelta.IsApplying || NetSession.Instance.Connection is not { } connection ||
            PendingUid.IsPending(item.uid) || !CardCache.Contains(item)) return;
        connection.Delta.AddRemote(new CardImportantDelta {
            Card = item, Important = item.c_isImportant, Request = connection.IsClient,
        });
    }
}
