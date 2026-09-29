using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Capture native reveal results, including already-known cells inside the spell
// area. Replaying Reveal would reroll its RNG and give peers different maps.
[HarmonyPatch]
internal static class MapRevealSyncPatch
{
    internal sealed class Capture(Map map, int zone, Capture? previous)
    {
        internal readonly Map Map = map;
        internal readonly int Zone = zone;
        internal readonly Capture? Previous = previous;
        internal readonly HashSet<int> Cells = [];
    }
    internal static Capture? Current;

    internal static IEnumerable<MethodBase> TargetMethods() => [
        AccessTools.Method(typeof(Map), nameof(Map.Reveal)),
        AccessTools.Method(typeof(Map), nameof(Map.RevealAll)),
    ];

    [HarmonyPrefix]
    internal static void Before(Map __instance, out Capture? __state)
    {
        __state = null;
        if (NetSession.Instance.Connection is null || __instance != EClass._map) return;
        Current = __state = new Capture(__instance, EClass._zone.uid, Current);
    }

    [HarmonyFinalizer]
    internal static void After(Capture? __state, Exception? __exception)
    {
        if (__state is null) return;
        Current = __state.Previous;
        if (__exception is not null || __state.Cells.Count == 0 ||
            __state.Map != EClass._map || __state.Zone != EClass._zone.uid) return;
        NetSession.Instance.Connection?.Delta.AddRemote(new MapRevealDelta {
            ZoneUid = __state.Zone, Size = __state.Map.Size, Cells = __state.Cells.ToArray(),
        });
    }
}

[HarmonyPatch(typeof(Map), nameof(Map.SetSeen))]
internal static class MapRevealCellPatch
{
    [HarmonyPrefix]
    internal static void Before(Map __instance, int x, int z, bool seen)
    {
        var capture = MapRevealSyncPatch.Current;
        if (seen && capture?.Map == __instance && x >= 0 && z >= 0 &&
            x < __instance.Size && z < __instance.Size) capture.Cells.Add(z * __instance.Size + x);
    }
}
