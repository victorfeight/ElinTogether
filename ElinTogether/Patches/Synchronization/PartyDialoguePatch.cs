using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(DramaCustomSequence), nameof(DramaCustomSequence.Build))]
internal static class PartyDialoguePatch
{
    [HarmonyPostfix]
    internal static void After(DramaCustomSequence __instance, Chara c)
    {
        if (NetSession.Instance.Connection is not ElinNetClient) return;
        var events = __instance.events;
        for (var i = 0; i + 1 < events.Count; i++) {
            var step = events[i].step;
            if (step is not ("_joinParty" or "_leaveParty") || events[i + 1] is not DramaEventMethod method) continue;
            var join = step == "_joinParty";
            method.action = () => {
                if (NetSession.Instance.Connection is not ElinNetClient client) return;
                client.Delta.AddRemote(new PartyCommandDelta { Member = c, ZoneUid = EClass._zone.uid, Join = join });
                EmpLog.Debug("Party command requested: actor {Actor}, member {Member}, join {Join}, zone {Zone}",
                    EClass.pc.uid, c.uid, join, EClass._zone.uid);
                // Do not run native client membership changes or MoveZone.
                // Nor display the unconditional 'hired' response before acceptance.
                __instance.TempGoto(__instance.StepEnd);
            };
        }
    }
}
