using System;
using System.Collections.Generic;

namespace ElinTogether.Net.Steam;

// Native message buffers are released before anything in this queue executes.
// A save exchange may bypass gameplay messages without dropping/reordering them.
internal sealed class SteamNetPacketInbox
{
    private readonly Queue<(object Packet, ISteamNetPeer Peer)> _packets = [];
    internal void Add(object packet, ISteamNetPeer peer) => _packets.Enqueue((packet, peer));
    internal void Clear() => _packets.Clear();

    internal void Dispatch(Action<object, ISteamNetPeer> handle, Func<object, bool>? accept = null)
    {
        var count = _packets.Count;
        while (count-- > 0 && _packets.TryDequeue(out var item)) {
            if (!item.Peer.IsConnected) continue;
            if (accept is not null && !accept(item.Packet)) {
                _packets.Enqueue(item);
                continue;
            }
            handle(item.Packet, item.Peer);
        }
    }
}
