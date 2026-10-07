using System;
using MessagePack;

namespace ElinTogether.Models.AI;

[MessagePackObject]
public class AIReadArgs : TaskArgsBase
{
    [Key(0)]
    public required RemoteCard Target { get; init; }
    [Key(1)] public Guid TravelRequest { get; init; }
    [Key(2)] public int ZoneUid { get; init; }

    public static AIReadArgs Create(AI_Read ai)
    {
        return new() {
            Target = ai.target,
            TravelRequest = SharedTravel.IsTravel(ai.target) ? Guid.NewGuid() : Guid.Empty,
            ZoneUid = EClass._zone.uid,
        };
    }

    public override AIAct CreateSubAct()
    {
        return new AI_Read {
            target = Target,
        };
    }
}
