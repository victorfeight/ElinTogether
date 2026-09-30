using System;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class PlayerProfileRequest
{
    [Key(0)] public int OwnerUid { get; init; }
    [Key(1)] public Guid Session { get; init; }
    [Key(2)] public Guid RequestId { get; init; }
}

[MessagePackObject]
public sealed class PlayerProfileCheckpoint
{
    [Key(0)] public int OwnerUid { get; init; }
    [Key(1)] public Guid Session { get; init; }
    [Key(2)] public long Revision { get; init; }
    [Key(3)] public required string Json { get; init; }
    [Key(4)] public Guid RequestId { get; init; }
}

[MessagePackObject]
public sealed class PlayerProfileReceipt
{
    [Key(0)] public int OwnerUid { get; init; }
    [Key(1)] public Guid Session { get; init; }
    [Key(2)] public long Revision { get; init; }
    [Key(3)] public Guid RequestId { get; init; }
}

// A new channel is issued with each save probe. UID alone cannot distinguish a
// delayed packet from a previous connection (or a previous restored world).
internal sealed class PlayerProfileChannel(int ownerUid, Guid session)
{
    internal int OwnerUid { get; } = ownerUid;
    internal Guid Session { get; } = session;
    private long _revision;
    private long _acknowledged;
    private string? _lastJson;
    private PlayerProfileCheckpoint? _checkpoint;
    internal PlayerProfileCheckpoint? Pending => _acknowledged == _revision ? null : _checkpoint;

    internal PlayerProfileCheckpoint? Capture(Chara actor, Player player, Guid requestId = default)
    {
        if (actor.uid != OwnerUid || Session == Guid.Empty) return null;
        SoloPlayerProfile.Capture(actor, player);
        var json = actor.GetStr(SoloPlayerProfile.SaveKey)!;
        if (json != _lastJson) {
            _lastJson = json;
            _revision++;
            _checkpoint = new() { OwnerUid = OwnerUid, Session = Session, Revision = _revision, Json = json };
        }
        return requestId == Guid.Empty ? Pending : new() {
            OwnerUid = OwnerUid, Session = Session, Revision = _revision, Json = json, RequestId = requestId,
        };
    }

    internal void Acknowledge(PlayerProfileReceipt receipt)
    {
        if (receipt.OwnerUid == OwnerUid && receipt.Session == Session && receipt.Revision == _revision)
            _acknowledged = receipt.Revision;
    }

    internal PlayerProfileReceipt? Accept(Chara actor, PlayerProfileCheckpoint checkpoint)
    {
        if (actor.uid != OwnerUid || checkpoint.OwnerUid != OwnerUid || checkpoint.Session != Session ||
            Session == Guid.Empty || checkpoint.Revision <= 0 || checkpoint.Revision < _revision) return null;
        if (checkpoint.Revision == _revision) {
            if (checkpoint.Json != _lastJson) return null;
        } else {
            SoloPlayerProfile.Accept(actor, checkpoint.Json);
            _lastJson = checkpoint.Json;
            _revision = checkpoint.Revision;
        }
        // Re-ack an identical retry without applying it a second time.
        return new() { OwnerUid = OwnerUid, Session = Session, Revision = _revision, RequestId = checkpoint.RequestId };
    }
}
