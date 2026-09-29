using System.Collections.Generic;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(UIInventory), nameof(UIInventory.Sort))]
internal static class InvSortEvent
{
    // Vanilla retries stacking until TryStackTo returns false for every pair.
    // A queued remote merge cannot count as progress in that synchronous loop.
    [System.ThreadStatic]
    internal static HashSet<int>? QueuedSources;

    [HarmonyPrefix]
    internal static void Begin(out HashSet<int>? __state)
    {
        __state = QueuedSources;
        if (NetSession.Instance.Connection is ElinNetClient) {
            QueuedSources ??= new HashSet<int>();
        }
    }

    [HarmonyFinalizer]
    internal static void End(HashSet<int>? __state)
    {
        QueuedSources = __state;
    }
}

// A previous right-click can leave this latch set until the next dirty redraw.
// Vanilla then sorts despite Shift being held, rebinding the clicked button before
// its queued OnClick runs. Preserve vanilla's normal sort-on-Shift-release path.
[HarmonyPatch(typeof(UIInventory), nameof(UIInventory.CheckDirty))]
internal static class InvQuickTransferSortGuard
{
    [HarmonyPrefix]
    internal static void Before(ref bool ___firstMouseRightDown)
    {
        if (NetSession.Instance.Connection is not null && UnityEngine.Input.GetMouseButton(0) &&
            (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) ||
             UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift))) {
            ___firstMouseRightDown = false;
        }
    }
}
