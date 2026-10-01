using System.Collections.Generic;
using System.Reflection.Emit;
using ElinTogether.Net;
using HarmonyLib;
using EModding.Helper;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(AI_Steal), nameof(AI_Steal.IsValidTC))]
internal static class MoongateTheftPatch
{
    // Replace only the theft-specific user-zone gate, never Zone.IsUserZone globally.
    // Keep vanilla target validation and the entire stealing action unchanged.
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> AllowConfiguredTheft(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(new CodeMatch(OpCodes.Callvirt,
                AccessTools.PropertyGetter(typeof(Zone), nameof(Zone.IsUserZone))))
            .EnsureValid("Moongate theft user-zone restriction")
            .SetInstruction(new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(MoongateTheftPatch), nameof(IsTheftBlocked))))
            .InstructionEnumeration();
    }

    internal static bool IsTheftBlocked(Zone zone) => zone.IsUserZone &&
        !(NetSession.Instance.Connection is ElinNetClient
            ? NetSession.Instance.Rules.AllowMoongateTheft
            : EmpConfig.Server.AllowMoongateTheft.Value);
}
