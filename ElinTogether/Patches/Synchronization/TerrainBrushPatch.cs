using System;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(AM_Terrain), nameof(AM_Terrain.OnProcessTiles))]
internal static class TerrainBrushPatch
{
    [HarmonyPrefix]
    internal static bool Before(AM_Terrain __instance, Point point, out Action? __state)
    {
        __state = null;
        if (NetSession.Instance.Connection is ElinNetClient) {
            TerrainBrush.Submit(__instance, point);
            return false;
        }
        __state = TerrainBrush.Capture(__instance, point);
        return true;
    }

    [HarmonyFinalizer]
    internal static void After(Action? __state) => __state?.Invoke();
}
