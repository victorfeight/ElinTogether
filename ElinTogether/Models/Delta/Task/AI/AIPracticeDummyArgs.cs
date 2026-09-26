using ElinTogether.Elements;
using MessagePack;

namespace ElinTogether.Models.AI;

// Training attacks already travel through melee/ranged/throw events. A progress
// placeholder prevents a second host AI from performing those attacks again.
[MessagePackObject]
public class AIPracticeDummyArgs : TaskArgsBase
{
    public override AIAct CreateSubAct() => DelegateProgress.Create(typeof(AI_PracticeDummy));
}
