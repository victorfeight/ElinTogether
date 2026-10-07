using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class RelicEquipmentPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(CharaBody), nameof(CharaBody.Equip))]
    internal static bool BeforeEquip(CharaBody __instance, Thing thing, BodySlot? slot, out RelicEquipment.Scope? __state)
    {
        __state = null;
        if (NetSession.Instance.Connection == null || !RelicEquipment.Relevant(__instance, thing, slot)) return true;
        // AI_Equip still moves/picks through existing task synchronization. Only
        // its equipment mutation comes from the completed equipment packet.
        if (RelicEquipment.SkipTaskReplay(__instance.owner)) return false;
        __state = new(__instance.owner);
        return true;
    }

    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(CharaBody), nameof(CharaBody.Unequip), typeof(BodySlot), typeof(bool))]
    internal static void BeforeUnequip(CharaBody __instance, BodySlot slot, out RelicEquipment.Scope? __state) =>
        __state = NetSession.Instance.Connection != null && slot.thing?.c_DNA != null ? new(__instance.owner) : null;

    [HarmonyFinalizer, HarmonyPatch(typeof(CharaBody), nameof(CharaBody.Equip))]
    [HarmonyPatch(typeof(CharaBody), nameof(CharaBody.Unequip), typeof(BodySlot), typeof(bool))]
    internal static void End(RelicEquipment.Scope? __state) => __state?.Dispose();

    [HarmonyTranspiler, HarmonyPatch(typeof(CharaBody), nameof(CharaBody.IsEquippable))]
    internal static IEnumerable<CodeInstruction> Eligibility(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pc = AccessTools.PropertyGetter(typeof(EClass), nameof(EClass.pc));
        var feat = AccessTools.PropertyGetter(typeof(Card), nameof(Card.feat));
        var indices = Enumerable.Range(0, Math.Max(0, codes.Count - 1))
            .Where(i => codes[i].Calls(pc) && codes[i + 1].Calls(feat)).ToArray();
        if (indices.Length != 1) throw new InvalidOperationException("Unsupported native relic affordability shape.");
        var index = indices[0];
        codes[index].opcode = OpCodes.Ldarg_0;
        codes[index].operand = null;
        codes.Insert(index + 1, new(OpCodes.Call, AccessTools.Method(typeof(RelicEquipmentPatch), nameof(BudgetOwner))));
        return codes;
    }

    internal static Chara BudgetOwner(CharaBody body) => NetSession.Instance.Connection == null ? EClass.pc : body.owner;

    private static bool PersonalUnlock => NetSession.Instance.Connection != null ||
        EClass.pc.GetStr(SoloPlayerProfile.SaveKey) != null;

    // Sorin's story flag belongs to Player, but his reward belongs to Chara.
    // Keep the native conversation/reward; decide its availability per character.
    [HarmonyPrefix, HarmonyPatch(typeof(DramaManager), nameof(DramaManager.CheckIF))]
    internal static bool SorinCondition(string IF, ref bool __result)
    {
        if ((IF != "hasFlag,sorin_relic" && IF != "!hasFlag,sorin_relic") || !PersonalUnlock) return true;
        var unlocked = EClass.pc.body.GetSlot(RelicEquipment.Slot, onlyEmpty: false) != null;
        __result = IF[0] == '!' ? !unlocked : unlocked;
        return false;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(TraitSorin), nameof(TraitSorin.ShouldShowQuestIcon))]
    internal static void SorinIcon(ref bool __result)
    {
        if (PersonalUnlock) __result = EClass.pc.body.GetSlot(RelicEquipment.Slot, onlyEmpty: false) == null;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(DramaOutcome), nameof(DramaOutcome.reward_sorin))]
    internal static void Reward() => RelicEquipment.PublishState(EClass.pc);

    [HarmonyPostfix, HarmonyPatch(typeof(Chara), nameof(Chara.OnSleep), typeof(int), typeof(int), typeof(bool))]
    internal static void Slept(Chara __instance)
    {
        // Saved humans acting as companions do not satisfy vanilla IsPC. They
        // still own relics and must receive the same unlock after actual sleep.
        if (NetSession.Instance.Connection is ElinNetHost && __instance.GetStr(SoloPlayerProfile.SaveKey) != null) {
            foreach (var slot in __instance.body.slots)
                if (slot.thing?.c_DNA != null) slot.thing.SetBool(RelicEquipment.SleepLock, false);
        }
        if (__instance.body.GetSlot(RelicEquipment.Slot, onlyEmpty: false) != null)
            RelicEquipment.PublishState(__instance);
    }
}
