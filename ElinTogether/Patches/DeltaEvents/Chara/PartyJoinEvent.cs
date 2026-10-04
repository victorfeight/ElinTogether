using ElinTogether.Helper;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Party), nameof(Party.AddMemeber))]
internal static class PartyJoinEvent
{
    [HarmonyPostfix]
    internal static void After(Party __instance, Chara c)
    {
        if (NetSession.Instance.Connection is ElinNetHost host &&
            __instance == EClass.pc.party && c.party == __instance && !c.IsPlayer) {
            host.Delta.AddRemote(new PartyMemberDelta { Member = c, Joined = true, CaptureSource = c });
        }
    }
}
