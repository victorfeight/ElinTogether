using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ElinTogether.Common;
using ElinTogether.Models;
using ElinTogether.Net.Steam;
using HeathenEngineering.SteamworksIntegration;

namespace ElinTogether.Net;

internal partial class ElinNetHost : ElinNetBase
{
    internal readonly Dictionary<int, NetPeerState> States = [];
    private readonly HashSet<int> _readyRemotePeers = [];
    private readonly DisconnectedPlayerCompanions _disconnectedCompanions = new();

    public override bool IsHost => true;

    internal void StartServer(bool localUdp = false)
    {
        Stop();
        StopWorldStateUpdate();

        if (SoloCharacterSelection.IsSoloCharacter) {
            EmpPop.Information("Return to the original host character in Saved characters before hosting multiplayer.");
            Session.ResetSession();
            return;
        }

        if (!core.IsGameStarted || player?.chara?.homeBranch?.owner is null) {
            EmpLog.Warning("Cannot start server: game not started or no land claimed");
            EmpPop.Debug("emp_ui_unclaimed_zone".lang());
            Session.ResetSession();
            return;
        }

        if (SoloCompanions.PrepareHosting() is { } companionReason) {
            EmpPop.Information(companionReason);
            Session.ResetSession();
            return;
        }

        Session.Lobby.CreateLobby();

        try {
            if (localUdp) {
                Socket.StartServerUdp();
            } else {
                Socket.StartServerSdr();
            }
        } catch {
            Session.ResetSession();
            throw;
        }

        Scheduler.Subscribe(DisconnectInactive, 1);
        Scheduler.Subscribe(RemoveStaleIntegrityCheck, 2);

        // host also registers self state
        var selfState = States[0] = new() {
            Index = 0,
            User = UserData.Me,
            CharaUid = player.uidChara,
        };

        // setup session states
        Session.Player = pc;
        Session.Self = selfState;
        Session.CurrentPlayers.Add(selfState);
        Session.SharedSpeed = NetSession.Instance.Rules.UseSharedSpeed
            ? SharedSpeed
            : -1;

        EmpPop.Information("emp_server_started".lang());

        CardCache.CacheCurrentZone();

        StartWorldStateUpdate();
    }

    protected override void RegisterPackets()
    {
        Router.ShouldReceivePacket = ShouldReceivePeerPacket;

        Router.RegisterHandler<NetIntegrityResponse>(OnNetHandshakeResponse);

        Router.RegisterHandler<SessionNewPlayerResponse>(OnSessionNewPlayerResponse);
        Router.RegisterHandler<MapDataRequest>(OnMapDataRequest);
        Router.RegisterHandler<ZoneDataReceivedResponse>(OnZoneDataReceivedResponse);
        Router.RegisterHandler<WorldStateRequest>(OnWorldStateRequest);
        Router.RegisterHandler<WorldStateDeltaList>(OnWorldStateDeltaResponse);
        Router.RegisterHandler<CharaStateSnapshot>(OnClientRemoteCharaSnapshot);
        Router.RegisterHandler<PlayerProfileCheckpoint>(OnPlayerProfileCheckpoint);
        Router.RegisterHandler<PlayerControlRequest>(OnPlayerControlRequest);
        Router.RegisterHandler<PlayerControlReady>(OnPlayerControlReady);

        // source validation
        Router.RegisterHandler<SourceValidationResponse>(OnSourceValidationResponse);
        Router.RegisterHandler<SourceValidationContinue>(OnSourceValidationContinue);
    }

    private void Broadcast<T>(T packet)
    {
        Socket.Broadcast.Send(packet);
    }

    protected void DisconnectInactive()
    {
        foreach (var peer in Socket.Peers) {
            if (!States.TryGetValue(peer.Id, out var state)) {
                continue;
            }

            if (state.LastReceivedTick == -1) {
                continue;
            }

            // client has not been responding after 25 ticks
            if (!peer.IsConnected && Session.Tick - state.LastReceivedTick > 25) {
                Socket.Disconnect(peer, EmpDisconnectInfo.InactivePeer);
            }
        }

        // remove all left over chara
        foreach (var actor in _disconnectedCompanions.Actors.ToArray()) {
            if (EmpConfig.Server.DisconnectedPlayerAI.Value && actor.party == pc.party) continue;
            _disconnectedCompanions.Release(actor);
            RemoveRemoteChara(actor);
        }
        foreach (var chara in _map.charas.ToArray()) {
            if (!chara.IsPC && chara.GetBool("remote_chara") && !ActiveRemoteCharas.Values.Contains(chara) &&
                !_disconnectedCompanions.Contains(chara)) {
                RemoveRemoteChara(chara);
            }
        }
    }

#region Net Events

    protected override void OnPeerConnected(ISteamNetPeer peer)
    {
        var sw = Stopwatch.StartNew();
        while (peer.User.Name is null && sw.ElapsedMilliseconds <= 500) {
            // do a spin wait to pin the username
        }

        EmpPop.Information("emp_player_connected".lang(), peer);

        // shou lai
        BeginHandshake(peer);

#if DEBUG
        if (!IsDebugGuiActive) {
            StartDebugGui();
        }
#endif
    }

    protected override void OnPeerDisconnected(ISteamNetPeer peer, string disconnectInfo)
    {
        EmpPop.Information("emp_player_disconnected".lang(), peer, disconnectInfo);

        _handshakes.Remove(peer.Id);
        _profileChannels.Remove(peer.Id);
        _controlResumes.Remove(peer.Id);
        var ready = _readyRemotePeers.Remove(peer.Id);
        _idleReports.Remove(peer.Id);
        Delta.DiscardIncomingPeer(peer.Id);
        WishInteraction.ReleasePeer(peer.Id);
        PendingRebind.ReleasePeer(peer.Id);

        if (States.Remove(peer.Id, out var state)) {
            Session.CurrentPlayers.Remove(state);
            if (ActiveRemoteCharas.Remove(peer.Id, out var remoteChara)) {
                WorldStateSnapshot.CachedRemoteSnapshots.RemoveAll(s => s.Owner.Uid == remoteChara.uid);
                ShopTrade.Release(remoteChara);
                var keepAI = DisconnectedPlayerCompanions.AllowsTakeover(
                    EmpConfig.Server.DisconnectedPlayerAI.Value, ready,
                    Session.Connection == this && core.IsGameStarted && !game.isLoading && !NetShutdown.IsQuitting,
                    disconnectInfo);
                if (!keepAI || !_disconnectedCompanions.TryBegin(remoteChara)) {
                    RemoveRemoteChara(remoteChara);
                    EmpLog.Information("Player {PlayerName} remote chara {Uid} parked for reconnect", state.User.Name, remoteChara.uid);
                }
            }

            _zone.RefreshCriminal();
        }

        EmpLog.Debug("Player {PlayerName} disconnected. {Remaining} players remaining",
            state?.User.Name ?? "unknown", States.Count);

        Session.SharedSpeed = Session.Rules.UseSharedSpeed && States.Count > 0 ? SharedSpeed : -1;
        Broadcast(SessionPlayersSnapshot.Create());

        // keep ticking but no update
        if (States.Count == 0) {
            PauseWorldStateUpdate();
            StopDebugGui();
        }
    }

#endregion
}
