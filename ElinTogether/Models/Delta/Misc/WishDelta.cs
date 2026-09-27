using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class WishPromptDelta : ElinDelta
{
    [Key(0)] public Guid RequestId { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetClient client) WishInteraction.Show(client, RequestId);
    }
}

[MessagePackObject]
public class WishReplyDelta : ElinDelta
{
    [Key(0)] public Guid RequestId { get; init; }
    // Null means the player cancelled the vanilla dialog.
    [Key(1)] public string? Text { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is not ElinNetHost host) return;
        using var simulation = Simulate();
        WishInteraction.Resolve(host, OriginPeer, RequestId, Text);
    }
}

internal static class WishInteraction
{
    private sealed record Pending(ElinNetHost Host, int Peer, Chara Recipient, int ZoneUid,
        int Power, BlessedState State);

    private static readonly Dictionary<Guid, Pending> Wishes = [];
    private static readonly HashSet<Guid> Shown = [];
    internal static Chara? Receiver { get; private set; }

    [ElinPreLoad]
    private static void OnLoad(GameIOContext context) => Clear();

    internal static void Clear()
    {
        Wishes.Clear();
        Shown.Clear();
    }

    internal static void ReleasePeer(int peer)
    {
        foreach (var id in Wishes.Where(pair => pair.Value.Peer == peer).Select(pair => pair.Key).ToArray()) {
            Wishes.Remove(id);
        }
    }

    internal static void Grant(ElinNetHost host, int peer, Chara recipient, int power, BlessedState state)
    {
        var id = Guid.NewGuid();
        Wishes.Add(id, new(host, peer, recipient, EClass._zone.uid, power, state));
        if (!host.SendDeltaTo(peer, new WishPromptDelta { RequestId = id })) {
            Wishes.Remove(id);
            EmpLog.Warning("Wish prompt {RequestId} could not reach peer {Peer}", id, peer);
            return;
        }
        EmpLog.Information("Wish granted {RequestId}: peer {Peer}, recipient {Uid}, power {Power}, state {State}",
            id, peer, recipient.uid, power, state);
    }

    internal static void Show(ElinNetClient client, Guid id)
    {
        if (id == Guid.Empty || !Shown.Add(id)) return;
        var replied = false;
        Dialog.InputName("dialogWish", "q", (cancel, text) => {
            if (replied || NetSession.Instance.Connection != client) return;
            replied = true;
            client.Delta.AddRemote(new WishReplyDelta { RequestId = id, Text = cancel ? null : text });
        });
    }

    internal static void Resolve(ElinNetHost host, int peer, Guid id, string? text)
    {
        if (!Wishes.TryGetValue(id, out var wish) || wish.Host != host || wish.Peer != peer) {
            EmpLog.Warning("Wish reply {RequestId} rejected from peer {Peer}: no matching grant", id, peer);
            return;
        }
        // Consume before invoking vanilla, including cancellation and failures.
        Wishes.Remove(id);
        if (text is null) {
            EmpLog.Information("Wish cancelled {RequestId} by peer {Peer}", id, peer);
            return;
        }
        if (!host.ActiveRemoteCharas.TryGetValue(peer, out var recipient) || recipient != wish.Recipient ||
            recipient.isDead || !recipient.IsInActiveMap || EClass._zone.uid != wish.ZoneUid) {
            host.SendDeltaTo(peer, new MsgSayDelta {
                Text = "The wish could not be completed because your character or location changed.",
                R = 1, G = 1, B = 1, A = 1,
            });
            EmpLog.Warning("Wish reply {RequestId} rejected: recipient or zone changed", id);
            return;
        }

        var previousPC = EClass.pc;
        var previousReceiver = Receiver;
        try {
            // Wish uses pc for ground placement, message identity and artifact handling.
            EClass.game.player.chara = recipient;
            Receiver = recipient;
            Msg.Say("wish", recipient, text);
            var success = ActEffect.Wish(text, recipient.NameTitled, wish.Power, wish.State);
            EmpLog.Information("Wish resolved {RequestId}: recipient {Uid}, text {Text}, success {Success}, zone {ZoneUid}, position {X},{Z}",
                id, recipient.uid, text, success, wish.ZoneUid, recipient.pos.x, recipient.pos.z);
        } finally {
            EClass.game.player.chara = previousPC;
            Receiver = previousReceiver;
        }
    }
}
