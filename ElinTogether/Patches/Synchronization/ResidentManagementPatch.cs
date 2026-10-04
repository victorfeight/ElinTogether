using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Match native operations rather than compiler-generated callback names.
[HarmonyPatch]
internal static class ResidentManagementPatch
{
    private static readonly Dictionary<MethodBase, (ResidentOperation Operation, FieldInfo[] Path)> Callbacks = [];

    // The cooldown closure captures an outer OnClick closure; the maid callback
    // captures the resident directly. Resolve and validate both shapes at patch time.
    internal static FieldInfo[] ResidentPath(Type closure)
    {
        IEnumerable<FieldInfo[]> Find(Type type, HashSet<Type> seen)
        {
            if (!seen.Add(type)) yield break;
            foreach (var field in type.GetFields(AccessTools.all).Where(f => !f.IsStatic)) {
                if (field.FieldType == typeof(Chara)) yield return [field];
                else if (field.FieldType.DeclaringType == typeof(BaseListPeople))
                    foreach (var tail in Find(field.FieldType, new(seen))) yield return new[] { field }.Concat(tail).ToArray();
            }
        }
        return Find(closure, []).Single();
    }

    internal static IEnumerable<MethodBase> TargetMethods()
    {
        var change = AccessTools.Method(typeof(FactionBranch), nameof(FactionBranch.ChangeMemberType));
        var maid = AccessTools.Field(typeof(FactionBranch), nameof(FactionBranch.uidMaid));
        var callbacks = typeof(BaseListPeople).GetNestedTypes(AccessTools.all).SelectMany(t => t.GetMethods(AccessTools.all | BindingFlags.DeclaredOnly))
            .Where(m => m.GetMethodBody() != null).ToArray();
        foreach (var operation in new[] { ResidentOperation.MemberType, ResidentOperation.Maid }) {
            var matches = callbacks.Where(m => PatchProcessor.GetOriginalInstructions(m).Any(i =>
                operation == ResidentOperation.MemberType ? i.Calls(change) : i.opcode == OpCodes.Stfld && Equals(i.operand, maid))).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Resident board callback changed: {operation}.");
            Callbacks[matches[0]] = (operation, ResidentPath(matches[0].DeclaringType!));
            yield return matches[0];
        }
    }

    [HarmonyPrefix]
    internal static bool Click(object __instance, MethodBase __originalMethod)
    {
        if (NetSession.Instance.Connection == null) return true;
        var callback = Callbacks[__originalMethod];
        object value = __instance;
        foreach (var field in callback.Path) value = field.GetValue(value)!;
        ResidentManagement.Submit((Chara)value, callback.Operation);
        return false; // Includes the vanilla callback's timer write: host owns the whole operation.
    }
}

[HarmonyPatch(typeof(FactionBranch), nameof(FactionBranch.ChangeMemberType))]
internal static class ResidentMemberTypePatch
{
    [HarmonyPostfix]
    internal static void Changed(FactionBranch __instance, Chara c)
    {
        if (NetSession.Instance.Connection is ElinNetHost host && __instance.owner == EClass._zone &&
            EClass._zone.IsPCFaction && !PartyMembership.Protected(c))
            host.Delta.AddRemote(ResidentStateDelta.Capture(__instance, c, clearBed: true));
    }
}
