using ElinTogether.Models;
using ElinTogether.Helper;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class RemoteBackpackCapacityPatch
{
    private static bool IsRemotePlayer(ThingContainer inventory) =>
        NetSession.Instance.Connection is not null && inventory.owner is Chara { IsPlayer: true } actor &&
        !ReferenceEquals(actor, NetSession.Instance.Player);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ThingContainer), nameof(ThingContainer.IsFull), typeof(int))]
    internal static bool IsFull(ThingContainer __instance, int y, ref bool __result)
    {
        if (!IsRemotePlayer(__instance)) return true;
        __result = y == 0 && PlayerBackpackCapacity.Used(__instance) >= __instance.GridSize;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ThingContainer), nameof(ThingContainer.ShouldShowOnGrid))]
    internal static bool ShouldShowOnGrid(ThingContainer __instance, Thing t, ref bool __result)
    {
        if (!IsRemotePlayer(__instance)) return true;
        __result = PlayerBackpackCapacity.UsesSlot(t);
        return false;
    }
}
