using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class CodexEvents
{
    // Auto-collection runs inside Pick before an explicit book request can arrive.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.Pick))]
    private static void BeforePick(Chara __instance, out Chara? __state)
    {
        __state = PersonalMsgSayPatch.CodexCollector;
        PersonalMsgSayPatch.CodexCollector = __instance;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Chara), nameof(Chara.Pick))]
    private static void AfterPick(Chara? __state)
    {
        PersonalMsgSayPatch.CodexCollector = __state;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ContentCodex), nameof(ContentCodex.OnClickGetCard))]
    private static bool Withdraw(ContentCodex __instance)
    {
        if (NetSession.Instance.Connection is null) return true;
        if (__instance.currentCodex is { } entry) {
            new CodexRequestDelta {
                Action = CodexRequestDelta.Operation.Withdraw, Species = entry.id,
            }.Submit();
        }
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ContentCodex), nameof(ContentCodex.OnClickAddCards))]
    private static bool CollectInventory()
    {
        if (NetSession.Instance.Connection is null) return true;
        new CodexRequestDelta { Action = CodexRequestDelta.Operation.CollectInventory }.Submit();
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ContentCodex), nameof(ContentCodex.Collect))]
    private static bool Collect(Thing t)
    {
        if (NetSession.Instance.Connection is not ElinNetClient) return true;
        // Incoming pickups must not credit another local copy of the shared book.
        // Host destruction and absolute counts will arrive through normal deltas.
        if (!ElinDelta.IsApplying) {
            new CodexRequestDelta {
                Action = CodexRequestDelta.Operation.Collect, ThingUid = t.uid,
            }.Submit();
        }
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CodexManager), nameof(CodexManager.AddCard))]
    private static void CountChanged(CodexManager __instance, string id)
    {
        if (__instance == EClass.player.codex) CodexCountDelta.Publish(id);
    }
}
