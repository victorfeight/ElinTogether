using ElinTogether.Models;
using ElinTogether.Helper;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class RemoteBackpackCapacityPatch
{
    private static bool HasRemotePlayerBackpack(ThingContainer inventory) =>
        inventory.owner is Chara actor &&
        // IsPlayer intentionally excludes host-controlled Take a Break actors.
        // Their saved backpack does not become an NPC backpack at that handoff,
        // or when the same saved character is recruited as an offline ally.
        (actor.IsPlayer || actor.GetBool("remote_chara")) &&
        !ReferenceEquals(actor, NetSession.Instance.Connection is null ? EClass.pc : NetSession.Instance.Player);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ThingContainer), nameof(ThingContainer.IsFull), typeof(int))]
    internal static bool IsFull(ThingContainer __instance, int y, ref bool __result)
    {
        if (!HasRemotePlayerBackpack(__instance)) return true;
        __result = y == 0 && PlayerBackpackCapacity.Used(__instance) >= __instance.GridSize;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ThingContainer), nameof(ThingContainer.ShouldShowOnGrid))]
    internal static bool ShouldShowOnGrid(ThingContainer __instance, Thing t, ref bool __result)
    {
        if (!HasRemotePlayerBackpack(__instance)) return true;
        __result = PlayerBackpackCapacity.UsesSlot(t);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ThingContainer), nameof(ThingContainer.IsAlmostFull))]
    internal static bool IsAlmostFull(ThingContainer __instance, int threshold, ref bool __result)
    {
        if (!HasRemotePlayerBackpack(__instance)) return true;
        __result = PlayerBackpackCapacity.Used(__instance) + threshold >= __instance.GridSize;
        return false;
    }
}
