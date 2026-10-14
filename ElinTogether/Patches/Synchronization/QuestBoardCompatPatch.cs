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

[HarmonyPatch(typeof(Quest), nameof(Quest.Start))]
internal static class QuestSkillOwnerStartPatch
{
    [HarmonyPrefix]
    internal static void Before(Quest __instance) => QuestSkillOwner.Bind(__instance);
}

[HarmonyPatch]
internal static class QuestSkillActorPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        if (QuestExpansionCompat.BoardMethod("Quests.QuestTimedSkill", "get_Chara") is { } getter) yield return getter;
    }
    [HarmonyPostfix]
    internal static void After(Quest __instance, ref Chara __result)
    {
        if (QuestSkillOwner.Actor(__instance) is { } actor) __result = actor;
    }
}

[HarmonyPatch]
internal static class QuestSkillExecutionPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.Complete));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.Fail));
        if (QuestExpansionCompat.BoardMethod("Quests.QuestTimedSkill", "CheckQuestStatus") is { } status) yield return status;
    }
    [HarmonyPrefix]
    internal static bool Before(Quest __instance, out QuestSkillOwner? __state) => QuestSkillOwner.Enter(__instance, out __state);
    [HarmonyFinalizer]
    internal static void Finalizer(QuestSkillOwner? __state) => __state?.Dispose();
}
