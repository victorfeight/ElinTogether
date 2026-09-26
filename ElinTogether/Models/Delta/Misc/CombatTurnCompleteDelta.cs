using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CombatTurnCompleteDelta : ElinDelta
{
    [Key(0)] public int RoundId { get; init; }
    [Key(1)] public float Cost { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host && host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var chara)) {
            ActionModeCombat.OnRoundComplete(chara.uid, RoundId, Cost);
        }
    }
}
