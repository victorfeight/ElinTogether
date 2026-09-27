using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Net;
using EModding.Helper;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class LocalHallucinationPatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => [
        AccessTools.Method(typeof(ConHallucination), nameof(ConHallucination.SetOwner)),
        AccessTools.Method(typeof(ConHallucination), nameof(ConHallucination.OnRemoved)),
    ];

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> UseLocalViewer(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Card), nameof(Card._IsPC))))
            .EnsureValid("hallucination local viewer")
            .SetInstruction(new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(LocalHallucinationPatch), nameof(IsLocalViewer))))
            .InstructionEnumeration();
    }

    internal static bool IsLocalViewer(Card owner) => NetSession.Instance.HasActiveConnection
        ? owner.IsPC : owner._IsPC;

    [ElinPostSceneInit]
    internal static void RefreshAfterSceneInit(Scene.Mode mode)
    {
        if (!NetSession.Instance.HasActiveConnection || mode == Scene.Mode.Title ||
            EClass.game?.player?.chara is not { } pc) return;

        // Save deserialization can precede selection of the client's character.
        // Derive the process-local visual state from the final local condition.
        if (!pc.HasCondition<ConHallucination>()) Player.seedHallucination = 0;
        else if (Player.seedHallucination == 0) Player.seedHallucination = EClass.rnd(10000) + 1;
    }
}
