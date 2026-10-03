using System;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Chara), nameof(Chara.GiveGift))]
internal static class CharaGiveGiftEvent
{
    [HarmonyPrefix]
    internal static bool OnGiveGift(Chara __instance, Chara c, Thing t, out SocialInteractions.Context? __state)
    {
        __state = null;
        if (NetSession.Instance.Connection is not { } connection) {
            return true;
        }
        if (connection.IsClient && ElinDelta.IsApplying) return false;
        if (SocialInteractions.Active || ElinDelta.IsApplying) return true;
        if (connection.IsHost) {
            if (__instance.IsPC) __state = SocialInteractions.Begin(__instance, c, gift: true);
            return true;
        }
        if (__instance.IsPC && !PlayerControl.LocalInputBlocked) {
            var source = PendingSplit.Split(t);
            if (source is not null && !PendingUid.IsPending(source.Uid)) {
                connection.Delta.AddRemote(new CharaGiveGiftDelta {
                    Id = Guid.NewGuid(), ZoneUid = EClass._zone.uid,
                    From = __instance, To = c, Thing = source,
                });
                // The host splits the real stack. This UI-only copy must not
                // enter the recipient's inventory or run gift effects locally.
                if (PendingUid.IsPending(t.uid)) CardCache.DelayDestroy(t);
            }
        }
        return false;
    }

    [HarmonyFinalizer]
    internal static void End(SocialInteractions.Context? __state) => __state?.Dispose();

}
