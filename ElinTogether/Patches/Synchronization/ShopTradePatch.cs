using ElinTogether.Models;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class ShopTradePatch
{
    [HarmonyPrefix, HarmonyPatch(typeof(ShopTransaction), nameof(ShopTransaction.OnEndTransaction))]
    internal static bool Close(ShopTransaction __instance) => !ShopTrade.CloseClient(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(ShopTransaction), nameof(ShopTransaction.OnEndTransaction))]
    internal static void HostClose(ShopTransaction __instance) => ShopTrade.PublishHostClose(__instance);

    [HarmonyPrefix, HarmonyPatch(typeof(DragItemCard), nameof(DragItemCard.OnDrag))]
    internal static bool FinishPaidDrag(ref bool __result)
    {
        if (!ShopTrade.FinishingDrag) return true;
        __result = true;
        return false;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(InvOwner.Transaction), nameof(InvOwner.Transaction.FreeTrade), MethodType.Getter)]
    internal static void FreeTrade(InvOwner.Transaction __instance, ref bool __result)
    {
        if (ShopTrade.Replaying == __instance) __result = true;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(InvOwner.Transaction), nameof(InvOwner.Transaction.IsValid))]
    internal static bool IsValid(InvOwner.Transaction __instance, ref bool __result)
    {
        if (ShopTrade.Replaying != __instance) return true;
        __result = true;
        return false;
    }

}
