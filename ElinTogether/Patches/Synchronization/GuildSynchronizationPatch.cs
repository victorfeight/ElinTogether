using System.Collections.Generic;
using System.Reflection;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class GuildQuestProgressPatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => [
        AccessTools.Method(typeof(Quest), nameof(Quest.OnModKarma)),
        AccessTools.Method(typeof(Quest), nameof(Quest.OnKillChara)),
        AccessTools.Method(typeof(Quest), nameof(Quest.OnGiveItem)),
        AccessTools.Method(typeof(Quest), nameof(Quest.CompleteTask)),
    ];

    [HarmonyPrefix]
    internal static bool Before(Quest __instance) =>
        NetSession.Instance.Connection is not ElinNetClient || !GuildStateSnapshot.IsGuildQuest(__instance);

    [HarmonyPostfix]
    internal static void After(Quest __instance)
    {
        if (GuildStateSnapshot.IsGuildQuest(__instance)) GuildStateSnapshot.MarkDirty();
    }
}

[HarmonyPatch]
internal static class GuildMutationPatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => [
        AccessTools.Method(typeof(Quest), nameof(Quest.ChangePhase)),
        AccessTools.Method(typeof(Quest), nameof(Quest.Complete)),
        AccessTools.Method(typeof(QuestManager), nameof(QuestManager.Start), [typeof(Quest)]),
        AccessTools.Method(typeof(Faction), nameof(Faction.AddContribution)),
        AccessTools.Method(typeof(FactionRelation), nameof(FactionRelation.Promote)),
        AccessTools.Method(typeof(DramaOutcome), nameof(DramaOutcome.guild_join)),
    ];

    // Coalesce until the next world snapshot, after CompleteTask clears its task.
    [HarmonyPostfix]
    internal static void After() => GuildStateSnapshot.MarkDirty();
}

[HarmonyPatch(typeof(Faction), nameof(Faction.AddContribution))]
internal static class GuildContributionReplayPatch
{
    [HarmonyPrefix]
    internal static bool Before(Faction __instance) => __instance is not Guild ||
        NetSession.Instance.Connection is not ElinNetClient || !ElinDelta.IsApplying;
}

[HarmonyPatch(typeof(TraitBaseSpellbook), nameof(TraitBaseSpellbook.OnRead))]
internal static class RemoteGuildBookPatch
{
    [HarmonyPostfix]
    internal static void After(TraitBaseSpellbook __instance, Chara c)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host || c.IsPC ||
            !System.Linq.Enumerable.Contains(host.ActiveRemoteCharas.Values, c)) return;
        // Vanilla only credits c.IsPC. The host owns the remote reader's actual
        // completion; its client replay is suppressed by GuildContributionReplayPatch.
        if (__instance.BookType == TraitBaseSpellbook.Type.Ancient) {
            Guild.Mage.AddContribution(5 + __instance.owner.refVal * 2);
        }
    }
}

[HarmonyPatch]
internal static class GuildDialoguePatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => [
        AccessTools.Method(typeof(DramaOutcome), nameof(DramaOutcome.guild_trial)),
        AccessTools.Method(typeof(DramaOutcome), nameof(DramaOutcome.guild_join)),
        AccessTools.Method(typeof(DramaOutcome), nameof(DramaOutcome.guild_mageTrial)),
        AccessTools.Method(typeof(DramaOutcome), nameof(DramaOutcome.guild_promote)),
    ];

    [HarmonyPrefix]
    internal static bool Before(DramaOutcome __instance, MethodBase __originalMethod)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client) return true;
        var guild = Guild.CurrentDrama;
        var operation = __originalMethod.Name switch {
            nameof(DramaOutcome.guild_trial) => GuildActionDelta.Operation.Trial,
            nameof(DramaOutcome.guild_join) => GuildActionDelta.Operation.Join,
            nameof(DramaOutcome.guild_mageTrial) => GuildActionDelta.Operation.MageLetter,
            _ => GuildActionDelta.Operation.Promote,
        };
        client.Delta.AddRemote(new GuildActionDelta {
            GuildId = guild.id, Npc = __instance.cc, Action = operation,
            ExpectedPhase = guild.Quest?.phase ?? -1, ExpectedRank = guild.relation.rank,
        });
        return false;
    }
}
