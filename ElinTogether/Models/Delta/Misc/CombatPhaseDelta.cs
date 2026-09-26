using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CombatPhaseDelta : ElinDelta
{
    [Key(0)]
    public required ActionModeCombat.CombatPhase Phase { get; init; }

    [Key(1)] public int RoundId { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        // host only
        if (net.IsHost) {
            return;
        }

        ActionModeCombat.ChangePhaseLocal(Phase, RoundId);
    }
}