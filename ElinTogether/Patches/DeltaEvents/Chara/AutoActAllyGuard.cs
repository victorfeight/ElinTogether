using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Guard the assignment entry points BEFORE they HoldCard or mutate AI status.
[HarmonyPatch]
internal static class AutoActAllyGuard
{
    [HarmonyPrepare]
    internal static bool Prepare() => TargetMethods().Any();

    internal static IEnumerable<MethodBase> TargetMethods()
    {
        var type = AccessTools.TypeByName("AutoActAllyExpansion.Patches.AutoAct_Patch");
        if (type is null) yield break;
        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)) {
            if (method.ReturnType == typeof(void) && method.Name.StartsWith("TrySetAutoAct") &&
                method.GetParameters().FirstOrDefault()?.ParameterType == typeof(Chara)) yield return method;
        }
    }

    [HarmonyPrefix]
    internal static bool Before(Chara chara)
    {
        return NetSession.Instance.Connection switch {
            ElinNetClient => false, // NPC simulation belongs to host; local player's Auto Act is separate.
            ElinNetHost host => !host.ActiveRemoteCharas.Values.Contains(chara),
            _ => true,
        };
    }
}
