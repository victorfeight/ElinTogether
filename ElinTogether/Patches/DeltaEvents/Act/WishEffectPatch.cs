using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(ActEffect), nameof(ActEffect.Proc), typeof(EffectId), typeof(int),
    typeof(BlessedState), typeof(Card), typeof(Card), typeof(ActRef))]
internal static class WishEffectPatch
{
    [HarmonyPrefix]
    internal static bool Before(EffectId id, int power, BlessedState state, Card cc, Card? tc)
    {
        if (id != EffectId.Wish) return true;
        // Prediction must not open an unbacked dialog or manufacture a local reward.
        if (NetSession.Instance.Connection is ElinNetClient) return false;
        if (NetSession.Instance.Connection is not ElinNetHost host) return true;
        var recipient = (tc ?? cc).Chara;
        if (recipient is null || recipient.IsPC) return true;
        foreach (var pair in host.ActiveRemoteCharas) {
            if (pair.Value != recipient) continue;
            // Proc doubles blessed/cursed power, then the Wish branch halves it.
            // Its incoming power is therefore exactly the value Wish receives.
            WishInteraction.Grant(host, pair.Key, recipient, power, state);
            return false;
        }
        return true;
    }
}
