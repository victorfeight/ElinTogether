using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class ClientQuestRerollPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        var update = AccessTools.Method(typeof(Zone), nameof(Zone.UpdateQuests));
        var callbacks = new[] { typeof(LayerQuestBoard) }.Concat(typeof(LayerQuestBoard).GetNestedTypes(AccessTools.all))
            .SelectMany(t => t.GetMethods(AccessTools.all | BindingFlags.DeclaredOnly)).Where(m => m.GetMethodBody() != null)
            .Where(m => {
                var il = PatchProcessor.GetOriginalInstructions(m);
                return il.Any(i => i.Calls(update)) && il.Any(i => Equals(i.operand, "notEnoughInfluence"));
            }).ToArray();
        if (callbacks.Length != 1) throw new InvalidOperationException("Quest board reroll callback changed.");
        return callbacks;
    }

    [HarmonyPrefix]
    internal static bool Reroll()
    {
        if (NetSession.Instance.Connection is not ElinNetClient) return true;
        Msg.Say("Only the host can reroll the shared quest board.");
        return false; // Guard before spending influence, not inside UpdateQuests.
    }
}
