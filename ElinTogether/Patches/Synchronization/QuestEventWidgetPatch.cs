using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using ElinTogether.Models;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(WidgetDate), nameof(WidgetDate._Refresh))]
internal static class QuestEventWidgetPatch
{
    // Replace just the quest-event text source, preserving the native date
    // widget layout and unrelated zone/event text. No extra widget or timer.
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var zoneGetter = AccessTools.PropertyGetter(typeof(Zone), nameof(Zone.TextWidgetDate));
        var eventGetter = AccessTools.PropertyGetter(typeof(ZoneEvent), nameof(ZoneEvent.TextWidgetDate));
        var zones = 0;
        var events = 0;
        foreach (var code in codes) {
            if (code.Calls(zoneGetter)) {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(QuestEventWidgetSync), nameof(QuestEventWidgetSync.ZoneText));
                zones++;
            } else if (code.Calls(eventGetter)) {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(QuestEventWidgetSync), nameof(QuestEventWidgetSync.EventText));
                events++;
            }
        }
        if (zones != 1 || events != 1) throw new InvalidOperationException("Quest event widget text call sites changed.");
        return codes;
    }
}
