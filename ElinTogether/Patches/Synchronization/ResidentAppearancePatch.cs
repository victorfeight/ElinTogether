using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Models;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class ResidentCosmeticCallbackPatch
{
    private static readonly Dictionary<MethodBase, FieldInfo[]> Paths = [];
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        var name = AccessTools.PropertySetter(typeof(Card), nameof(Card.c_altName));
        var parts = AccessTools.Field(typeof(Chara), nameof(Chara.pccData));
        var callbacks = typeof(BaseListPeople).GetNestedTypes(AccessTools.all)
            .SelectMany(t => t.GetMethods(AccessTools.all | BindingFlags.DeclaredOnly)).Where(m => m.GetMethodBody() != null);
        var foundNames = 0;
        var foundToggles = 0;
        foreach (var method in callbacks) {
            var codes = PatchProcessor.GetOriginalInstructions(method);
            var rename = codes.Any(i => i.Calls(name));
            var toggle = codes.Any(i => i.opcode == OpCodes.Stfld && Equals(i.operand, parts));
            if (!rename && !toggle) continue;
            if (rename) foundNames++;
            if (toggle) foundToggles++;
            Paths[method] = ResidentManagementPatch.ResidentPath(method.DeclaringType!);
            yield return method;
        }
        if (foundNames != 1 || foundToggles != 2) throw new InvalidOperationException("Resident cosmetic callbacks changed.");
    }

    [HarmonyPrefix]
    internal static void Before(object __instance, MethodBase __originalMethod, out ResidentAppearance.Edit? __state)
    {
        object value = __instance;
        foreach (var field in Paths[__originalMethod]) value = field.GetValue(value)!;
        __state = ResidentAppearance.Begin((Chara)value);
    }

    [HarmonyPostfix]
    internal static void After(ResidentAppearance.Edit? __state) => ResidentAppearance.Finish(__state);
}

// Native Kill calls OnKill (which applies the PCC edits) before onKill listeners.
// Listen there instead of capturing temporary previews or polling editor fields.
[HarmonyPatch(typeof(LayerEditSkin), nameof(LayerEditSkin.Activate))]
internal static class ResidentSkinEditorPatch
{
    [HarmonyPrefix] internal static void Before(Chara _chara, out ResidentAppearance.Edit? __state) => __state = ResidentAppearance.Begin(_chara);
    [HarmonyPostfix] internal static void After(LayerEditSkin __instance, ResidentAppearance.Edit? __state)
    { if (__state != null) __instance.SetOnKill(() => ResidentAppearance.Finish(__state)); }
}

[HarmonyPatch(typeof(LayerEditPortrait), nameof(LayerEditPortrait.Activate))]
internal static class ResidentPortraitEditorPatch
{
    [HarmonyPrefix] internal static void Before(Chara _chara, PCCData? _pcc, out ResidentAppearance.Edit? __state) =>
        // A nested PCC portrait editor is committed by the outer PCC editor.
        __state = _pcc == null ? ResidentAppearance.Begin(_chara) : null;
    [HarmonyPostfix] internal static void After(LayerEditPortrait __instance, ResidentAppearance.Edit? __state)
    { if (__state != null) __instance.SetOnKill(() => ResidentAppearance.Finish(__state)); }
}

[HarmonyPatch(typeof(LayerEditPCC), nameof(LayerEditPCC.Activate))]
internal static class ResidentPccEditorPatch
{
    [HarmonyPrefix] internal static void Before(Chara _chara, UIPCC.Mode m, out ResidentAppearance.Edit? __state) =>
        __state = m == UIPCC.Mode.Full && _chara.IsPCC ? ResidentAppearance.Begin(_chara) : null;
    [HarmonyPostfix] internal static void After(LayerEditPCC __instance, ResidentAppearance.Edit? __state)
    { if (__state != null) __instance.SetOnKill(() => ResidentAppearance.Finish(__state)); }
}
