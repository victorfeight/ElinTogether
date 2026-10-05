using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Match the native confirmed name write, not generated lambda names or UI text.
[HarmonyPatch]
internal static class FactionBoardPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        var methods = typeof(TraitFactionBoard).GetNestedTypes(AccessTools.all)
            .Append(typeof(TraitFactionBoard))
            .SelectMany(t => t.GetMethods(AccessTools.all | BindingFlags.DeclaredOnly))
            .Where(m => m.GetMethodBody() != null);
        var rename = 0;
        var open = 0;
        foreach (var method in methods) {
            var codes = PatchProcessor.GetOriginalInstructions(method);
            if (codes.Any(i => i.opcode == OpCodes.Stfld && Equals(i.operand, AccessTools.Field(typeof(Faction), "name")))) {
                if (method.GetParameters().Count(p => p.ParameterType == typeof(string)) != 1)
                    throw new InvalidOperationException("Faction rename arguments changed");
                rename++;
                yield return method;
            } else if (codes.Any(i => i.operand is MethodInfo m && m.DeclaringType == typeof(LayerList) && m.Name == "SetStringList")) {
                open++;
                yield return method;
            }
        }
        if (rename != 1 || open != 1) throw new InvalidOperationException("Faction board callbacks changed");
    }

    [HarmonyPrefix]
    internal static bool Before(MethodBase __originalMethod, object[] __args)
    {
        if (NetSession.Instance.Connection == null) return true;
        var parameters = __originalMethod.GetParameters();
        var nameIndex = Array.FindIndex(parameters, p => p.ParameterType == typeof(string));
        BranchBoardManagement.Submit(new() {
            Operation = nameIndex < 0 ? BranchBoardOperation.Refresh : BranchBoardOperation.RenameFaction,
            Name = nameIndex < 0 ? null : __args[nameIndex] as string,
        });
        return nameIndex < 0;
    }
}
