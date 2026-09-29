using System.Collections;
using SynchronizationContext = ElinTogether.Patches.SynchronizationContext;

namespace ElinTogether.Net;

internal partial class ElinNetClient
{
    // Unlike DeferredCoroutine's conditional overload, this iterator has no
    // static runner. StopAllCoroutines/destruction cancels only this connection's wait.
    private IEnumerator StartWorldStateUpdateWhenReady()
    {
        while (!core.IsGameStarted) yield return null;
        StartWorldStateUpdate();
    }

    /// <summary>
    ///     Subscribe all scheduler jobs and reset pause state
    /// </summary>
    public void StartWorldStateUpdate()
    {
        // 50hz delta dispatch
        Scheduler.Subscribe(SynchronizationContext.AllowDeltaSending, 50f);

        _pauseUpdate = false;
        EmpLog.Debug("Started client delta sending after game became ready");
    }

    /// <summary>
    ///     Unsubscribe all scheduler jobs and reset pause state
    /// </summary>
    public void StopWorldStateUpdate()
    {
        Scheduler.Unsubscribe(SynchronizationContext.AllowDeltaSending);

        _pauseUpdate = false;

        EmpLog.Debug("Stopping client state update");
    }

}
