using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using ElinTogether.Elements;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class PersonalStaminaRecoveryPatch
{
    // Replace only vanilla's one NPC stamina-regeneration call. Do not change
    // its probability/amount or charge food recovery against combat credit.
    [HarmonyTranspiler, HarmonyPatch(typeof(Chara), nameof(Chara.TickConditions))]
    internal static IEnumerable<CodeInstruction> Regen(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mod = AccessTools.Method(typeof(Stats), nameof(Stats.Mod));
        var stamina = AccessTools.PropertyGetter(typeof(Chara), nameof(Chara.stamina));
        var random = AccessTools.Method(typeof(EClass), nameof(EClass.rnd), [typeof(int)]);
        var matches = Enumerable.Range(5, Math.Max(0, codes.Count - 5)).Where(i =>
            codes[i - 5].Calls(stamina) && codes[i - 4].opcode == OpCodes.Ldc_I4_5 &&
            codes[i - 3].Calls(random) && codes[i - 2].opcode == OpCodes.Ldc_I4_1 &&
            codes[i - 1].opcode == OpCodes.Add && codes[i].Calls(mod)).Select(i => codes[i]).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Unsupported native NPC stamina regeneration shape.");
        matches[0].opcode = OpCodes.Call;
        matches[0].operand = AccessTools.Method(typeof(PersonalStaminaRecoveryPatch), nameof(RecoverNpc));
        return codes;
    }

    internal static void RecoverNpc(Stats stamina, int amount)
    {
        var actor = BaseStats.CC;
        var before = stamina.value;
        stamina.Mod(amount);
        // Remote human ticks are mirrors, not AI ownership. Only the machine
        // actually running a companion spends its saved recovery allowance.
        if (NetSession.Instance.Connection is not ElinNetClient && !actor.IsPC && actor.ai is not GoalRemote)
            PersonalStaminaRecovery.RecoveredByCompanion(actor, actor.stamina.value - before);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Chara), nameof(Chara.OnSleep), typeof(int), typeof(int), typeof(bool))]
    internal static void Slept(Chara __instance)
    {
        if (!__instance.IsPC && NetSession.Instance.Connection is not ElinNetClient &&
            __instance.GetInt(PersonalStaminaRecovery.Key, -1) >= 0)
            PersonalStaminaRecovery.Store(__instance, 0);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Chara), nameof(Chara.Revive))]
    internal static void Reviving(Chara __instance)
    {
        if (__instance.isDead && (ReferenceEquals(__instance, EClass.pc) ||
            __instance.GetInt(PersonalStaminaRecovery.Key, -1) >= 0)) PersonalStaminaRecovery.Mirror(__instance, 0);
    }
}
