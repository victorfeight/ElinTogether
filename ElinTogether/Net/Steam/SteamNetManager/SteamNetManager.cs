using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ElinTogether.Common;
using ElinTogether.Models;
using ElinTogether.Helper.Steam;
using Steamworks;

namespace ElinTogether.Net.Steam;

public partial class SteamNetManager(ISteamNetSerializer? serializer = null) : IDisposable
{
    private readonly IntPtr[] _batchedMessages = new IntPtr[EmpConstants.MaxBatchedMessages];
    private readonly SteamNetPeerBroadcast _broadcast = new(serializer ?? new SteamNetSerializer());
    private readonly List<SteamNetPeer> _peers = [];
    private readonly ISteamNetSerializer _serializer = serializer ?? new SteamNetSerializer();

    private readonly SteamNetPacketInbox _receivedPackets = new();
    private bool _waitingForPackets;
    private bool _disposed;
    private HSteamListenSocket _listenSocket;
    private ISteamNetListener? _listener;
    private HSteamNetPollGroup _pollGroup;

    internal NetworkFlightRecorder? Trace { get; set; }
    internal void RefreshTraceStats()
    {
        foreach (var peer in _peers) peer.UpdateRealtime();
    }
    internal string TraceState => $"pollGroup={_pollGroup} disposed={_disposed} waiting={_waitingForPackets} inbox={_receivedPackets.Count} " +
        string.Join(";", _peers.Select(p => p.TraceState));

    public bool IsHost { get; private set; }
    public bool IsListening { get; private set; }

    /// <summary>
    ///     Fake placeholder peer
    /// </summary>

    private ISteamNetPeer FakePeer => field ??= new SteamNetPeerFake();

    /// <summary>
    ///     The first connected peer, mainly client->host
    /// </summary>
    public ISteamNetPeer FirstPeer => _peers.FirstOrDefault(p => p.IsConnected) ?? FakePeer;

    /// <summary>
    ///     The optimized broadcast peer, mainly host->multiple clients
    /// </summary>
    public ISteamNetPeer Broadcast => _broadcast;

    /// <summary>
    ///     All connected peers
    /// </summary>
    public IReadOnlyList<ISteamNetPeer> Peers => [.._peers];

    /// <summary>
    ///     Any connected peers
    /// </summary>
    public bool IsConnected => _peers.Any(p => p.IsConnected);

    /// <summary>
    ///     Initialize poll group and optionally steam lobby
    /// </summary>
    public void Initialize(ISteamNetListener listener)
    {
        if (_pollGroup != HSteamNetPollGroup.Invalid) {
            return;
        }

        _broadcast.Trace = Trace;
        _pollGroup = SteamNetworkingSockets.CreatePollGroup();
        Trace?.Mark("connection", $"create poll group={_pollGroup}");
        _listener = listener;

        SteamCallback<SteamNetConnectionStatusChangedCallback_t>.Add(HandleStatusChange);
    }

    /// <summary>
    ///     Poll batched events on all peers
    /// </summary>
    public void Poll(Func<object, bool>? accept = null)
    {
        if (_pollGroup == HSteamNetPollGroup.Invalid || NetShutdown.IsQuitting) {
            return;
        }

        using var poll = Trace?.Enter("poll", $"ReceiveMessages group={_pollGroup} profileOnly={accept is not null}");
        var received = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(_pollGroup, _batchedMessages, _batchedMessages.Length);
        if (received < 0) Trace?.Fault($"ReceiveMessages returned {received} group={_pollGroup}");

        for (var i = 0; i < received; ++i) {
            var msg = SteamNetworkingMessage_t.FromIntPtr(_batchedMessages[i]);
            try {
                var peer = _peers.Find(p => p.Connection == msg.m_conn);
                if (peer is null) {
                    Trace?.Fault($"received unknown connection={msg.m_conn} message={msg.m_nMessageNumber} bytes={msg.m_cbSize}");
                    continue;
                }
                Trace?.Mark($"rx/{peer.Id}", $"message={msg.m_nMessageNumber} bytes={msg.m_cbSize}");
                using var decode = Trace?.Enter("decode", $"peer={peer.Id} message={msg.m_nMessageNumber} bytes={msg.m_cbSize}");

                var bytes = new byte[msg.m_cbSize];
                Marshal.Copy(msg.m_pData, bytes, 0, msg.m_cbSize);

                var (typeHash, payload) = SteamNetSerializer.ExtractTypeAndPayload(bytes);
                var type = SteamNetTypeRegistry.Resolve(typeHash);
                if (type == null) {
                    EmpLog.Warning("Failed to parse type hash {TypeHash}",
                        typeHash);
                    continue;
                }

                var packet = _serializer.Deserialize(payload, type);
                _receivedPackets.Add(packet, peer, msg.m_nMessageNumber);
                Trace?.Mark($"decoded/{peer.Id}", $"message={msg.m_nMessageNumber} {NetworkPacketTrace.Describe(packet)}");

                peer.Stat.Received(msg.m_cbSize);
            } catch (Exception ex) {
                Trace?.Fault($"decode failed connection={msg.m_conn} message={msg.m_nMessageNumber}: {ex}");
                throw;
            } finally {
                SteamNetworkingMessage_t.Release(_batchedMessages[i]);
            }
        }

        // Release the entire native batch before invoking handlers. A save can
        // receive its replies recursively without overwriting native pointers.
        _receivedPackets.Dispatch((packet, peer) => _listener?.OnMessageReceived(packet, peer), accept);
    }

