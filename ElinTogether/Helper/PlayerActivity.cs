using System.Collections.Generic;
using ElinTogether.API.SourceValidation;
using ElinTogether.Elements;

namespace ElinTogether.Helper;

// A remote task is only the current replicated step, not the player's whole goal.
internal static class PlayerActivity
{
    internal static void ClearFinishedLocalGoal(Chara chara)
    {
        // Vanilla normally does this on the next Chara.Tick. Multiplayer may
        // already have paused the clock, while input still requires HasNoGoal.
        if (chara.IsPC && !chara.isDead && !chara.HasNoGoal && !chara.ai.IsRunning) {
            chara.SetNoGoal();
        }
    }

    internal static int ReportAct(Chara chara)
    {
        return chara.isDead || chara.ai.IsNoGoal || !chara.ai.IsRunning
            ? 0
            : ActMappingValidator.Default.ActToIdMapping.GetValueOrDefault(chara.ai.GetType(), 0);
    }

    internal static bool IsBusy(Chara chara, int reportedAct = 0)
    {
        if (chara.isDead || !chara.IsInActiveMap) return false;

        var task = chara.ai is GoalRemote remote ? remote.child : chara.ai;
        if (task is { IsRunning: true, IsNoGoal: false }) return true;

        // Reports arrive on the existing network scheduler even while gameplay is
        // paused. They cover navigation and the gap between reconstructed tasks.
        return ActMappingValidator.Default.IdToActMapping.TryGetValue(reportedAct, out var type) &&
               typeof(AIAct).IsAssignableFrom(type) && !typeof(NoGoal).IsAssignableFrom(type);
    }
}
