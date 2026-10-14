using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(ContentQuest), nameof(ContentQuest.SelectQuest))]
internal static class QuestAbandonmentPatch
{
    [HarmonyPostfix]
    internal static void After(ContentQuest __instance, Quest q)
    {
        if (NetSession.Instance.Connection is not ElinNetClient) return;
        // Only replace the user-confirmed abandon action. Expiry and replica
        // callbacks must never become requests to fail a host quest.
        __instance.buttonAbandon.SetOnClick(() => Dialog.YesNo("dialog_abandonQuest", () => QuestAbandonment.Submit(q)));
    }
}
