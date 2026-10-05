using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Quest), nameof(Quest.Complete))]
internal static class QuestCompleteEvent
{
    [HarmonyPrefix]
    internal static bool OnClientComplete(Quest __instance)
    {
        return NetSession.Instance.Connection is not ElinNetClient;
    }

    [HarmonyPostfix]
    internal static void OnQuestComplete(Quest __instance)
    {
        if (GuildStateSnapshot.IsGuildQuest(__instance)) return;
        if (NetSession.Instance.Connection is not ElinNetHost connection || ElinDelta.IsApplying || !__instance.isComplete) {
            return;
        }

        connection.Delta.AddRemote(new QuestCompleteDelta {
            Uid = __instance.uid,
        });
        if (__instance.AffinityGain != 0 && __instance.DestChara is { IsAliveInCurrentZone: true } recipient)
            connection.Delta.AddRemote(SocialStateDelta.Capture(recipient));
    }
}
