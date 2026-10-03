using System;
using System.Collections.Generic;

namespace ElinTogether.Net.Steam;

// Native message buffers are released before anything in this queue executes.
// A save exchange may bypass gameplay messages without dropping/reordering them.
internal sealed class SteamNetPacketInbox
{
    private readonly Queue<(object Packet, ISteamNetPeer Peer, long Message)> _packets = [];
    internal NetworkFlightRecorder? Trace { get; set; }
    internal int Count => _packets.Count;
    internal void Add(object packet, ISteamNetPeer peer, long message = 0) => _packets.Enqueue((packet, peer, message));
    internal void Clear() => _packets.Clear();

    internal void Dispatch(Action<object, ISteamNetPeer> handle, Func<object, bool>? accept = null)
    {
        var count = _packets.Count;
        while (count-- > 0 && _packets.TryDequeue(out var item)) {
            if (!item.Peer.IsConnected) {
                Trace?.Mark("inbox-drop", $"message={item.Message} type={item.Packet.GetType().Name} disconnected peer");
                continue;
            }
            if (accept is not null && !accept(item.Packet)) {
                _packets.Enqueue(item);
                continue;
            }
            using var dispatch = Trace?.Enter("dispatch", $"message={item.Message} type={item.Packet.GetType().Name}");
            handle(item.Packet, item.Peer);
            Trace?.Mark("dispatched", $"message={item.Message} type={item.Packet.GetType().Name}");
        }
    }
}
