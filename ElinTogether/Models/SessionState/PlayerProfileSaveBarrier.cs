using System;
using System.Collections.Generic;

namespace ElinTogether.Models;

// One explicit save request. Old checkpoints/receipts cannot complete a new save.
internal sealed class PlayerProfileSaveBarrier
{
    internal Guid Id { get; } = Guid.NewGuid();
    private readonly Dictionary<int, PlayerProfileRequest> _players = [];
    private readonly HashSet<int> _received = [];
    internal bool Complete => _received.Count == _players.Count;

    internal PlayerProfileRequest Add(int peer, int owner, Guid session)
    {
        var request = new PlayerProfileRequest { OwnerUid = owner, Session = session, RequestId = Id };
        _players.Add(peer, request);
        return request;
    }

    internal bool Owns(int peer, int uid) => _players.TryGetValue(peer, out var request) && request.OwnerUid == uid;

    internal void Receive(int peer, PlayerProfileReceipt receipt)
    {
        if (_players.TryGetValue(peer, out var request) && receipt.RequestId == Id &&
            receipt.Session == request.Session && receipt.OwnerUid == request.OwnerUid && receipt.Revision > 0)
            _received.Add(peer);
    }
}
