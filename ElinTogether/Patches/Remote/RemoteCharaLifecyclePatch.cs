using System.Linq;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Game), nameof(Game.OnLoad))]
internal static class RemoteCharaLifecyclePatch
{
    [HarmonyPrefix]
    internal static void DetachSavedRemoteCharasFromHomeBranches()
    {
        foreach (var chara in EClass.game.cards.globalCharas.Values
                     .Where(chara => chara.GetBool("remote_chara"))) {
            ElinNetHost.DetachRemoteFromHomeBranch(chara);
        }
    }
}
