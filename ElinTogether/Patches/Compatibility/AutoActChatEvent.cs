using System;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Leave the local dialogue UI in Auto Act. Resolve conversation rolls/experience
// on host; bounded interest prediction lets its synchronous UI loop terminate.
[HarmonyPatch(typeof(Affinity), nameof(Affinity.OnTalkRumor))]
internal static class AutoActChatEvent
{
    [HarmonyPrefix]
    internal static bool Before()
    {
        if (NetSession.Instance.Connection is not ElinNetClient client || ElinDelta.IsApplying ||
            EClass.pc.ai.GetType().FullName != "AutoActMod.Actions.AutoActChat" || Affinity.CC is not { } target) return true;
        client.Delta.AddRemote(new AutoActStepDelta {
            RequestId = Guid.NewGuid(), Owner = EClass.pc, Kind = AutoActStepDelta.Step.Chat,
            Pos = target.pos, Target = target, ZoneUid = EClass._zone.uid,
        });
        target.interest -= 10;
        return false;
    }
}

// Auto Act wakes a sleeping conversation target by calling Card.Kick directly.
[HarmonyPatch(typeof(Card), nameof(Card.Kick), new[] { typeof(Chara), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
internal static class AutoActChatKickEvent
{
    [HarmonyPrefix]
    internal static bool Before(Card __instance, Chara t)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client || ElinDelta.IsApplying ||
            !__instance.IsPC || EClass.pc.ai.GetType().FullName != "AutoActMod.Actions.AutoActChat") return true;
        client.Delta.AddRemote(new CharaActPerformDelta {
            ActId = ABILITY.ActKick, Owner = __instance, TargetCard = t, Pos = t.pos,
        });
        return false;
    }
}
