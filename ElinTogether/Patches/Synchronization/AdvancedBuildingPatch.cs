using System;
using System.Collections.Generic;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// These UI operations have no host transaction yet. Block before native task
// creation/payment, not in Task.Destroy: destruction can return stored materials.
internal static class AdvancedBuildingAccess
{
    internal static bool IsHostControlled(ActionMode mode)
    {
        if (mode is AM_CreateArea or AM_ExpandArea or AM_EditArea or
            AM_RemoveDesignation or AM_Deconstruct or AM_Copy) return true;
        for (Type? type = mode.GetType(); type != null; type = type.BaseType)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AM_Designation<>)) return true;
        return false;
    }

    internal static bool Allow()
    {
        if (NetSession.Instance.Connection is not ElinNetClient) return true;
        Msg.Say("Advanced building and area editing are currently controlled by the host.");
        return false;
    }
}

[HarmonyPatch(typeof(BaseTileSelector), nameof(BaseTileSelector.TryProcessTiles))]
internal static class AdvancedBuildingSelectionPatch
{
    [HarmonyPrefix] internal static bool Before(BaseTileSelector __instance)
    {
        if (!AdvancedBuildingAccess.IsHostControlled(__instance.mode) || AdvancedBuildingAccess.Allow()) return true;
        // End the rejected drag without invoking OnSelectEnd/ExecuteSummary.
        __instance.start = null;
        return false;
    }
}

[HarmonyPatch(typeof(UndoManager), nameof(UndoManager.Perform))]
internal static class AdvancedBuildingUndoPatch
{
    [HarmonyPrefix] internal static bool Before() => AdvancedBuildingAccess.Allow();
}

[HarmonyPatch(typeof(BaseArea), nameof(BaseArea.ListInteractions))]
internal static class AdvancedBuildingAreaMenuPatch
{
    [HarmonyPostfix] internal static void After(List<BaseArea.Interaction> __result)
    {
        // Keep inspection available. Check authority at click time as menus can
        // survive a connection change. Native callbacks include delayed dialogs.
        foreach (var entry in __result) {
            var action = entry.action;
            if (action != null) entry.action = () => { if (AdvancedBuildingAccess.Allow()) action(); };
        }
    }
}
