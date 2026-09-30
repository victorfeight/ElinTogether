using System;
using ElinTogether.Models;

namespace ElinTogether.Net;

internal partial class ElinNetClient
{
    private PlayerProfileChannel? _profileChannel;
    // Explicit save/lifecycle call only. No polling, debounce or retry timer.
    internal bool CheckpointPersonalProfile()
    {
        if (_profileChannel is null || !IsConnected || core.game is null || game.isLoading ||
            !core.IsGameStarted || Session.Player != pc || pc.uid != _profileChannel.OwnerUid ||
            FaithTransactions.Actor is not null) return _profileChannel is null || !IsConnected;
        try {
            if (_profileChannel.Capture(pc, player) is not { } checkpoint) return true;
            var channel = _profileChannel;
            var savedGame = game;
            if (Host.Send(checkpoint) && Socket.WaitForPackets(IsProfilePacket,
                () => channel.Pending is null,
                () => IsConnected && game == savedGame && _profileChannel == channel)) return true;
            EmpPop.Information("Your personal profile was not acknowledged. Stay connected and try saving again.");
        } catch (Exception ex) {
            EmpLog.Warning(ex, "Could not checkpoint personal player profile for {Uid}", pc.uid);
        }
        return false;
    }

    private static bool IsProfilePacket(object packet) => packet is PlayerProfileRequest or PlayerProfileReceipt;

    private void OnPlayerProfileRequest(PlayerProfileRequest request)
    {
        if (_profileChannel is not { } channel || request.Session != channel.Session ||
            request.OwnerUid != channel.OwnerUid || request.RequestId == Guid.Empty ||
            core.game is null || game.isLoading || Session.Player != pc || !core.IsGameStarted ||
            FaithTransactions.Actor is not null) return;
        try {
            // Reply even when unchanged: the request proves freshness for THIS save.
            if (channel.Capture(pc, player, request.RequestId) is { } checkpoint) Host.Send(checkpoint);
        } catch (Exception ex) {
            EmpLog.Warning(ex, "Could not capture requested personal profile for {Uid}", request.OwnerUid);
        }
    }

    private void OnPlayerProfileReceipt(PlayerProfileReceipt receipt) => _profileChannel?.Acknowledge(receipt);
}
