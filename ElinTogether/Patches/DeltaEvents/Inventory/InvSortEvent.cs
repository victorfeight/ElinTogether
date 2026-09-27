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
