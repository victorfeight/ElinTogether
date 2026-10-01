using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Trait), nameof(Trait.Toggle))]
internal static class CardToggleEvent
{
    [HarmonyPrefix]
    internal static void OnToggle(Trait __instance, out (bool wasOn, ScopeExit r) __state)
    {
        __state = (__instance.owner?.isOn ?? false, MsgRelayContext.IncludeSpeaker(__instance.owner));
    }

    [HarmonyPostfix]
    internal static void OnToggleEnd(Trait __instance, bool silent, (bool wasOn, ScopeExit r) __state)
    {
        if (NetSession.Instance.Connection is not { } connection) {
            return;
        }

        var owner = __instance.owner;
        if (owner is null || owner.isOn == __state.wasOn) {
            return;
        }

        if (connection.IsClient) {
            if (ElinDelta.IsApplying) {
                return;
            }

            if (PendingUid.IsPending(owner.uid) || !CardCache.Contains(owner)) {
                return;
            }
        }

        // host relay
        connection.Delta.AddRemote(new CardToggleDelta {
            Card = owner,
            IsOn = owner.isOn,
            Silent = silent,
        });
    }

    [HarmonyFinalizer]
    internal static void OnToggleCleanup((bool wasOn, ScopeExit r) __state)
    {
        __state.r?.Dispose();
    }
}
// Failure checks in Toggle must keep their recipient routing. Only the successful
// presentation is duplicated by the authoritative CardToggleDelta on receivers.
[HarmonyPatch(typeof(Trait), nameof(Trait.PlayToggleEffect))]
internal static class CardToggleEffectMessageEvent
{
    [HarmonyPrefix]
    internal static void Before(out ScopeExit __state) =>
        __state = MsgRelayContext.Suppress(MsgRelayContext.IsRedirecting);

    [HarmonyFinalizer]
    internal static void After(ScopeExit? __state) => __state?.Dispose();
}
