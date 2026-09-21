using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(TraitParcel), nameof(TraitParcel.OnUse))]
internal static class TraitParcelUserPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> UseActualUser(IEnumerable<CodeInstruction> instructions)
    {
        var pcGetter = AccessTools.PropertyGetter(typeof(EClass), nameof(EClass.pc));
        var replacements = 0;

        foreach (var instruction in instructions) {
            if (instruction.Calls(pcGetter)) {
                // TraitParcel.OnUse receives the character using the parcel, but vanilla
                // routes its message and contents through the local EClass.pc instead.
                instruction.opcode = OpCodes.Ldarg_1;
                instruction.operand = null;
                replacements++;
            }

            yield return instruction;
        }

        if (replacements != 3) {
            throw new InvalidOperationException($"Expected three EClass.pc accesses in TraitParcel.OnUse, found {replacements}");
        }
    }
}
