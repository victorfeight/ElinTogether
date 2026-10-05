using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Models;
using HarmonyLib;
using UnityEngine;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(AM_MoveInstalled), nameof(AM_MoveInstalled.TryPutAway))]
internal static class ConstructionPutAwayPatch
{
    [HarmonyPrefix] internal static bool Before(AM_MoveInstalled __instance, ref bool __result)
    {
        if (!ElinTogether.Net.NetSession.Instance.IsClient) return true;
        __result = BuildingObjectManagement.SubmitPutAway(__instance.target);
        if (__result) { __instance.target = null; EClass.ui.hud.hint.UpdateText(); }
        return false;
    }
}

[HarmonyPatch(typeof(HitSummary), nameof(HitSummary.Execute))]
internal static class ConstructionPaymentPatch
{
    // Keep native payment/consumption. Only move its UI preference assignment
    // behind a scope check; remote execution must not require or edit host UI.
    [HarmonyTranspiler] internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
    {
        var codes = input.ToList();
        var materials = codes.FindIndex(c => c.operand is FieldInfo { Name: "lastMats" });
        if (materials < 0 || codes.Count(c => c.operand is FieldInfo { Name: "lastMats" }) != 1)
            throw new InvalidOperationException("Native construction material preference assignment changed.");
        var start = codes.FindLastIndex(materials, c => c.opcode == OpCodes.Ldsfld &&
            c.operand is FieldInfo { Name: "Instance", DeclaringType: var type } && type == typeof(BuildMenu));
        var end = codes.FindIndex(materials, c => c.operand is MethodInfo { Name: "set_Item" });
        if (start < 0 || end < 0) throw new InvalidOperationException("Native construction preference boundary changed.");
        var first = new CodeInstruction(OpCodes.Ldarg_0);
        foreach (var code in codes.Skip(start).Take(end - start + 1)) first.labels.AddRange(code.labels);
        codes.RemoveRange(start, end - start + 1);
        codes.InsertRange(start, new[] { first,
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(HitSummary), nameof(HitSummary.recipe))),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ConstructionContext), nameof(ConstructionContext.RecordMaterial))) });
        return codes;
    }
}

[HarmonyPatch(typeof(TaskMoveInstalled), nameof(TaskMoveInstalled.OnProgressComplete))]
internal static class ConstructionMovedItemPatch
{
    [HarmonyPostfix] internal static void After(TaskMoveInstalled __instance)
    {
        if (ElinTogether.Net.NetSession.Instance.Connection is ElinTogether.Net.ElinNetHost host &&
            __instance.target is { isDestroyed: false } target && target.parent == EClass._zone)
            host.Delta.AddRemote(CardConstructionState.Capture(target));
    }
}

[HarmonyPatch(typeof(RecipeCard), nameof(RecipeCard.Build), typeof(Chara), typeof(Card), typeof(Point), typeof(int), typeof(int), typeof(int), typeof(int))]
internal static class ConstructionKeyboardPatch
{
    [HarmonyPostfix] internal static void After(Card t)
    {
        if (ElinTogether.Net.NetSession.Instance.IsHost && CharaProgressCompleteEvent.Action is TaskBuild && !t.isDestroyed)
            CharaProgressCompleteEvent.Pack(CardConstructionState.Capture(t));
    }
    [HarmonyTranspiler] internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
    {
        var key = AccessTools.Method(typeof(Input), nameof(Input.GetKey), new[] { typeof(KeyCode) });
        foreach (var code in input) {
            if (code.Calls(key)) { code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(ConstructionContext), nameof(ConstructionContext.ReadKey)); }
            yield return code;
        }
    }
}
