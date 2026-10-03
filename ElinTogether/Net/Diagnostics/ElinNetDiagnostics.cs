using System;
using System.Linq;
using ElinTogether.Patches;

namespace ElinTogether.Net;

public abstract partial class ElinNetBase
{
    internal readonly NetworkFlightRecorder Trace = new(message => EmpLog.Information("NetTrace {NetworkTrace}", message));
    private DateTime _nextTraceContext;

    internal void CaptureTraceContext()
    {
        if (DateTime.UtcNow < _nextTraceContext) return;
        _nextTraceContext = DateTime.UtcNow.AddSeconds(2);
        try {
            Socket.RefreshTraceStats();
            var currentGame = core.game;
            var actor = currentGame?.player?.chara;
            var mode = this is ElinNetClient client ? client.TraceState : ((ElinNetHost)this).TraceState;
            Trace.Context($"role={(IsHost ? "host" : "client")} lobby={Session.SessionId} tick={Session.Tick} " +
                $"uid={actor?.uid} zone={currentGame?.activeZone?.uid} started={core.IsGameStarted} loading={currentGame?.isLoading} " +
                $"paused={scene?.paused} uiBlocked={ui?.BlockActions} ai={actor?.ai?.GetType().Name}/{actor?.ai?.Current?.GetType().Name} " +
                $"gameDelta={Core.gameDelta} bufferedTime={SynchronizationContext.GameDelta} canSend={SynchronizationContext.CanSendDelta} " +
                $"deltaBuffers={Delta.GetCounts()} {mode}\n{Socket.TraceState}");
        } catch (Exception ex) { Trace.Mark("context-error", ex.GetType().Name + ": " + ex.Message); }
    }
}

internal partial class ElinNetClient
{
    internal string TraceState => $"handshake={_handshakePhase} pausedSend={_pauseUpdate} control={ControlMode} blocked={ControlInputBlocked} " +
        $"profileSession={_profileChannel?.Session} pendingRevision={_profileChannel?.Pending?.Revision}";
}

internal partial class ElinNetHost
{
    internal string TraceState => $"pausedSend={_pauseUpdate} profileSave={_profileSave?.Id} " +
        string.Join(";", ActiveRemoteCharas.Select(p => $"peer={p.Key}/actor={p.Value.uid}/input={AcceptsPlayerInput(p.Key)}"));
}
