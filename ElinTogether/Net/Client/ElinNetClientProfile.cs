using System;
using ElinTogether.Models;

namespace ElinTogether.Net;

internal partial class ElinNetClient
{
    private PlayerProfileChannel? _profileChannel;
    private bool _profileDeparturePrepared;
    // Explicit save/lifecycle call only. No polling, debounce or retry timer.
    internal bool CheckpointPersonalProfile(bool notifyFailure = true)
    {
        if (_profileDeparturePrepared) return true;
        if (ControlMode != PlayerControlMode.Human) return true;
        if (_profileChannel is null || !IsConnected || core.game is null || game.isLoading ||
            !core.IsGameStarted || Session.Player != pc || pc.uid != _profileChannel.OwnerUid ||
            FaithTransactions.Actor is not null) {
            EmpLog.Information("ProfileTrace checkpoint-skipped channel={Channel} connected={Connected} loading={Loading} started={Started} ownerMatch={OwnerMatch} faithBusy={FaithBusy}",
                _profileChannel is not null, IsConnected, core.game?.isLoading, core.IsGameStarted,
                core.game?.player?.chara is { } actor && Session.Player == actor && actor.uid == _profileChannel?.OwnerUid,
                FaithTransactions.Actor is not null);
            return _profileChannel is null || !IsConnected;
        }
        try {
            if (_profileChannel.Capture(pc, player) is not { } checkpoint) return true;
            var channel = _profileChannel;
            var savedGame = game;
            var sent = Host.Send(checkpoint);
            EmpLog.Information("ProfileTrace client-send uid={Uid} session={Session} revision={Revision} sent={Sent}",
                checkpoint.OwnerUid, checkpoint.Session, checkpoint.Revision, sent);
            if (sent && Socket.WaitForPackets(IsProfilePacket,
                () => channel.Pending is null,
                () => IsConnected && game == savedGame && _profileChannel == channel)) return true;
            EmpLog.Warning("ProfileTrace checkpoint-failed uid={Uid} session={Session} revision={Revision} sent={Sent} connected={Connected} sameGame={SameGame} sameChannel={SameChannel}",
                checkpoint.OwnerUid, checkpoint.Session, checkpoint.Revision, sent, IsConnected, game == savedGame, _profileChannel == channel);
            if (notifyFailure) EmpPop.Information("Your personal profile was not acknowledged. Stay connected and try saving again.");
        } catch (Exception ex) {
            EmpLog.Warning(ex, "Could not checkpoint personal player profile for {Uid}", pc.uid);
        }
        return false;
    }

    // Recovery cannot require a reply from the connection being recovered.
    // Keep the unacknowledged record on disk before the old world is torn down.
    private bool PrepareProfileForReconnect()
    {
        try {
            // A disconnected socket must not turn the normal checkpoint's early
            // return into permission to discard uncaptured local changes.
            if (!IsConnected && ControlMode == PlayerControlMode.Human && _profileChannel is { } channel &&
                core.game is not null && !game.isLoading && core.IsGameStarted && Session.Player == pc &&
                pc.uid == channel.OwnerUid && FaithTransactions.Actor is null) channel.Capture(pc, player);
            if (CheckpointPersonalProfile(notifyFailure: false) && _profileChannel?.Pending is null) {
                _profileDeparturePrepared = true;
                return true;
            }
            if (_profileChannel?.Pending is not { } pending) {
                EmpPop.Information("Cannot reconnect safely: the personal profile could not be captured.");
                return false;
            }
            var path = PlayerProfileRecovery.Save(CorePath.PathBackup, Session.SessionId, pending);
            EmpLog.Warning("Reconnecting with unacknowledged personal profile backed up to {Path}; actor {Uid}, revision {Revision}",
                path, pending.OwnerUid, pending.Revision);
            EmpPop.Information("Reconnecting using the host's last saved personal profile. Your unacknowledged profile was backed up locally; its path is in the session log.");
            _profileDeparturePrepared = true;
            return true;
        } catch (Exception ex) {
            EmpLog.Warning(ex, "Cannot preserve personal profile before reconnect");
            EmpPop.Information("Reconnect stopped because the personal profile backup could not be written. Check the session log.");
            return false;
        }
    }

    private static bool IsProfilePacket(object packet) => packet is PlayerProfileRequest or PlayerProfileReceipt;

    private void OnPlayerProfileRequest(PlayerProfileRequest request)
    {
        EmpLog.Information("ProfileTrace client-request uid={Uid} session={Session} request={Request}", request.OwnerUid, request.Session, request.RequestId);
        var channel = _profileChannel;
        var reason = ControlMode != PlayerControlMode.Human ? "control-not-human" :
            channel is null ? "channel-missing" : request.Session != channel.Session ? "session-mismatch" :
            request.OwnerUid != channel.OwnerUid ? "owner-mismatch" : request.RequestId == Guid.Empty ? "request-empty" :
            core.game is null ? "game-missing" : game.isLoading ? "loading" : Session.Player != pc ? "player-mismatch" :
            !core.IsGameStarted ? "game-not-started" : FaithTransactions.Actor is not null ? "faith-busy" : null;
        if (reason is not null) {
            EmpLog.Warning("ProfileTrace client-request-rejected uid={Uid} request={Request} reason={Reason}", request.OwnerUid, request.RequestId, reason);
            return;
        }
        try {
            // Reply even when unchanged: the request proves freshness for THIS save.
            if (channel!.Capture(pc, player, request.RequestId) is { } checkpoint) {
                var sent = Host.Send(checkpoint);
                EmpLog.Information("ProfileTrace client-reply uid={Uid} session={Session} revision={Revision} request={Request} sent={Sent}",
                    checkpoint.OwnerUid, checkpoint.Session, checkpoint.Revision, checkpoint.RequestId, sent);
            } else {
                EmpLog.Warning("ProfileTrace client-capture-empty uid={Uid} request={Request}", request.OwnerUid, request.RequestId);
            }
        } catch (Exception ex) {
            EmpLog.Warning(ex, "Could not capture requested personal profile for {Uid}", request.OwnerUid);
        }
    }

    private void OnPlayerProfileReceipt(PlayerProfileReceipt receipt)
    {
        var accepted = _profileChannel?.Acknowledge(receipt) == true;
        EmpLog.Information("ProfileTrace client-receipt uid={Uid} session={Session} revision={Revision} request={Request} accepted={Accepted} channelSession={ChannelSession} pendingRevision={PendingRevision}",
            receipt.OwnerUid, receipt.Session, receipt.Revision, receipt.RequestId, accepted, _profileChannel?.Session, _profileChannel?.Pending?.Revision);
    }
}
