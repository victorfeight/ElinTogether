using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class CardLockStateEvent
{
    // Unlocking happens in AI_OpenLock's progress tick, before completion capture.
    // Capture after the native method also clears hard-lock pricing/lost property.
    [HarmonyPostfix, HarmonyPatch(typeof(Trait), nameof(Trait.OnLockOpen))]
    internal static void Unlocked(Trait __instance) => Publish(__instance.owner, "unlock");

    [HarmonyPrefix, HarmonyPatch(typeof(TraitChestPractice), nameof(TraitChestPractice.OnSimulateHour))]
    internal static void BeforeReset(TraitChestPractice __instance, out int __state) => __state = __instance.owner.c_lockLv;

    [HarmonyPostfix, HarmonyPatch(typeof(TraitChestPractice), nameof(TraitChestPractice.OnSimulateHour))]
    internal static void AfterReset(TraitChestPractice __instance, int __state)
    {
        if (__instance.owner.c_lockLv != __state) Publish(__instance.owner, "practice-reset");
    }

    private static void Publish(Card card, string reason)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host || !EClass.core.IsGameStarted || EClass.game.isLoading ||
            card is not Thing { isDestroyed: false } || !CardCache.Contains(card)) return;
        var delta = CardLockStateDelta.Capture(card);
        if (CharaProgressCompleteEvent.ShouldPack(false)) CharaProgressCompleteEvent.Pack(delta);
        else host.Delta.AddRemote(delta);
        EmpLog.Debug("Chest lock state published: item {Uid}, lock {Lock}, hard {Hard}, lost {Lost}, reason {Reason}",
            card.uid, card.c_lockLv, card.c_lockedHard, card.isLostProperty, reason);
    }
}
