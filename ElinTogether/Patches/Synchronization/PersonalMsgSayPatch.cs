using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class PersonalMsgSayPatch
{
    internal static Chara? BountyReceiver { get; set; }

    internal static Chara? CodexCollector { get; set; }

    internal static ScopeExit ForCollector(Chara collector)
    {
        var previous = CodexCollector;
        CodexCollector = collector;
        return new() { OnExit = () => CodexCollector = previous };
    }

    private static int PeerFor(ElinNetHost host, Chara? receiver) => receiver is null
        ? 0 : host.ActiveRemoteCharas.FirstOrDefault(pair => pair.Value == receiver).Key;

    // Policy-specific messages share the general relay's delivery/fallback boundary.
    private static bool Route(ElinNetHost host, int peer, string text, ref string result) =>
        !MsgRelayContext.TrySend(host, peer, text, ref result);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Msg), nameof(Msg.Say), typeof(string), typeof(int), typeof(string), typeof(string))]
    internal static bool OnCollectionMessage(string idLang, int i, string? ref1, string? ref2,
        ref string __result)
    {
        if (idLang != "addedCards" || NetSession.Instance.Connection is null) return true;
        // CodexCountDelta now announces the confirmed shared change to everyone.
        // Suppress vanilla's misleading personal line on both host and client.
        __result = "";
        Msg.SetColor();
        Msg.alwaysVisible = false;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Msg), nameof(Msg.Say), typeof(string))]
    internal static bool OnWishResult(string idLang, ref string __result)
    {
        if (idLang is not ("dropReward" or "wishFail") || WishInteraction.Receiver is not { } receiver ||
            NetSession.Instance.Connection is not ElinNetHost host) return true;
        return Route(host, PeerFor(host, receiver), Msg.GetGameText(idLang), ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Msg), nameof(Msg.Say), typeof(string), typeof(Card), typeof(string), typeof(string), typeof(string))]
    internal static bool OnPersonalMessage(string idLang, Card c1, string? ref1, string? ref2, string? ref3,
        ref string __result)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host) {
            return true;
        }

        var peerIndex = idLang switch {
            "bounty" when BountyReceiver is { } bountyReceiver => PeerFor(host, bountyReceiver),
            "wish" when WishInteraction.Receiver is { } receiver => PeerFor(host, receiver),
            _ => 0,
        };
        if (peerIndex <= 0) {
            return true;
        }

        return Route(host, peerIndex, Msg.GetRawText(idLang, c1, ref1, ref2, ref3), ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Msg), nameof(Msg.Say), typeof(string), typeof(Card), typeof(Card), typeof(string), typeof(string))]
    internal static bool OnTalismanMessage(string idLang, Card c1, Card c2, string? ref1, string? ref2,
        ref string __result)
    {
        if (idLang is "talisman" or "talisman_pc" && TalismanCraftPatch.Receiver is { } receiver &&
            NetSession.Instance.Connection is ElinNetHost craftHost) {
            return Route(craftHost, PeerFor(craftHost, receiver),
                Msg.GetRawText(idLang, c1, c2, ref1, ref2), ref __result);
        }

        return true;
    }
}
