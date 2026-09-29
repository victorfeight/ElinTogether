using System;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Chara), nameof(Chara.AddCondition), typeof(Condition), typeof(bool))]
internal static class CharaAddConditionEvent
{
    [HarmonyPostfix]
    internal static void OnCharaAddCondition(Chara __instance, Condition? __result, Condition c, bool force)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host) {
            return;
        }

        host.Delta.AddRemote(new CharaAddConditionDelta {
            Owner = __instance,
            ConditionId = c.id,
            Power = c.power,
            Force = force,
            // Stacking/toggle/nullification can change the family while returning
            // null. The final family also corrects a locally stale resisted status.
            State = ConditionSync.Capture(__instance, c.id),
        });
    }

    [HarmonyPrefix]
    internal static bool OnClientAddCondition()
    {
        // clients cannot add conditions normally
        return NetSession.Instance.IsHost;
    }

    extension(Chara chara)
    {
        [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
        internal Condition Stub_AddCondition(Condition condition, bool force)
        {
            throw new NotImplementedException("Chara.AddCondition");
        }
    }
}
