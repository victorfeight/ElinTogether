using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Chara), nameof(Chara.TryDropBossLoot))]
internal static class BossLootTerrainPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> RouteTerrain(IEnumerable<CodeInstruction> instructions)
    {
        var block = AccessTools.Method(typeof(Point), nameof(Point.SetBlock), [typeof(int), typeof(int)]);
        var obj = AccessTools.Method(typeof(Point), nameof(Point.SetObj), [typeof(int), typeof(int), typeof(int)]);
        var codes = new List<CodeInstruction>(instructions);
        var blocks = 0;
        var objects = 0;
        foreach (var code in codes) {
            if (code.Calls(block)) {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(BossLootTerrainPatch), nameof(SetBlock));
                blocks++;
            } else if (code.Calls(obj)) {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(BossLootTerrainPatch), nameof(SetObj));
                objects++;
            }
        }
        if (blocks != 1 || objects != 1) throw new InvalidOperationException("Boss loot terrain call sites changed");
        return codes;
    }

    internal static void SetBlock(Point point, int material, int id)
    {
        // Keep death messages, quest updates and rewards on their existing paths.
        // Only the client-side terrain mutation is suppressed.
        if (NetSession.Instance.Connection is ElinNetClient) return;
        point.SetBlock(material, id);
        Send(new BossLootTileDelta { ZoneUid = EClass._zone.uid, Pos = point, Block = true, Material = material, Id = id });
    }

    internal static void SetObj(Point point, int id, int value, int dir)
    {
        if (NetSession.Instance.Connection is ElinNetClient) return;
        point.SetObj(id, value, dir);
        Send(new BossLootTileDelta { ZoneUid = EClass._zone.uid, Pos = point, Block = false, Id = id, Value = value, Direction = dir });
    }

    private static void Send(BossLootTileDelta delta)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host) return;
        // Preserve ordering before chest placement, including a kill during progress.
        if (CharaProgressCompleteEvent.ShouldPack(false)) CharaProgressCompleteEvent.Pack(delta);
        else host.Delta.AddRemote(delta);
    }
}
