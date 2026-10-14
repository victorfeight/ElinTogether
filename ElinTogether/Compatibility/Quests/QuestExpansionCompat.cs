using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElinTogether.Net;
using ElinTogether.Helper;
using HarmonyLib;

namespace ElinTogether.Models;

// Optional adapter: no hard dependency on the quest mod, no copies of its rules.
internal static class QuestExpansionCompat
{
    internal static readonly Assembly? Assembly = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == "RandomQuestExpantion");
    internal static readonly Assembly? BoardAssembly = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == "QuestPack");
    internal static bool IsExpansion(Assembly? assembly) => assembly != null &&
        (assembly == Assembly || assembly == BoardAssembly);
    internal static readonly Type[] Types = typeof(Quest).Assembly.GetTypes()
        .Concat(Assembly?.GetTypes() ?? []).Concat(BoardAssembly?.GetTypes() ?? []).ToArray();
    internal static MethodInfo? BoardMethod(string type, string name) =>
        BoardAssembly?.GetType("QuestPack." + type) is { } t ? AccessTools.Method(t, name) : null;
    internal static MethodInfo? Method(string type, string name) =>
        Assembly?.GetType("RandomQuestExpantion." + type) is { } t ? AccessTools.Method(t, name) : null;
    internal static bool IsQuestEvent(ZoneEvent evt) => evt is ZoneEventQuest ||
        IsExpansion(evt.GetType().Assembly);
    internal static int QuestLimit => Method("Config.ModConfig", "get_MaxQuestLimit")?.Invoke(null, null) is int limit
        ? limit : QuestManager.MaxRandomQuest;
    internal static void OnInvest() => Method("Patch.DramaPatch", "HandleOnInvestShopEvent")?.Invoke(null, null);

    internal static List<Thing> PartyDeliveryItems(QuestDeliver quest, bool onlyFirst)
    {
        var result = quest.ListDestThing(onlyFirst);
        if (NetSession.Instance.Connection is not ElinNetHost host || (onlyFirst && result.Count > 0)) return result;
        var previous = EClass.pc;
        try {
            foreach (var actor in host.ActiveRemoteCharas.Values.Distinct()) {
                if (actor.isDead || !actor.IsInActiveMap || actor == previous) continue;
                EClass.player.chara = actor;
                foreach (var item in quest.ListDestThing(onlyFirst)) {
                    if (!result.Contains(item)) result.Add(item);
                    if (onlyFirst) return result;
                }
            }
        } finally { EClass.player.chara = previous; }
        return result;
    }

    internal static bool PartyPerforming(AIAct localAI)
    {
        if (localAI is AI_PlayMusic) return true;
        if (NetSession.Instance.Connection is not ElinNetHost host) return false;
        return host.ActiveRemoteCharas.Values.Any(actor => !actor.isDead && actor.IsInActiveMap && Playing(actor.ai));
    }
    private static bool Playing(AIAct? ai)
    {
        for (; ai != null; ai = ai.child) if (ai is AI_PlayMusic && ai.IsRunning) return true;
        return false;
    }
}
