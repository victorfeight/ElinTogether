using System.Collections.Generic;
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

    [Key(2)] public required int[] DuePlayers { get; init; }
    [Key(3)] public double Clock { get; init; }
    [Key(4)] public required Dictionary<int, double> Deadlines { get; init; }

    [Key(5)] public int DispatchUid { get; init; }
    [Key(6)] public int Revision { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        // host only
        if (net.IsHost) {
            return;
        }

        ActionModeCombat.ChangePhaseLocal(Phase, RoundId, DuePlayers, Clock, Deadlines, DispatchUid, Revision);
    }
}