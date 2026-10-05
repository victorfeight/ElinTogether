using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Card), nameof(Card.ModExpParty), typeof(int), typeof(int))]
internal static class RemoteModExpPartyPatch
{
    [HarmonyPrefix]
    internal static bool OnModExpParty(Card __instance, int ele, int a)
    {
        if (NetSession.Instance.Connection is not { } connection) {
            return true;
        }

        // Native oversized-crop harvesting awards the entire party. Client replay
        // must not reroll that award, nor may a remote harvester suppress everyone.
        if (!connection.IsHost) return false;
        if (__instance is not Chara actor) return true;
        foreach (var member in actor.party?.members ?? [actor]) {
            using (HostSkillProgression.Begin(connection, member)) member.ModExp(ele, a);
        }
        return false;
    }
}
