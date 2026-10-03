using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class CardEnhancementEvent
{
    [HarmonyPrefix, HarmonyPatch(typeof(Card), nameof(Card.ModEncLv))]
    internal static void Before(Card __instance, out int __state) => __state = __instance.encLV;

    [HarmonyPostfix, HarmonyPatch(typeof(Card), nameof(Card.ModEncLv))]
    internal static void After(Card __instance, int __state)
    {
        if (__instance.encLV != __state) Publish(__instance, "change");
    }

    // Transfer keeps a cached item's identity. Reconcile its enhancement too,
    // including gear repaired before this synchronization channel was installed.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Card), nameof(Card.AddThing), typeof(Thing), typeof(bool), typeof(int), typeof(int))]
    internal static void AfterTransfer(Card __instance, Thing __result)
    {
        if (__result is not null && __result.parent == __instance) Publish(__result, "transfer");
    }

    private static void Publish(Card card, string reason)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host ||
            !EClass.core.IsGameStarted || EClass.game.isLoading ||
            card is not Thing { isDestroyed: false, IsEquipmentOrRangedOrAmmo: true } item ||
            !CardCache.Contains(item)) return;
        var delta = new CardEnhancementDelta { Card = item, Level = item.encLV };
        if (CharaProgressCompleteEvent.ShouldPack(false)) CharaProgressCompleteEvent.Pack(delta);
        else host.Delta.AddRemote(delta);
        EmpLog.Debug("Equipment enhancement published: item {Uid}, level {Level}, reason {Reason}",
            item.uid, item.encLV, reason);
    }
}
