using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using HeathenEngineering.SteamworksIntegration;
using HeathenEngineering.SteamworksIntegration.API;
using Steamworks;
using UnityEngine;

namespace ElinTogether.Net.Steam;

internal class SteamNetPeer : ISteamNetPeer, IDisposable
{
    private const int MemoryArenaInitialSize = 4 * (1 << 10);
    private const int MemoryArenaGrowthRatio = 2;

    private static readonly Func<int, IntPtr> _allocator = Marshal.AllocHGlobal;
    private static readonly Action<IntPtr> _deallocator = Marshal.FreeHGlobal;
    private static readonly Func<IntPtr, IntPtr, IntPtr> _reallocator = Marshal.ReAllocHGlobal;

    private static int _nextId = -1;
    private static readonly Dictionary<UserData, int> _peerIdHistory = [];

    // ReSharper disable once ChangeFieldTypeToSystemThreadingLock
    protected readonly object ArenaLock = new();

    internal NetworkFlightRecorder? Trace { get; set; }
    private int _pendingReliable, _pendingUnreliable, _unackedReliable;
    private long _queueTime;
    internal string TraceState => $"peer={Id} steam={User} handle={Connection} state={ConnectionState} " +
        $"rx={Stat.PacketsReceived}/{Stat.BytesReceived} tx={Stat.PacketsSent}/{Stat.BytesSent} rxAge={Stat.SecondsSinceLastReceive:F1} " +
        $"pendingReliable={_pendingReliable} pendingUnreliable={_pendingUnreliable} unacked={_unackedReliable} queueUs={_queueTime} ping={Stat.LastPingMs} qos={Stat.ConnectionQualityLocal}/{Stat.ConnectionQualityRemote}";

    public readonly HSteamNetConnection Connection;
    public readonly SteamNetworkingIdentity RemoteIdentity;
    protected readonly ISteamNetSerializer Serializer;

    protected IntPtr Arena;
    protected int ArenaSize;
    private bool _disposed;
    private EResult? _lastSendFailure;

    public SteamNetPeer(HSteamNetConnection connection, ISteamNetSerializer serializer)
    {
        Connection = connection;

        SteamNetworkingSockets.GetConnectionInfo(connection, out var info);
        RemoteIdentity = info.m_identityRemote;

        User = RemoteIdentity.GetSteamID64();
        Friends.Client.RequestUserInformation(User, true);

        if (_peerIdHistory.TryGetValue(User, out var preferredId)) {
            int current;
            while ((current = Volatile.Read(ref _nextId)) < preferredId) {
                Interlocked.CompareExchange(ref _nextId, preferredId, current);
            }
        } else {
            preferredId = Interlocked.Increment(ref _nextId);
        }
        Id = _peerIdHistory[User] = preferredId;

        Serializer = serializer;
        ArenaSize = MemoryArenaInitialSize;
        Arena = _allocator(ArenaSize);
    }

    public ESteamNetworkingConnectionState ConnectionState =>
        !NetShutdown.IsQuitting && SteamNetworkingSockets.GetConnectionInfo(Connection, out var info)
            ? info.m_eState
            : ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_None;

    public virtual int Id { get; }
    public UserData User { get; }

    public virtual bool IsConnected =>
        ConnectionState is
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting or
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_FindingRoute or
            ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected;

    public SteamNetPeerStat Stat => field ??= new();

    public virtual bool Send<T>(T message, SteamNetSendFlag sendFlags = SteamNetSendFlag.Reliable)
    {
        using var serialize = Trace?.Enter("serialize", typeof(T).Name);
        try {
            var bytes = Serializer.Serialize(message);
            Trace?.Mark("outgoing", NetworkPacketTrace.Describe(message!));
            return Send(bytes, sendFlags);
        } catch (Exception ex) {
            Trace?.Fault($"serialize/send type={typeof(T).Name}: {ex}");
            throw;
        }
    }

