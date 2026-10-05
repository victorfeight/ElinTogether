using System.Collections.Generic;
using System.Reflection;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Held placement already replays CharaBuildDelta. Native menu/NPC construction
// has no held card, so relay its exact setters instead of building twice.
[HarmonyPatch]
internal static class ConstructionTerrainPatch
{
    internal static bool Capture => NetSession.Instance.IsHost && (ConstructionContext.Current is { Terrain: true } || CharaProgressCompleteEvent.Action is TaskBuild { held: null });
    internal static void Publish(ConstructionTerrainDelta delta)
    {
        if (CharaProgressCompleteEvent.Action is TaskBuild) CharaProgressCompleteEvent.Pack(delta);
        else if (NetSession.Instance.Connection is ElinNetHost host) host.Delta.AddRemote(delta);
    }
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in typeof(Map).GetMethods(AccessTools.all | BindingFlags.DeclaredOnly)) {
            var n = method.GetParameters().Length;
            if (method.Name is "SetBlock" or "SetFloor" && n == 5 || method.Name == "SetObj" && n == 7 ||
                method.Name is "SetBridge" or "SetRoofBlock" or "SetDeco" or "SetLiquid") yield return method;
        }
    }
    [HarmonyPostfix] internal static void After(Map __instance, int x, int z, MethodBase __originalMethod)
    {
        if (!Capture || __instance != EClass._map) return;
        var kind = __originalMethod.Name switch {
            "SetBlock" => ConstructionTerrainKind.Block, "SetFloor" => ConstructionTerrainKind.Floor,
            "SetObj" => ConstructionTerrainKind.Object, "SetBridge" => ConstructionTerrainKind.Bridge,
            "SetRoofBlock" => ConstructionTerrainKind.Roof, "SetDeco" => ConstructionTerrainKind.Deco,
            _ => ConstructionTerrainKind.Liquid,
        };
        Publish(ConstructionTerrainDelta.Capture(new Point(x, z), kind));
    }
}

// MineBlock removes roofs by writing the cell field directly, unlike walls.
[HarmonyPatch(typeof(Map), nameof(Map.MineBlock))]
internal static class ConstructionRoofRemovalPatch
{
    [HarmonyPrefix] internal static void Before(Point point, out bool? __state) =>
        __state = ConstructionTerrainPatch.Capture && point.IsValid ? ActionMode.Mine.IsRoofEditMode() && point.cell._roofBlock != 0 : null;
    [HarmonyPostfix] internal static void After(Map __instance, Point point, bool? __state)
    {
        if (__state.HasValue && __instance == EClass._map)
            ConstructionTerrainPatch.Publish(ConstructionTerrainDelta.Capture(point, __state.Value ? ConstructionTerrainKind.Roof : ConstructionTerrainKind.Rotation));
    }
}

[HarmonyPatch(typeof(Map), nameof(Map.MineObj))]
internal static class ConstructionGatherResultPatch
{
    [HarmonyPostfix] internal static void After(Map __instance, Point point)
    {
        // Native MineObj clears gatherCount after SetObj, or sets isHarvested
        // directly. Send the final result rather than repeating native drops.
        if (ConstructionTerrainPatch.Capture && __instance == EClass._map && point.IsValid)
            ConstructionTerrainPatch.Publish(ConstructionTerrainDelta.Capture(point, ConstructionTerrainKind.Object));
    }
}

[HarmonyPatch(typeof(RecipeBridgePillar), nameof(RecipeBridgePillar.Build), typeof(Chara), typeof(Card), typeof(Point), typeof(int), typeof(int), typeof(int), typeof(int))]
internal static class ConstructionPillarPatch
{
    [HarmonyPostfix] internal static void After(Point pos)
    {
        if (ConstructionTerrainPatch.Capture) CharaProgressCompleteEvent.Pack(ConstructionTerrainDelta.Capture(pos, ConstructionTerrainKind.Pillar));
    }
}

[HarmonyPatch(typeof(TaskBuild), nameof(TaskBuild.OnProgressComplete))]
internal static class ConstructionRotationPatch
{
    internal sealed record BeforeTile(Map Map, Point Position, int BlockDirection, int RoofDirection, int ObjectDirection, bool Modified);
    [HarmonyPrefix] internal static void Before(TaskBuild __instance, out BeforeTile? __state)
    {
        __state = NetSession.Instance.IsHost && __instance.held == null && __instance.pos.IsValid
            ? new(EClass._map, __instance.pos.Copy(), __instance.pos.cell.blockDir, __instance.pos.cell._roofBlockDir, __instance.pos.cell.objDir, __instance.pos.cell.isModified) : null;
    }
    [HarmonyPostfix] internal static void After(BeforeTile? __state)
    {
        // Postfix runs before the existing progress finalizer publishes its
        // collected results. Cover native corner rotation's direct field writes.
        if (__state == null || __state.Map != EClass._map) return;
        var cell = __state.Position.cell;
        if (cell.blockDir != __state.BlockDirection || cell._roofBlockDir != __state.RoofDirection ||
            cell.objDir != __state.ObjectDirection || cell.isModified != __state.Modified)
            CharaProgressCompleteEvent.Pack(ConstructionTerrainDelta.Capture(__state.Position, ConstructionTerrainKind.Rotation));
    }
}
