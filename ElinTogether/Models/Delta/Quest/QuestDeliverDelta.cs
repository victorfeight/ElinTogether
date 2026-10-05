using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class QuestDeliverDelta : ElinDelta
{
    [Key(0)] public int Uid { get; init; }
    [Key(1)] public int ZoneUid { get; init; }
    [Key(2)] public required RemoteCard Recipient { get; init; }
    [Key(3)] public required RemoteCard Item { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host) SharedQuests.Deliver(host, this);
    }
}
