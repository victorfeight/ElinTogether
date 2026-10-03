using System;

namespace ElinTogether.Models;

/// <summary>Presentation hooks for confirmed social state, without replaying gameplay.</summary>
public static class SocialNotifications
{
    /// <summary>Client affinity changed: character, previous value, confirmed value.
    /// Covers shared relationships, including another player's social actions.
    /// Initial character loading does not emit this event.</summary>
    public static event Action<Chara, int, int>? AffinityChanged;

    internal static void ApplyAffinity(Chara target, int value)
    {
        var previous = target._affinity;
        target._affinity = value;
        if (previous == value || AffinityChanged is not { } handlers) return;
        foreach (Action<Chara, int, int> handler in handlers.GetInvocationList()) {
            try { handler(target, previous, value); }
            catch (Exception ex) {
                // Optional UI mods must never interrupt state replication.
                EmpLog.Warning("Affinity notification failed for {Uid}: {Error}", target.uid, ex.Message);
            }
        }
    }
}
