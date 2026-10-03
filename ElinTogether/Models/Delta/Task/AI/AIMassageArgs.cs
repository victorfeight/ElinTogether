using MessagePack;

namespace ElinTogether.Models.AI;

[MessagePackObject]
public sealed class AIMassageArgs : TaskArgsBase
{
    [Key(0)] public required RemoteCard Target { get; init; }
    public static AIMassageArgs Create(AI_Massage ai) => new() { Target = ai.target };
    public override AIAct CreateSubAct() => new AI_Massage { target = Target };
}
