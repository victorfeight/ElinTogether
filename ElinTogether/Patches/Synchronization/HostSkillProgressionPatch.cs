using ElinTogether.Models;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(ElementContainer), nameof(ElementContainer.ModExp))]
internal static class HostSkillProgressionPatch
{
    [HarmonyPrefix]
    internal static bool Before(ElementContainer __instance, int ele, float a, bool chain) =>
        HostSkillProgression.AllowExperience(__instance, ele, a, chain);
}
