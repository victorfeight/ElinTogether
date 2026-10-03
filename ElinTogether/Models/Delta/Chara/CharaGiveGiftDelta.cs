using System;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CharaGiveGiftDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard From { get; init; }

    [Key(1)]
    public required RemoteCard To { get; init; }

    [Key(2)]
    public required RemoteCard Thing { get; init; }

    [Key(3)] public Guid Id { get; init; }
    [Key(4)] public int ZoneUid { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host) SocialInteractions.Give(host, OriginPeer, this);
    }
}
