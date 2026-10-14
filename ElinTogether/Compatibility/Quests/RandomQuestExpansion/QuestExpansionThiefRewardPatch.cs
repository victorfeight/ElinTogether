using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Standalone upstream reward defect, retained for compatibility with the installed
// mod. Keep separate from MP rules; remove when the supported mod fixes it.
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
