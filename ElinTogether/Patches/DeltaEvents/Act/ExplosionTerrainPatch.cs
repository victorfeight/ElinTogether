using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Explosion visuals/damage retain their existing replay. Only the host mines
// terrain; publish native setter results through the existing scoped capture.
[HarmonyPatch(typeof(ActEffect), nameof(ActEffect.DamageEle))]
internal static class ExplosionTerrainPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> RouteTerrain(IEnumerable<CodeInstruction> instructions)
    {
        var block = AccessTools.Method(typeof(Map), nameof(Map.MineBlock));
        var obj = AccessTools.Method(typeof(Map), nameof(Map.MineObj));
        var codes = new List<CodeInstruction>(instructions);
        var blocks = 0;
        var objects = 0;
        foreach (var code in codes) {
            if (code.Calls(block)) {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(ExplosionTerrainPatch), nameof(MineBlock));
                blocks++;
            } else if (code.Calls(obj)) {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(ExplosionTerrainPatch), nameof(MineObj));
                objects++;
            }
        }
        if (blocks != 1 || objects != 1) throw new InvalidOperationException("Explosion terrain call sites changed");
        return codes;
    }

    internal static void MineBlock(Map map, Point point, bool recoverBlock, Chara c, bool mineObj) =>
        Mine(map, point, () => map.MineBlock(point, recoverBlock, c, mineObj));

    internal static void MineObj(Map map, Point point, Task task, Chara c) =>
        Mine(map, point, () => map.MineObj(point, task, c));

    private static void Mine(Map map, Point point, Action mine)
    {
        if (NetSession.Instance.Connection is ElinNetClient) return;
        if (NetSession.Instance.Connection is not ElinNetHost) {
            mine();
            return;
        }
        var end = PathTerrainSync.CaptureMap(map);
        try {
            mine();
            EmpLog.Debug("Explosion terrain mined in zone {ZoneUid} at {X},{Z}", EClass._zone.uid, point.x, point.z);
        } finally {
            end();
        }
    }
}