    // Only invoked by a synchronous save/disconnect exchange, never a scheduler.
    internal bool WaitForPackets(Func<object, bool> accept, Func<bool> complete, Func<bool> valid)
    {
        if (_waitingForPackets) { Trace?.Fault("profile wait rejected: nested wait"); return false; }
        _waitingForPackets = true;
        using var wait = Trace?.Enter("profile-wait", "bounded receive wait");
        try {
            var invalidated = false;
            var result = PlayerProfileReplyWait.Run(() => Poll(accept), complete, () => {
                var current = valid() && !NetShutdown.IsQuitting;
                invalidated |= !current;
                return current;
            });
            Trace?.Mark("profile-wait", $"result={result} invalidated={invalidated} quitting={NetShutdown.IsQuitting}");
            if (!result) Trace?.Fault("profile wait failed");
            return result;
        } finally { _waitingForPackets = false; }
    }

#region Connection Management

    /// <summary>
    ///     Ungracefully discard the listen socket
    /// </summary>
    public void Stop()
    {
        _receivedPackets.Clear();
        foreach (var peer in Peers) {
            Disconnect(peer, EmpDisconnectInfo.HostShutdown);
        }

        DiscardListenSocket();
    }

    internal void Shutdown()
    {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _receivedPackets.Clear();

        SteamCallback<SteamNetConnectionStatusChangedCallback_t>.Remove(HandleStatusChange);

        foreach (var peer in _peers) {
            EmpLog.Verbose("Closing connection {@Peer}",
                peer);

            SteamNetworkingSockets.SetConnectionPollGroup(peer.Connection, HSteamNetPollGroup.Invalid);

            SteamNetworkingSockets.CloseConnection(peer.Connection, 0, EmpDisconnectInfo.HostShutdown, false);

            peer.Dispose();
        }

        _peers.Clear();
        _broadcast.ClearTargets();
        _broadcast.Dispose();

        DiscardListenSocket();
        DestroyPollGroup();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Disconnect with reason
    /// </summary>
    public void Disconnect(ISteamNetPeer disconnectPeer, string reason)
    {
        if (disconnectPeer is not SteamNetPeer peer) {
            return;
        }

        if (!_peers.Contains(peer)) {
            return;
        }

        EmpLog.Verbose("Closing connection {@Peer}",
            peer);

        // reciprocal disconnect
        _listener?.OnPeerDisconnected(peer, reason);

        if (!NetShutdown.IsQuitting) {
            SteamNetworkingSockets.SetConnectionPollGroup(peer.Connection, HSteamNetPollGroup.Invalid);

            SteamNetworkingSockets.CloseConnection(peer.Connection, 0, reason, true);
        }

        Trace?.Expect($"rx/{peer.Id}", false);
        Trace?.Mark("connection", $"remove peer={peer.Id} reason={reason}");
        _peers.Remove(peer);
        _broadcast.RemoveTarget(peer);

        peer.Dispose();
    }

    private ISteamNetPeer AddConnection(HSteamNetConnection connection)
    {
        EmpLog.Verbose("Adding connection handle to poll group");

        if (_peers.FirstOrDefault(p => p.Connection == connection) is { } duplicate) {
            EmpLog.Verbose("Duplicate connection! Ignored");
            return duplicate;
        }

        var peer = new SteamNetPeer(connection, _serializer) { Trace = Trace };
        _receivedPackets.Trace = Trace;
        Trace?.Expect($"rx/{peer.Id}", true);
        Trace?.Mark("connection", $"add peer={peer.Id} steam={peer.User} handle={connection} state={peer.ConnectionState}");
        if (!peer.IsConnected) {
            SteamNetworkingSockets.CloseConnection(peer.Connection, 0, EmpDisconnectInfo.RemoteClosed, true);
            return FakePeer;
        }

        peer.User.SetPlayedWith();

        var grouped = SteamNetworkingSockets.SetConnectionPollGroup(connection, _pollGroup);
        Trace?.Mark("connection", $"assign peer={peer.Id} handle={connection} group={_pollGroup} success={grouped}");
        if (!grouped) Trace?.Fault($"poll group assignment failed peer={peer.Id}");

        _peers.Add(peer);
        _broadcast.AddTarget(peer);

        _listener?.OnPeerConnected(peer);

        EmpLog.Debug("Player {PlayerName} {RemoteIdentity} connected, claiming index {PeerIndex}",
            peer.User.Name, peer.User, peer.Id);

        return peer;
    }

    private void HandleStatusChange(SteamNetConnectionStatusChangedCallback_t status)
    {
        var connection = status.m_hConn;
        Trace?.Mark("connection", $"callback handle={connection} old={status.m_eOldState} new={status.m_info.m_eState} end={status.m_info.m_eEndReason} detail={status.m_info.m_szEndDebug}");

        switch (status.m_info.m_eState) {
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                if (IsHost) {
                    // we only accept as host
                    AcceptIfHost(connection, status.m_info);
                }

                break;
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                // this should only call for host
                // but check just in case
                if (IsHost) {
                    AddConnection(connection);
                }

                break;
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                var peer = _peers.FirstOrDefault(p => p.Connection == connection);
                if (peer is not null) {
                    Disconnect(peer, status.m_info.m_szEndDebug);
                }

                break;
        }
    }

#endregion

#region Cleanups

    ~SteamNetManager()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) {
            return;
        }

        _disposed = true;

        if (!disposing) {
            return;
        }

        _broadcast.Dispose();

        if (NetShutdown.IsQuitting) {
            return;
        }

        SteamCallback<SteamNetConnectionStatusChangedCallback_t>.Remove(HandleStatusChange);

        DiscardListenSocket();
        DestroyPollGroup();
    }

#endregion
}
