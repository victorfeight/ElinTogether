using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CharaFaithDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Owner { get; init; }

    [Key(1)]
    public required string FaithId { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (Owner.Find() is not Chara { IsPC: false } chara) {
            return;
        }

        if (game.religions.Find(FaithId) is not { } religion) {
            EmpLog.Warning("Unknown religion {FaithId} for chara {Uid}, heretic!!",
                FaithId, chara.uid);
            return;
        }

        // Legacy choice packets only mirror the host's local conversion.
        // Client conversion must execute the complete validated transaction.
        if (!net.IsHost && chara.faith != religion) religion.JoinFaith(chara);
    }
}
