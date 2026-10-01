using System;
using System.Linq;
using ElinTogether.Components;
using ElinTogether.Elements;
using ElinTogether.Models;
using ElinTogether.Patches;

namespace ElinTogether.Net;

internal partial class ElinNetClient
{
    internal PlayerControlMode ControlMode { get; private set; }
    private Guid _controlRequest;
    private Guid _controlResume;
    internal bool ControlRequestPending => _controlRequest != Guid.Empty;
    internal bool ControlInputBlocked => ControlMode != PlayerControlMode.Human || _controlRequest != Guid.Empty;

    internal void TogglePlayerControl()
    {
        if (_controlRequest != Guid.Empty || ControlMode == PlayerControlMode.Resuming) return;
        var takeBreak = ControlMode == PlayerControlMode.Human;
        if (takeBreak && !CheckpointPersonalProfile()) return;
        foreach (var queued in player.queues.list.ToArray()) player.queues.Remove(queued);
        WorldStateDeltaUpdate();
        _controlRequest = Guid.NewGuid();
        if (!Host.Send(new PlayerControlRequest { Id = _controlRequest, TakeBreak = takeBreak })) {
            _controlRequest = Guid.Empty;
            EmpPop.Information("Could not request a control change. Check the connection.");
        }
    }

    private void OnPlayerControlReply(PlayerControlReply reply)
    {
        if (reply.Id == Guid.Empty || (reply.Id != _controlRequest && reply.Id != _controlResume)) return;
        _controlRequest = Guid.Empty;
        ControlMode = reply.Mode;
        if (reply.Mode == PlayerControlMode.Resuming) _controlResume = reply.Id;
        else _controlResume = Guid.Empty;
        Delta.ClearOut();
        ActionModeCombat.ClearControlInput();
        if (ControlMode == PlayerControlMode.Companion) pc.SetAI(GoalRemote.Default);
        else if (ControlMode == PlayerControlMode.Human) pc.SetNoGoal();
        if (reply.Error is not null) EmpPop.Information(reply.Error);
        LayerElinTogether.Instance?.Reopen();
    }

    private void FinishControlResume()
    {
        if (_controlResume != Guid.Empty && ControlMode == PlayerControlMode.Resuming)
            Host.Send(new PlayerControlReady { Id = _controlResume });
    }
}
