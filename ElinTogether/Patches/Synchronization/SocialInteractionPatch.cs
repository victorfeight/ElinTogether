using System;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Affinity), nameof(Affinity.OnTalkRumor))]
internal static class SocialTalkEvent
{
    [HarmonyPrefix]
    internal static bool Before(out SocialInteractions.Context? __state)
    {
        __state = null;
        if (SocialInteractions.Active || ElinDelta.IsApplying || Affinity.CC is not { } target) return true;
        if (NetSession.Instance.Connection is ElinNetHost) {
            __state = SocialInteractions.Begin(EClass.pc, target);
            return true;
        }
        if (NetSession.Instance.Connection is not ElinNetClient client) return true;
        // AutoAct owns its request/completion and bounded interest prediction.
        // Its host execution uses the same SocialInteractions.Talk resolver.
        if (EClass.pc.ai.GetType().FullName == "AutoActMod.Actions.AutoActChat") return true;
        if (!PlayerControl.LocalInputBlocked) client.Delta.AddRemote(new SocialTalkDelta {
            Id = Guid.NewGuid(), ZoneUid = EClass._zone.uid, Actor = EClass.pc, Target = target,
            KnowsFavourites = target.knowFav,
        });
        return false;
    }

    [HarmonyFinalizer]
    internal static void End(SocialInteractions.Context? __state) => __state?.Dispose();
}

// A gift is not permission to replace a human's equipment or auto-sell their
// inventory. AI companions retain the native gift follow-up behavior.
[HarmonyPatch]
internal static class HumanGiftInventoryPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(Chara), nameof(Chara.SetAI))]
    internal static void Task(Chara __instance, AIAct g) => SocialInteractions.CaptureGiftTask(__instance, g);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.TryEquip))]
    internal static bool Equip(Chara __instance) => !SocialInteractions.ProtectGiftRecipient(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.TryClearInventory))]
    internal static bool Clear(Chara __instance) => !SocialInteractions.ProtectGiftRecipient(__instance);
}
