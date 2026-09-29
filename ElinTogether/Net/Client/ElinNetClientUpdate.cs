using System.Linq;
using ElinTogether.Models;
using ElinTogether.Patches;

namespace ElinTogether.Net;

internal partial class ElinNetClient
{
    private WorldStateSnapshot? _lastTick;
    private bool _pauseUpdate;

    /// <summary>
    ///     Send out local deltas and self snapshot to remote host
    /// </summary>
    internal void WorldStateDeltaUpdate()
    {
        if (_pauseUpdate) {
            return;
        }

        // send out delta
        if (!Delta.HasPendingOut) {
            return;
        }

        if (Delta.FlushOutBuffer() is not { Count: > 0 } deltaList) {
            return;
        }

        var sent = Host.Send(new WorldStateDeltaList {
            DeltaList = deltaList,
        });
        if (deltaList.Any(d => d is CharaBuildDelta or CardAddThingDelta or ThingRequest)) {
            EmpLog.Debug("Client item/build batch sent: success {Sent}, types {Types}",
                sent, string.Join(",", deltaList.Select(d => d.GetType().Name)));
        }
    }

    /// <summary>
    ///     Process local or deferred deltas received from host
    /// </summary>
    internal void WorldStateDeltaProcess()
    {
        if (!Delta.HasPendingIn) {
            return;
        }

        Delta.ProcessLocalBatch(this);
    }

    /// <summary>
    ///     Net event: Apply client-side reconciliation from host
    /// </summary>
    private void OnWorldStateSnapshot(WorldStateSnapshot snapshot)
    {
        if (_lastTick is not null) {
            var dropped = snapshot.ServerTick - _lastTick.ServerTick;
            if (dropped > 1) {
                EmpLog.Warning("Falling behind with {DroppedTicks} dropped ticks",
                    dropped);
            }
        }

        _lastTick = snapshot;
        Session.Tick = snapshot.ServerTick;

        if (core.game?.activeZone?.map is null) {
            return;
        }

        // apply world state
        snapshot.ApplyReconciliation();

        // send back client state
        Host.Send(CharaStateSnapshot.CreateSelf());
    }

    /// <summary>
    ///     Net event: Apply delta changes from host
    /// </summary>
    private void OnWorldStateDeltaResponse(WorldStateDeltaList response)
    {
        foreach (var delta in response.DeltaList) {
            Delta.AddLocal(delta);
        }
    }

    /// <summary>
    ///     Net event: Apply new session remote player states
    /// </summary>
    private void OnSessionStatesUpdate(SessionPlayersSnapshot states)
    {
        states.Apply();
    }

    /// <summary>
    ///     Net event: Apply new session rules
    /// </summary>
    /// <param name="rules"></param>
    private void OnSessionRulesUpdate(NetSessionRules rules)
    {
        NetSession.Instance.Rules = rules;
    }

#region Scheduler Jobs

    /// <summary>
    ///     Pause sending out deltas, *but they still accumulate*
    /// </summary>
    public void PauseWorldStateUpdate()
    {
        _pauseUpdate = true;

        EmpLog.Debug("Pausing client state update");
    }

    /// <summary>
    ///     Resume sending out deltas
    /// </summary>
    public void ResumeWorldStateUpdate(bool clearDelta = false)
    {
        _pauseUpdate = false;

        EmpLog.Debug("Resuming client state update");

        if (clearDelta) {
            Delta.ClearOut();
        }
    }

#endregion
}
