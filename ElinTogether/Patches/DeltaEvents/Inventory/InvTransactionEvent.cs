using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(InvOwner.Transaction), nameof(InvOwner.Transaction.Process))]
internal static class InvTransactionEvent
{
    [HarmonyPrefix]
    internal static bool OnTransactionProcess(InvOwner.Transaction __instance, bool startTransaction)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client || ElinDelta.IsApplying) {
            return true;
        }

        if (ShopTrade.Submit(__instance, startTransaction)) return false;

        // ability fake card
        if (__instance.thing.trait is TraitAbility) {
            return true;
        }

        // These slots select an existing item. Do not detach equipped gear on
        // the host merely to select it; their handlers submit validated actions.
        if (__instance.destInv is InvOwnerEffect or InvOwnerBlend) {
            return true;
        }

        if (!CardCache.Contains(__instance.thing) && !CardCache.TryAdopt(__instance.thing)) {
            EmpLog.Warning("Refusing transaction of uncached thing {Uid}", __instance.thing.uid);
            return false;
        }

        if (__instance.thing.parent is null) {
            return true;
        }

        // check so replay can be canceled
        InvOwner.Transaction.error = new() {
            card = __instance.thing,
        };
        if (!__instance.IsValid()) {
            return true;
        }

        ThingRequest
            .Create(__instance.thing, __instance.num)
            .Send()
            .Then(thing => {
                __instance.thing = thing;
                __instance.Process(startTransaction);
            });

        return false;
    }
}
