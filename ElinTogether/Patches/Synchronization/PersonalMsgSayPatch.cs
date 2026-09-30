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

    // All personal confirmations use the existing packet and suppress only after delivery.
    private static bool Route(ElinNetHost host, int peer, string text, ref string result)
    {
        if (peer <= 0 || Msg.ignoreAll || !SendToPeer(host, peer, text)) return true;
        result = text;
        Msg.SetColor();
        Msg.alwaysVisible = false;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Msg), nameof(Msg.SayRaw))]
    internal static bool OnWorshipMessage(string text, ref string __result)
    {
        if (FaithTransactions.Actor is not { IsRemotePlayer: true } actor ||
            NetSession.Instance.Connection is not ElinNetHost host) return true;
        // Vanilla has already formatted the line in the requesting player's
        // context. Reuse the ordinary personal-message delivery boundary.
        return Route(host, PeerFor(host, actor), text, ref __result);
    }

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

    internal static int? RefuelPeer { get; set; }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Msg), nameof(Msg.Say), typeof(string))]
    internal static bool OnWishResult(string idLang, ref string __result)
    {
        if (idLang is not ("dropReward" or "wishFail") || WishInteraction.Receiver is not { } receiver ||
            NetSession.Instance.Connection is not ElinNetHost host) return true;
        return Route(host, PeerFor(host, receiver), Msg.GetGameText(idLang), ref __result);
    }
    internal static bool RemoteToggleReplay { get; set; }
    internal static Chara? FirstTimeCraftReceiver { get; set; }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Msg), nameof(Msg.Say), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string))]
    internal static bool OnFirstTimeCraftMessage(string idLang, string ref1, string? ref2, string? ref3,
        string? ref4, ref string __result)
    {
        if (idLang != "firstTimeCraft" || FirstTimeCraftReceiver is not { } receiver ||
            NetSession.Instance.Connection is not ElinNetHost host) return true;
        var peer = PeerFor(host, receiver);
        if (peer <= 0) return true;
        return Route(host, peer, Msg.GetRawText(idLang, ref1, ref2, ref3, ref4), ref __result);
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
            "crafted" when RemoteCraft.ProductReceiver is { } receiver =>
                PeerFor(host, receiver),
            "fueled" => RefuelPeer.GetValueOrDefault(),
            _ => 0,
        };
        if (peerIndex <= 0) {
            return true;
        }

        return Route(host, peerIndex, Msg.GetRawText(idLang, c1, ref1, ref2, ref3), ref __result);
    }

    private static bool SendToPeer(ElinNetHost host, int peerIndex, string text)
    {
        var color = Msg.currentColor;
        return host.SendDeltaTo(peerIndex, new MsgSayDelta {
                Text = text,
                R = color.r,
                G = color.g,
                B = color.b,
                A = color.a,
            });
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Msg), nameof(Msg.Say), typeof(string), typeof(Card), typeof(Card), typeof(string), typeof(string))]
    internal static bool OnRemoteToggleMessage(string idLang, Card c1, Card c2, string? ref1, string? ref2,
        ref string __result)
    {
        if (idLang is "talisman" or "talisman_pc" && TalismanCraftPatch.Receiver is { } receiver &&
            NetSession.Instance.Connection is ElinNetHost craftHost) {
            return Route(craftHost, PeerFor(craftHost, receiver),
                Msg.GetRawText(idLang, c1, c2, ref1, ref2), ref __result);
        }

        var isRemoteToggle = RemoteToggleReplay &&
            idLang is "toggle_fire" or "toggle_ele" or "lever" or "open" or "close";
        var isRemoteRefuel = idLang == "toggle_fire" && RefuelPeer is > 0;
        if ((!isRemoteToggle && !isRemoteRefuel) ||
            NetSession.Instance.Connection is not ElinNetHost) {
            return true;
        }

        // The acting client already emits its own toggle line with the local
        // player and visible card name.
        __result = Msg.GetRawText(idLang, c1, c2, ref1, ref2);
        Msg.SetColor();
        Msg.alwaysVisible = false;
        return false;
    }
}