    public virtual bool Send(byte[] bytes, SteamNetSendFlag sendFlags = SteamNetSendFlag.Reliable)
    {
        if (NetShutdown.IsQuitting) {
            return false;
        }

        var size = bytes.Length;

        lock (ArenaLock) {
            if (Arena == IntPtr.Zero) {
                return false;
            }

            PinArena(size);

            Marshal.Copy(bytes, 0, Arena, size);

            // crash on native side cannot be handled
            using var send = Trace?.Enter("send", $"peer={Id} bytes={size}");
            var result = SteamNetworkingSockets.SendMessageToConnection(Connection, Arena, (uint)size, (int)sendFlags, out var messageNumber);
            RecordSend(bytes, result, messageNumber);
            if (result != EResult.k_EResultOK) {
                if (_lastSendFailure != result) {
                    EmpLog.Warning("Steam send failed: peer {Peer}, result {Result}, state {State}, bytes {Bytes}, receiveAge {ReceiveAge:F1}s",
                        Id, result, ConnectionState, size, Stat.SecondsSinceLastReceive);
                }
                _lastSendFailure = result;
                return false;
            }
            if (_lastSendFailure is not null) {
                EmpLog.Information("Steam sending recovered for peer {Peer} after {Result}", Id, _lastSendFailure);
                _lastSendFailure = null;
            }
        }

        Stat.Sent(size);
        UpdateRealtime();

        return true;
    }

    internal void RecordSend(byte[] bytes, EResult result, long messageNumber)
    {
        var type = bytes.Length >= 4 ? BitConverter.ToUInt32(bytes, 0).ToString("X8") : "short";
        Trace?.Mark($"tx/{Id}", $"message={messageNumber} hash={type} bytes={bytes.Length} result={result}");
        if (result != EResult.k_EResultOK) Trace?.Fault($"Steam send peer={Id} message={messageNumber} result={result} state={ConnectionState}");
    }

    protected void PinArena(int size)
    {
        if (size <= ArenaSize) {
            return;
        }

        var newSize = Math.Max(size, ArenaSize * MemoryArenaGrowthRatio);

        Arena = _reallocator(Arena, (IntPtr)newSize);
        ArenaSize = newSize;
    }

    public void UpdateRealtime()
    {
        const float pingAlpha = 0.2f;
        const float bandwidthAlpha = 0.15f;

        if (NetShutdown.IsQuitting) {
            return;
        }

        var status = new SteamNetConnectionRealTimeStatus_t();
        var discard = new SteamNetConnectionRealTimeLaneStatus_t();
        var result = SteamNetworkingSockets.GetConnectionRealTimeStatus(Connection, ref status, 0, ref discard);
        if (result != EResult.k_EResultOK) {
            return;
        }

        _pendingReliable = status.m_cbPendingReliable;
        _pendingUnreliable = status.m_cbPendingUnreliable;
        _unackedReliable = status.m_cbSentUnackedReliable;
        _queueTime = (long)status.m_usecQueueTime;
        Stat.LastPingMs = status.m_nPing;
        Stat.ConnectionQualityLocal = status.m_flConnectionQualityLocal;
        Stat.ConnectionQualityRemote = status.m_flConnectionQualityRemote;
        Stat.LastUpdated = DateTime.UtcNow;

        // use ema to smooth out the spikes
        Stat.AvgPingMs = Stat.AvgPingMs == 0f
            ? Stat.LastPingMs
            : Mathf.Lerp(Stat.AvgPingMs, Stat.LastPingMs, pingAlpha);

        Stat.AvgBpsOut = Stat.AvgBpsOut == 0f
            ? status.m_flOutBytesPerSec
            : Mathf.Lerp(Stat.AvgBpsOut, status.m_flOutBytesPerSec, bandwidthAlpha);

        Stat.AvgBpsIn = Stat.AvgBpsIn == 0f
            ? status.m_flInBytesPerSec
            : Mathf.Lerp(Stat.AvgBpsIn, status.m_flInBytesPerSec, bandwidthAlpha);
    }

#region Cleanups

    ~SteamNetPeer()
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

        // the finalizer runs off-thread and must not take ArenaLock, so swap the pointer out
        // atomically instead: a racing Send then sees IntPtr.Zero and bails rather than writing
        // into freed native memory
        var arena = Interlocked.Exchange(ref Arena, IntPtr.Zero);
        if (arena != IntPtr.Zero) {
            _deallocator(arena);
        }
    }

#endregion
}