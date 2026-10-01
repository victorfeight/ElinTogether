using System.Linq;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class MsgRelayContext
{
    private static Mode _mode;
    private static int _peerIndex;
    private static Card? _speaker;

    internal static bool IsRedirecting => _mode == Mode.Redirect;

    internal static ScopeExit RedirectTo(Chara receiver)
    {
        return NetSession.Instance.Connection is ElinNetHost host
            ? RedirectTo(host.ActiveRemoteCharas.FirstOrDefault(pair => pair.Value == receiver).Key)
            : new();
    }

    internal static ScopeExit RedirectTo(int peerIndex)
    {
        return Enter(peerIndex > 0 ? Mode.Redirect : Mode.None, peerIndex);
    }

    internal static ScopeExit Suppress(bool active = true)
    {
        return active ? Enter(Mode.Suppress, 0) : new();
    }

    internal static ScopeExit IncludeSpeaker(Card? speaker)
    {
        if (!IsRedirecting) return new();
        var previous = _speaker;
        _speaker = speaker;
        return new() { OnExit = () => _speaker = previous };
    }

    private static ScopeExit Enter(Mode mode, int peerIndex)
    {
        var (previousMode, previousPeer) = (_mode, _peerIndex);
        _mode = mode;
        _peerIndex = peerIndex;
        return new() {
            OnExit = () => {
                _mode = previousMode;
                _peerIndex = previousPeer;
            },
        };
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Card), nameof(Card.ShouldShowMsg), MethodType.Getter)]
    internal static void ShowRecipientMessage(Card __instance, ref bool __result)
    {
        // Card.Say checks host visibility before reaching Msg.SayRaw. A personal
        // recipient must still receive their own confirmation when off-screen.
        if (!__result && IsRedirecting && NetSession.Instance.Connection is ElinNetHost host &&
            host.ActiveRemoteCharas.TryGetValue(_peerIndex, out var receiver) &&
            (ReferenceEquals(__instance, receiver) || ReferenceEquals(__instance, _speaker)) && !receiver.isDead) __result = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Msg), nameof(Msg.SayRaw))]
    internal static bool OnSayRaw(string text, ref string __result)
    {
        if (_mode == Mode.None || Msg.ignoreAll || NetSession.Instance.Connection is not ElinNetHost host) {
            return true;
        }

        if (_mode == Mode.Redirect) return !TrySend(host, _peerIndex, text, ref __result);
        Msg.SetColor();
        Msg.alwaysVisible = false;
        __result = text;
        return false;
    }

    internal static bool TrySend(ElinNetHost host, int peer, string text, ref string result)
    {
        if (peer <= 0 || Msg.ignoreAll) return false;
        var color = Msg.currentColor;
        if (!host.SendDeltaTo(peer, new MsgSayDelta {
            Text = text, R = color.r, G = color.g, B = color.b, A = color.a,
        })) return false;
        result = text;
        Msg.SetColor();
        Msg.alwaysVisible = false;
        return true;
    }

    private enum Mode : byte
    {
        None,
        Redirect,
        Suppress,
    }
}