using System.Collections.Generic;
using System.Reflection;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// These mod entry points are Harmony postfixes, not Quest overrides. Guard the
// callbacks themselves: skipping the patched vanilla method cannot skip postfixes.
[HarmonyPatch]
internal static class QuestExpansionWorldMutationPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        if (QuestExpansionCompat.Method("Patch.PlayerPatch", "FriendlyGuildPatch") is { } setup) yield return setup;
        if (QuestExpansionCompat.BoardMethod("Patches.GameDatePatch", "Postfix") is { } hourly) yield return hourly;
    }
    [HarmonyPrefix]
    internal static bool Before() => NetSession.Instance.Connection is not ElinNetClient;
    [HarmonyPostfix]
    internal static void After() => RandomQuestStateSnapshot.MarkDirty();
}
