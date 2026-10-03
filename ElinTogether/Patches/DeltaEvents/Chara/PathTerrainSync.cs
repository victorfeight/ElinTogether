using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Capture only native path destruction, including recursive dependent-ramp mining.
// Ordinary mining/building keep their existing action replay protocols.
[HarmonyPatch]
internal static class PathTerrainSync
{
    private static Map? _capturing;

    [HarmonyPrefix, HarmonyPatch(typeof(Chara), nameof(Chara.DestroyPath))]
    internal static bool Begin(out Map? __state)
    {
        __state = _capturing;
        if (NetSession.Instance.Connection is ElinNetClient) return false;
        if (NetSession.Instance.Connection is ElinNetHost) _capturing = EClass._map;
        return true;
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(Chara), nameof(Chara.DestroyPath))]
    internal static void End(Map? __state) => _capturing = __state;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Map), nameof(Map.SetBlock), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int))]
    internal static void Block(Map __instance, int x, int z)
    {
        if (_capturing != __instance || NetSession.Instance.Connection is not ElinNetHost) return;
        var cell = __instance.cells[x, z];
        BossLootTerrainPatch.Send(new BossLootTileDelta {
            ZoneUid = EClass._zone.uid, Pos = new Point(x, z), Block = true,
            Material = cell._blockMat, Id = cell._block, Direction = cell.blockDir,
        });
        EmpLog.Debug("Path terrain cleared block in zone {ZoneUid} at {X},{Z}", EClass._zone.uid, x, z);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Map), nameof(Map.SetObj), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool))]
    internal static void Object(Map __instance, int x, int z)
    {
        if (_capturing != __instance || NetSession.Instance.Connection is not ElinNetHost) return;
        var cell = __instance.cells[x, z];
        BossLootTerrainPatch.Send(new BossLootTileDelta {
            ZoneUid = EClass._zone.uid, Pos = new Point(x, z), Block = false,
            Id = cell.obj, Value = cell.objVal, Direction = cell.objDir,
        });
    }
}
