using System.Linq;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(InvOwnerDeliver), nameof(InvOwnerDeliver._OnProcess))]
internal static class QuestDepositPatch
{
    [HarmonyPrefix]
    internal static bool Before(InvOwnerDeliver __instance, Thing t)
    {
        if (__instance.mode != InvOwnerDeliver.Mode.Crop || NetSession.Instance.Connection is not ElinNetClient) return true;
        if (!ElinDelta.IsApplying) QuestDeposits.Submit(__instance, [t]);
        return false;
    }
    [HarmonyPostfix]
    internal static void After(InvOwnerDeliver __instance)
    {
        if (__instance.mode == InvOwnerDeliver.Mode.Crop) RandomQuestStateSnapshot.MarkDirty();
    }
}

[HarmonyPatch(typeof(LayerDragGrid), nameof(LayerDragGrid.SetInv))]
internal static class QuestBulkDepositPatch
{
    [HarmonyPostfix]
    internal static void After(LayerDragGrid __instance, InvOwnerDraglet owner)
    {
        if (NetSession.Instance.Connection == null || owner is not InvOwnerDeliver { mode: InvOwnerDeliver.Mode.Crop } deliver) return;
        // Vanilla's bulk button bypasses _OnProcess and writes weight directly.
        // Use the same transaction as a single-item deposit on both peers.
        __instance.buttonDeliver.SetOnClick(() => {
            var items = EClass.pc.things.List(t => deliver.ShouldShowGuide(t)).ToArray();
            if (items.Length == 0) SE.BeepSmall();
            else QuestDeposits.Submit(deliver, items);
            __instance.Close();
        });
    }
}
