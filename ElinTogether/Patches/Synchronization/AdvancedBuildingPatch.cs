using System;
using ElinTogether.Net;
using ElinTogether.Models;
using HarmonyLib;

namespace ElinTogether.Patches;

// Supported operations are routed below; the remaining UI-only paths must stop
// before native task creation/payment. Never guard Task.Destroy/refunds globally.
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
        Msg.Say("Blueprint imports, house templates, flood fill and editor-only operations are controlled by the host.");
        return false;
    }
}

[HarmonyPatch(typeof(UndoManager), nameof(UndoManager.WriteNote))]
internal static class AdvancedBuildingUndoNotePatch
{
    [HarmonyPrefix] internal static bool Before(UINote n)
    {
        if (!NetSession.Instance.IsClient) return true;
        n.Clear(); n.Space(10); n.AddText("NoteText_topic", "tUndo".lang());
        n.AddText("Cancel your last pending construction or gathering batch here. Completed work is not reversed.");
        n.Build(); return false;
    }
}

[HarmonyPatch(typeof(BaseTileSelector), nameof(BaseTileSelector.TryProcessTiles))]
internal static class AdvancedBuildingSelectionPatch
{
    [HarmonyPrefix] internal static bool Before(BaseTileSelector __instance, Point _end)
    {
        if (NetSession.Instance.IsClient) {
            if (EInput.skipFrame > 0) return false;
            if (__instance.mode is AM_EditArea) return true;
            if (__instance.mode is AM_MoveInstalled { target: null }) return true; // Native target selection only.
            if (BuildingObjectManagement.TrySubmit(__instance.mode, __instance.start ?? _end, _end)) {
                __instance.start = null;
                return false;
            }
            if (__instance.mode is AM_Build build && ConstructionManagement.TrySubmit(build, __instance.start, _end)) {
                __instance.start = null;
                return false;
            }
            if (DesignationManagement.TrySubmit(__instance.mode, __instance.start ?? _end, _end)) {
                __instance.mode.OnSelectEnd(cancel: true);
                __instance.start = null;
                return false;
            }
            AreaCommand? command = __instance.mode switch {
                AM_CreateArea create => new() { Operation = AreaOperation.Create, AreaType = create.area.type.id },
                AM_ExpandArea expand => new() { Operation = expand.shrink ? AreaOperation.Shrink : AreaOperation.Expand, AreaUid = expand.area.uid },
                _ => null,
            };
            if (command != null) {
                command.Start = __instance.start ?? _end; command.End = _end;
                AreaManagement.Submit(command);
                __instance.start = null;
                return false;
            }
        }
        if (!AdvancedBuildingAccess.IsHostControlled(__instance.mode) || AdvancedBuildingAccess.Allow()) return true;
        if (__instance.mode is AM_Mine or AM_Dig) __instance.mode.OnSelectEnd(cancel: true);
        // End the rejected drag without invoking OnSelectEnd/ExecuteSummary.
        __instance.start = null;
        return false;
    }
}

[HarmonyPatch(typeof(UndoManager), nameof(UndoManager.Perform))]
internal static class AdvancedBuildingUndoPatch
{
    [HarmonyPrefix] internal static bool Before()
    {
        if (!NetSession.Instance.IsClient) return true;
        DesignationManagement.Submit(new() { Operation = DesignationOperation.Undo });
        return false;
    }
}
