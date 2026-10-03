using ElinTogether.Models;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Game), nameof(Game.Kill))]
internal static class LocalWidgetLayoutPatch
{
    // Runs before vanilla destroys widgets, including solo -> join, disconnect,
    // and the live-world replacement when returning from companion control.
    [HarmonyPrefix]
    internal static void BeforeKill() => LocalWidgetLayout.CaptureLive();
}
