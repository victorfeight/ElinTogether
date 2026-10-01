using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Elements;
using ElinTogether.Models;
using ElinTogether.Net.Steam;
using ElinTogether.Patches;

namespace ElinTogether.Net;

internal partial class ElinNetHost
{
    private readonly Dictionary<int, Guid> _controlResumes = [];

    internal bool IsCompanionControlled(Chara actor) => States.Values.Any(s =>
        s.CharaUid == actor.uid && s.Control == PlayerControlMode.Companion);

    internal bool AcceptsPlayerInput(int peer) => ActiveRemoteCharas.ContainsKey(peer) &&
        States.TryGetValue(peer, out var state) && state.Control == PlayerControlMode.Human;

    internal void ToggleHostControl()
    {
        if (!States.TryGetValue(0, out var state)) return;
        var takingBreak = state.Control == PlayerControlMode.Human;
        if (takingBreak && CannotTakeBreak(pc) is { } error) { EmpPop.Information(error); return; }
        ActionModeCombat.ClearControlInput();
        foreach (var queued in player.queues.list.ToArray()) player.queues.Remove(queued);
        DisconnectedPlayerCompanions.StopActions(pc);
        state.Control = takingBreak ? PlayerControlMode.Companion : PlayerControlMode.Human;
        if (takingBreak) PlayerControlPatch.ChooseHostGoal();
        Broadcast(SessionPlayersSnapshot.Create());
    }

    internal void EndLocalControl()
    {
        if (!States.TryGetValue(0, out var state) || state.Control == PlayerControlMode.Human) return;
        state.Control = PlayerControlMode.Human;
        DisconnectedPlayerCompanions.StopActions(pc);
        ActionModeCombat.ClearControlInput();
    }

    private static string? CannotTakeBreak(Chara actor)
    {
        if (!core.IsGameStarted || game.isLoading || actor.isDead || actor.currentZone != _zone)
            return "Wait until your living character has finished loading.";
        if (actor.conSleep is not null) return "Finish sleeping before taking a break.";
        if (actor.host is not null || actor.ride is not null || actor.parasite is not null)
            return "Dismount before taking a break.";
        if (OwnerProgressionAwards.Read(actor).Count != 0)
            return "Wait for your pending progression rewards, then try again.";
        return null;
    }

    private void OnPlayerControlRequest(PlayerControlRequest request, ISteamNetPeer peer)
    {
        if (request.Id == Guid.Empty || !States.TryGetValue(peer.Id, out var state) ||
            !ActiveRemoteCharas.TryGetValue(peer.Id, out var actor)) return;
        string? error = null;
        if (request.TakeBreak) {
            if (state.Control == PlayerControlMode.Resuming || !_readyRemotePeers.Contains(peer.Id))
                error = "Wait until character synchronization finishes.";
            else if (state.Control == PlayerControlMode.Human && (error = CannotTakeBreak(actor)) is null) {
                ShopTrade.Release(actor);
                WishInteraction.ReleasePeer(peer.Id);
                Delta.DiscardIncomingPeer(peer.Id);
                WorldStateSnapshot.CachedRemoteSnapshots.RemoveAll(s => s.Owner.Uid == actor.uid);
                DisconnectedPlayerCompanions.StopActions(actor);
                state.Control = PlayerControlMode.Companion;
                actor.RefreshFaithElement();
                actor.ChooseNewGoal();
            }
            peer.Send(new PlayerControlReply { Id = request.Id, Mode = state.Control, Error = error });
        } else if (state.Control == PlayerControlMode.Companion) {
            state.Control = PlayerControlMode.Resuming;
            DisconnectedPlayerCompanions.Dismount(actor);
            DisconnectedPlayerCompanions.StopActions(actor);
            Delta.DiscardIncomingPeer(peer.Id);
            _controlResumes[peer.Id] = request.Id;
            peer.Send(new PlayerControlReply { Id = request.Id, Mode = PlayerControlMode.Resuming });
            SendSaveProbe(actor, peer, request.Id);
        } else {
            peer.Send(new PlayerControlReply { Id = request.Id, Mode = state.Control,
                Error = state.Control == PlayerControlMode.Resuming ? "Character synchronization is still in progress." : null });
        }
        Broadcast(SessionPlayersSnapshot.Create());
        EmpLog.Information("Player control request {Request}: peer {Peer}, mode {Mode}, error {Error}",
            request.Id, peer.Id, States[peer.Id].Control, error);
    }

    private void OnPlayerControlReady(PlayerControlReady ready, ISteamNetPeer peer)
    {
        if (!_controlResumes.TryGetValue(peer.Id, out var request) || request != ready.Id ||
            !_readyRemotePeers.Contains(peer.Id) || !States.TryGetValue(peer.Id, out var state)) return;
        _controlResumes.Remove(peer.Id);
        state.Control = PlayerControlMode.Human;
        peer.Send(new PlayerControlReply { Id = ready.Id, Mode = PlayerControlMode.Human });
        Broadcast(SessionPlayersSnapshot.Create());
        EmpLog.Information("Human control restored for peer {Peer}, chara {Uid}", peer.Id, state.CharaUid);
    }
}
