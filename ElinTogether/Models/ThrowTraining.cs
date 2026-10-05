using ElinTogether.Net;

namespace ElinTogether.Models;

// A throw result may finish an attack, but must not choose the player's next AI.
internal static class ThrowTraining
{
    private static int _replayDepth;

    internal static bool AllowTask(AIAct task) => _replayDepth == 0 || task is not AI_PracticeDummy;

    internal static void Replay(Card owner, Point point, Card? target, Thing item, ThrowMethod method)
    {
        var previousActor = Act.CC;
        var previousTarget = Act.TC;
        var previousPoint = Act.TP.Copy();
        _replayDepth++;
        try {
            Act.CC = owner.Chara;
            Act.TC = target;
            Act.TP.Set(point);
            ActThrow.Throw(owner, point, target, item, method);
        } finally {
            Act.CC = previousActor;
            Act.TC = previousTarget;
            Act.TP.Set(previousPoint);
            _replayDepth--;
        }
    }

    internal static void StartClient(Card owner, Card? target, Thing item)
    {
        // The client skips native Throw until its result arrives. Start the loop
        // at the original input instead, with vanilla ActThrow's eligibility.
        // SetAI publishes the existing AIPracticeDummyArgs / progress protocol.
        if (NetSession.Instance.Connection is not ElinNetClient ||
            owner is not Chara actor || !actor.IsPC || !item.trait.CanBeDestroyed ||
            !item.HasElement(410) || target is null || actor.ai is AI_PracticeDummy ||
            !(target.trait is TraitTrainingDummy || target.IsRestrainedResident) || actor.stamina.value <= 0) return;

        actor.SetAI(new AI_PracticeDummy { target = target, throwItem = item });
        EmpLog.Debug("Throw training started from client input: actor {ActorUid}, target {TargetUid}, item {ItemUid}",
            actor.uid, target.uid, item.uid);
    }
}
