using System.Linq;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

internal static class RemoteCharaLifecyclePatch
{
    private static readonly Harmony Harmony = new($"{ModInfo.Guid}.remote-chara-lifecycle");

    internal static void Apply()
    {
        Harmony.Patch(
            AccessTools.Method(typeof(Player), nameof(Player.OnLoad)),
            postfix: new HarmonyMethod(typeof(RemoteCharaLifecyclePatch),
                nameof(DetachSavedRemoteCharasFromHomeBranches)));
    }

    internal static void DetachSavedRemoteCharasFromHomeBranches()
    {
        // The host owns saved remote-character lifecycle state. A client loading
        // the host's probe must retain the replicated branch state unchanged.
        if (NetSession.Instance.IsClient) {
            return;
        }

        foreach (var chara in EClass.game.cards.globalCharas.Values
                     .Where(chara => chara.GetBool("remote_chara"))) {
            ElinNetHost.DetachRemoteFromHomeBranch(chara);
        }
    }
}
