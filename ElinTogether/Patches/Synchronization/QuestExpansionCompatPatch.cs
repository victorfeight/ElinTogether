using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class QuestExpansionEncounterPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        if (QuestExpansionCompat.Method("ModQuests.Common.QuestRiskyDeliver", "ShouldOverrideEncounter") is { } method) yield return method;
    }
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions) =>
        Transpilers.MethodReplacer(instructions,
            AccessTools.Method(typeof(QuestDeliver), nameof(QuestDeliver.ListDestThing)),
            AccessTools.Method(typeof(QuestExpansionCompat), nameof(QuestExpansionCompat.PartyDeliveryItems)));
}

[HarmonyPatch]
internal static class QuestExpansionMusicPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        if (QuestExpansionCompat.Method("ModQuestEvent.ZoneEventMusicMonster", "_OnTickRound") is { } method) yield return method;
    }
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions) {
            if (instruction.opcode == OpCodes.Isinst && Equals(instruction.operand, typeof(AI_PlayMusic))) {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(QuestExpansionCompat), nameof(QuestExpansionCompat.PartyPerforming));
            }
            yield return instruction;
        }
    }
}

[HarmonyPatch]
internal static class QuestExpansionDramaPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        if (QuestExpansionCompat.Method("General.DramaWrapper", "Release") is { } method) yield return method;
    }
    // Rescue/ambush callbacks change fields directly before releasing the drama.
    [HarmonyPostfix]
    internal static void After() => RandomQuestStateSnapshot.MarkDirty();
}

[HarmonyPatch]
internal static class QuestExpansionPreEnterPatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => (QuestExpansionCompat.Assembly?.GetTypes() ?? [])
        .Where(t => typeof(ZonePreEnterEvent).IsAssignableFrom(t))
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(m => m.Name == "Execute" && !m.IsAbstract);
    [HarmonyPrefix]
    internal static bool Before() => NetSession.Instance.Connection is not ElinNetClient;
    [HarmonyPostfix]
    internal static void After() => RandomQuestStateSnapshot.MarkDirty();
}

[HarmonyPatch]
internal static class QuestExpansionThiefRewardPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        if (QuestExpansionCompat.Method("ModQuests.ThiefGuild.QuestTGRiskyEscort", "OnDropReward") is { } method) yield return method;
    }
    // Correct the mod's mistaken destination, preserving its native reward flow.
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions) =>
        Transpilers.MethodReplacer(instructions,
            QuestExpansionCompat.Method("General.General", "get_MerchantGuildZone"),
            QuestExpansionCompat.Method("General.General", "get_ThiefGuildZone"));
}
