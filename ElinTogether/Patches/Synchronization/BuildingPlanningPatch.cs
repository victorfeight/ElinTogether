using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class BuildingPlanningDirtyPatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in new[] { typeof(Area), typeof(RoomManager), typeof(BaseArea), typeof(AreaData) })
            foreach (var method in type.GetMethods(AccessTools.all | BindingFlags.DeclaredOnly))
                if (method.Name is "AddPoint" or "RemovePoint" or "AddArea" or "RemoveArea" or "AddRoom" or "RemoveRoom" or
                    "RefreshAll" or "ChangeType" or "SetRandomName" || method.Name.StartsWith("set_")) yield return method;
        foreach (var type in new[] { typeof(TaskMine), typeof(TaskDig), typeof(TaskCut), typeof(TaskHarvest), typeof(TaskBuild), typeof(TaskMoveInstalled) })
            yield return AccessTools.Method(typeof(DesignationList<>).MakeGenericType(type), "OnAdd");
    }
    [HarmonyPostfix] internal static void After() => BuildingPlanning.Dirty();
}

[HarmonyPatch(typeof(Task), nameof(Task.Destroy))]
internal static class BuildingPlanningTaskRemovedPatch
{
    [HarmonyPostfix] internal static void After(Task __instance)
    {
        if (__instance is TaskDesignation && __instance.taskList != null) BuildingPlanning.Dirty();
    }
}

[HarmonyPatch]
internal static class BuildingPlanningDrawPatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => new[] {
        AccessTools.Method(typeof(BaseTileMap), nameof(BaseTileMap.DrawTile)),
        AccessTools.Method(typeof(TileMapElona), nameof(TileMapElona.DrawTile)),
    };
    [HarmonyTranspiler] internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
    {
        var codes = input.ToList();
        var draw = AccessTools.Method(typeof(TaskDesignation), nameof(TaskDesignation.Draw));
        var indices = codes.Select((c, i) => (c, i)).Where(p => p.c.Calls(draw)).Select(p => p.i).ToArray();
        if (indices.Length != 1) throw new InvalidOperationException("Native designation drawing boundary changed.");
        var index = indices[0];
        codes[index].opcode = OpCodes.Call;
        codes[index].operand = AccessTools.Method(typeof(BuildingPlanning), nameof(BuildingPlanning.DrawNative));
        // Both native 'no detail' and 'no designation' branches join here after
        // tile projection and before terrain rendering mutates RenderParam.
        var first = new CodeInstruction(OpCodes.Ldarg_0);
        first.labels.AddRange(codes[index + 1].labels); codes[index + 1].labels.Clear();
        codes.InsertRange(index + 1, new[] { first, new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(BaseTileMap), "param")),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BuildingPlanning), nameof(BuildingPlanning.Draw))) });
        return codes;
    }
}

[HarmonyPatch]
internal static class AreaSettingsCallbackPatch
{
    internal sealed record Edit(BaseArea Area, AreaSettings Before);
    internal static IEnumerable<MethodBase> TargetMethods() => typeof(BaseArea).GetNestedTypes(AccessTools.all).Append(typeof(BaseArea))
        .SelectMany(t => t.GetMethods(AccessTools.all | BindingFlags.DeclaredOnly))
        .Where(m => (m.DeclaringType != typeof(BaseArea) || m.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)) &&
            m.GetMethodBody() != null && PatchProcessor.GetOriginalInstructions(m).Any(i =>
            i.opcode == OpCodes.Stfld && i.operand is FieldInfo { DeclaringType: var declaring, Name: "name" } && declaring == typeof(AreaData) ||
            i.operand is MethodInfo call && call.DeclaringType == typeof(AreaData) && call.Name.StartsWith("set_")));
    [HarmonyPrefix] internal static bool Before(object __instance, out Edit? __state)
    {
        __state = null;
        if (NetSession.Instance.Connection == null) return true;
        if (NetSession.Instance.IsClient && !BuildingPlanning.HasState) { Msg.Say("Waiting for the host's area state."); return false; }
        var area = __instance as BaseArea ?? BranchBoardCallbackPatch.Capture<BaseArea>(__instance, typeof(BaseArea));
        if (!EClass._map.rooms.mapIDs.TryGetValue(area.uid, out var current) || current != area) return false;
        __state = new(area, AreaSettings.Capture(area)); return true;
    }
    [HarmonyPostfix] internal static void After(Edit? __state)
    {
        if (__state == null) return;
        var after = AreaSettings.Capture(__state.Area);
        var fields = __state.Before.Difference(after);
        if (fields == 0) return;
        if (NetSession.Instance.IsClient) {
            __state.Before.Write(__state.Area, fields);
            AreaManagement.Submit(new() { Operation = AreaOperation.Settings, AreaUid = __state.Area.uid, Before = __state.Before, After = after });
        } else BuildingPlanning.Dirty();
    }
}

[HarmonyPatch]
internal static class AreaDeletePatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => new[] { typeof(AM_EditArea), typeof(InspectGroupArea) }
        .SelectMany(t => t.GetNestedTypes(AccessTools.all)).SelectMany(t => t.GetMethods(AccessTools.all | BindingFlags.DeclaredOnly))
        .Where(m => m.GetMethodBody() != null && PatchProcessor.GetOriginalInstructions(m).Any(i => i.Calls(AccessTools.Method(typeof(RoomManager), nameof(RoomManager.RemoveArea)))));
    [HarmonyTranspiler] internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
    {
        foreach (var code in input) {
            if (code.Calls(AccessTools.Method(typeof(RoomManager), nameof(RoomManager.RemoveArea)))) {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(AreaDeletePatch), nameof(Delete));
            }
            yield return code;
        }
    }
    private static void Delete(RoomManager manager, Area area)
    {
        if (NetSession.Instance.IsClient) AreaManagement.Submit(new() { Operation = AreaOperation.Delete, AreaUid = area.uid });
        else manager.RemoveArea(area);
    }
}
