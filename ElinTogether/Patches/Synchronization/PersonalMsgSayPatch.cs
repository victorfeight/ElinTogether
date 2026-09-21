using System.Linq;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class PersonalMsgSayPatch
{
    internal static int? RefuelPeer { get; set; }
    internal static bool RemoteToggleReplay { get; set; }
    internal static Chara? FirstTimeCraftReceiver { get; set; }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Msg), nameof(Msg.Say), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string))]
    internal static void OnFirstTimeCraftMessage(string idLang, string ref1, string? ref2, string? ref3, string? ref4)
    {
        if (idLang != "firstTimeCraft" || FirstTimeCraftReceiver is not { } receiver ||
            NetSession.Instance.Connection is not ElinNetHost host) {
            return;
        }

        var peerIndex = host.ActiveRemoteCharas.FirstOrDefault(pair => pair.Value == receiver).Key;
        if (peerIndex > 0) {
            SendToPeer(host, peerIndex, Msg.GetRawText(idLang, ref1, ref2, ref3, ref4));
        }
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
            "crafted" when RemoteCraft.ProductReceiver is { } receiver =>
                host.ActiveRemoteCharas.FirstOrDefault(pair => pair.Value == receiver).Key,
            "fueled" => RefuelPeer.GetValueOrDefault(),
            _ => 0,
        };
        if (peerIndex <= 0) {
            return true;
        }

        var text = Msg.GetRawText(idLang, c1, ref1, ref2, ref3);
        if (!SendToPeer(host, peerIndex, text)) {
            return true;
        }

        __result = text;
        Msg.SetColor();
        Msg.alwaysVisible = false;
        return false;
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
