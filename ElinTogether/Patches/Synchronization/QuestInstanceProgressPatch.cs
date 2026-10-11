using System.Collections.Generic;
using System.Reflection;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Models;
using HarmonyLib;

namespace ElinTogether.Patches;

// Some instance quests (e.g. RQX monster concerts) read their live zone event
// from GetTextProgress. Clients receive quest state before entering the map,
// and do not own that event's simulation. Use the host's presentation result
// for both the tracker and journal, including an empty transition state.
[HarmonyPatch]
internal static class QuestInstanceProgressPatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => OverrideMethodComparer
        .FindAllOverrides(typeof(Quest), nameof(Quest.GetTextProgress));

    [HarmonyPrefix]
    internal static bool Before(Quest __instance, ref string __result)
    {
        if (NetSession.Instance.Connection is not ElinNetClient || !__instance.UseInstanceZone) return true;
        __result = QuestEventWidgetSync.ProgressText(__instance);
        return false;
    }
}
