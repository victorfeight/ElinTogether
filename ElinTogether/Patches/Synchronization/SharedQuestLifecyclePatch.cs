using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class SharedQuestLifecyclePatch
{
    // Include mod overrides and callbacks, but not presentation getters or
    // initialization/deserialization. Scope authority to random quests.
    internal static IEnumerable<MethodBase> TargetMethods() => QuestExpansionCompat.Types
        .Where(t => typeof(Quest).IsAssignableFrom(t) || typeof(QuestTask).IsAssignableFrom(t))
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(m => !m.IsAbstract && !m.ContainsGenericParameters &&
            (m.Name is "Fail" or "CompleteTask" or "OnEnterZone" or "OnFail" or "OnComplete" or "OnCompleteTask" or "OnKillChara" or "OnGiveItem" or "OnModKarma" or "OnStart" or "OnChangePhase" ||
             (m.DeclaringType?.Assembly == QuestExpansionCompat.Assembly && m.Name.StartsWith("On", StringComparison.Ordinal) && m.Name != "OnInit" && m.Name != "OnGetDetail")))
        .Distinct();

    [HarmonyPrefix]
    internal static bool Before(object __instance) => NetSession.Instance.Connection is not ElinNetClient ||
        !(__instance is Quest q ? RandomQuestStateSnapshot.Includes(q) :
          __instance is QuestTask task && task.owner != null && RandomQuestStateSnapshot.Includes(task.owner));

    [HarmonyPostfix]
    internal static void After(object __instance)
    {
        var quest = __instance as Quest ?? (__instance as QuestTask)?.owner;
        if (quest != null && RandomQuestStateSnapshot.Includes(quest)) RandomQuestStateSnapshot.MarkDirty();
    }
}

[HarmonyPatch]
internal static class SharedQuestMembershipPatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => new[] { "Start", "Remove", "RemoveGlobal" }
        .Select(n => AccessTools.Method(typeof(QuestManager), n, [typeof(Quest)]));
    [HarmonyPostfix]
    internal static void After() => RandomQuestStateSnapshot.MarkDirty();
}

[HarmonyPatch]
internal static class SharedQuestEventAuthorityPatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => QuestExpansionCompat.Types
        .Where(t => t == typeof(ZoneEvent) || typeof(ZoneEventQuest).IsAssignableFrom(t) ||
            (t.Assembly == QuestExpansionCompat.Assembly && typeof(ZoneEvent).IsAssignableFrom(t)))
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(m => !m.IsAbstract && m.Name is "Tick" or "OnVisit" or "OnLeaveZone" or "OnCharaDie" or "OnTickRound" or "_OnTickRound" or "OnFirstTick" or "OnKill")
        .Distinct();
    [HarmonyPrefix]
    internal static bool Before(ZoneEvent __instance) => NetSession.Instance.Connection is not ElinNetClient ||
        !QuestExpansionCompat.IsQuestEvent(__instance);
    [HarmonyPostfix]
    internal static void After(ZoneEvent __instance, MethodBase __originalMethod)
    {
        if (QuestExpansionCompat.IsQuestEvent(__instance) && __originalMethod.Name != "Tick") RandomQuestStateSnapshot.MarkDirty();
    }
}

[HarmonyPatch(typeof(Zone), nameof(Zone.ModInfluence))]
internal static class SharedGuildZoneInfluencePatch
{
    [HarmonyPostfix]
    internal static void After() => GuildStateSnapshot.MarkDirty();
}
