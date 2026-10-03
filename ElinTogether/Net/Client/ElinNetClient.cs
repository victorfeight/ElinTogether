using System;
using ElinTogether.Common;
using ElinTogether.LangMod;
using ElinTogether.Models;
using ElinTogether.Net.Steam;
using HeathenEngineering.SteamworksIntegration;
using ReflexCLI.UI;

namespace ElinTogether.Net;

internal partial class ElinNetClient : ElinNetBase
{
    private DateTime _lastTimeout = DateTime.Now;
    private bool _reportedReceiveStall;

    public override bool IsHost => false;
    public ISteamNetPeer Host => Socket.FirstPeer;
    public bool IsJoiningLobby { get; private set; }

    protected override void Update()
    {
        base.Update();

        if (IsConnected) {
            // A connected Steam handle is not proof that game packets are arriving.
            // Do not discard the player's live profile automatically on a stall.
            var stalled = _handshakePhase == NetHandshakePhase.Joined &&
                Host.Stat.SecondsSinceLastReceive > Math.Max(15, EmpConfig.Policy.Timeout.Value);
            if (stalled && !_reportedReceiveStall) {
                EmpLog.Warning("Host packet stream stalled: no packets for {Seconds:F1}s, tick {Tick}, received {Received}, sent {Sent}, buffered {Buffers}",
                    Host.Stat.SecondsSinceLastReceive, Session.Tick, Host.Stat.PacketsReceived,
                    Host.Stat.PacketsSent, Delta.GetCounts());
                EmpPop.Information("The host connection has stopped delivering game updates. You can use Reconnect in the multiplayer menu; unsaved personal profile data will be backed up first.");
            } else if (!stalled && _reportedReceiveStall) {
                EmpLog.Information("Host packet stream resumed at tick {Tick}", Session.Tick);
            }
            _reportedReceiveStall = stalled;
            _lastTimeout = DateTime.Now;
            return;
        }

        if (IsJoiningLobby && Session.Lobby.Current.HasServer) {
            if (long.TryParse(Session.Lobby.Current[$"connection_key_{UserData.Me}"], out var key) && key != 0L) {
                IsJoiningLobby = false;
                ConnectSteamUser(Session.Lobby.Current.GameServer.id);
            }
        }

#if !DEBUG
        var elapsed = DateTime.Now - _lastTimeout;
        if (elapsed.TotalSeconds > EmpConfig.Policy.Timeout.Value) {
            EmpPop.Information("emp_ui_timeout".lang());
            Session.ResetSession();
        }
#endif
    }

    public void ConnectLocalPort(ushort port = EmpConstants.LocalPort)
    {
        Stop();
        Socket.Connect(port);
    }

    public void ConnectSteamUser(UserData steamId)
    {
        Stop();
        Socket.Connect(steamId);
    }

    protected override void RegisterPackets()
    {
        Router.ShouldReceivePacket = ShouldReceiveHostPacket;

        // delta
        Router.RegisterHandler<ZoneDataResponse>(OnZoneDataResponse);
        Router.RegisterHandler<ZoneActivateResponse>(OnZoneActivateResponse);
        Router.RegisterHandler<WorldStateSnapshot>(OnWorldStateSnapshot);
        Router.RegisterHandler<WorldStateDeltaList>(OnWorldStateDeltaResponse);

        // integrity
        Router.RegisterHandler<NetIntegrityRequest>(OnNetHandshakeRequest);
        Router.RegisterHandler<NetIntegrityRejected>(OnNetHandshakeRejected);

        // source validation
        Router.RegisterHandler<SourceValidationRequest>(OnSourceValidationRequest);
        Router.RegisterHandler<SourceValidationFailed>(OnSourceValidationFailed);

        // session
        Router.RegisterHandler<SessionNewPlayerRequest>(OnSessionNewPlayerRequest);
        Router.RegisterHandler<SaveDataProbe>(OnSaveDataProbe);
        Router.RegisterHandler<SteamLobbyRequest>(OnSteamLobbyRequest);
        Router.RegisterHandler<SessionPlayersSnapshot>(OnSessionStatesUpdate);
        Router.RegisterHandler<NetSessionRules>(OnSessionRulesUpdate);
        Router.RegisterHandler<PlayerProfileReceipt>(OnPlayerProfileReceipt);
        Router.RegisterHandler<PlayerProfileRequest>(OnPlayerProfileRequest);
        Router.RegisterHandler<PlayerControlReply>(OnPlayerControlReply);
        Router.RegisterHandler<SessionReconnectRequest>(OnSessionReconnectRequest);
    }

    internal override void Stop()
    {
        CheckpointPersonalProfile();
        _profileChannel = null;
        base.Stop();

        if (!core.IsGameStarted) {
            return;
        }

        scene.Init(Scene.Mode.Title);
    }

#region Net Events

    /// <summary>
    ///     Net event: On connected to host
    /// </summary>
    protected override void OnPeerConnected(ISteamNetPeer host)
    {
        if (!host.IsConnected) {
            EmpPop.Information("emp_error_connection".lang());
            Session.ResetSession();
            return;
        }

        // reconnects
        BeginHandshake();

        EmpPop.Information("emp_connecting_host".lang(), Host);

        // Own this wait on the connection. The game helper shares a static coroutine
        // handle which remains stale if a disconnect stops its owning coroutine.
        StartCoroutine(StartWorldStateUpdateWhenReady());

#if DEBUG
        if (!IsDebugGuiActive) {
            StartDebugGui();
        }
#endif
    }

    /// <summary>
    ///     Net event: On disconnected from host.
    ///     Fully clean up resources and return to title.
    /// </summary>
    protected override void OnPeerDisconnected(ISteamNetPeer host, string disconnectInfo)
    {
        StopWorldStateUpdate();
        StopAllCoroutines();

        if (ReflexUIManager.IsConsoleOpen()) {
            ReflexUIManager.StaticClose();
        }

        EmpLog.Warning("Disconnected from host: {Reason}",
            disconnectInfo);

        Session.ResetSession();

        EmpPop.Information("emp_disconnected_host".Loc(disconnectInfo));
    }

#endregion
}
