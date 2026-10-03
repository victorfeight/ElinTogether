using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Models;
using ElinTogether.Net.Steam;

namespace ElinTogether.Net;

internal partial class ElinNetHost
{
    private readonly Dictionary<int, PlayerProfileChannel> _profileChannels = [];
    private PlayerProfileSaveBarrier? _profileSave;

    internal bool PreparePersonalProfilesForSave()
    {
        if (_profileSave is not null) return false;
        var controlledCount = ActiveRemoteCharas.Keys.Count(AcceptsPlayerInput);
        if (controlledCount == 0) return true;
        var savedGame = game;
        var peers = Socket.Peers.Where(p => AcceptsPlayerInput(p.Id)).ToArray();
        var barrier = new PlayerProfileSaveBarrier();
        _profileSave = barrier;
        try {
            if (peers.Length != controlledCount) return SaveFailed();
            foreach (var peer in peers) {
                if (!peer.IsConnected || !_profileChannels.TryGetValue(peer.Id, out var channel)) return SaveFailed();
                var request = barrier.Add(peer.Id, channel.OwnerUid, channel.Session);
                var sent = peer.Send(request);
                EmpLog.Information("ProfileTrace host-request peer={Peer} uid={Uid} session={Session} request={Request} sent={Sent}",
                    peer.Id, request.OwnerUid, request.Session, request.RequestId, sent);
                if (!sent) return SaveFailed();
            }
            if (!Socket.WaitForPackets(p => p is PlayerProfileCheckpoint,
                () => barrier.Complete,
                () => game == savedGame && peers.All(p => p.IsConnected &&
                    ActiveRemoteCharas.TryGetValue(p.Id, out var actor) && barrier.Owns(p.Id, actor.uid))))
                return SaveFailed();
            EmpLog.Information("Personal profiles ready for host save: request {Request}, players {Count}", barrier.Id, peers.Length);
            return true;
        } catch (Exception ex) {
            EmpLog.Warning(ex, "Failed to collect personal profiles before saving");
            return SaveFailed();
        } finally { _profileSave = null; }
    }

    private static bool SaveFailed()
    {
        EmpPop.Information("Save cancelled: a player's personal profile could not be received. Check their connection and try saving again.");
        return false;
    }

    private void OnPlayerProfileCheckpoint(PlayerProfileCheckpoint checkpoint, ISteamNetPeer peer)
    {
        EmpLog.Information("ProfileTrace host-receive peer={Peer} uid={Uid} session={Session} revision={Revision} request={Request}",
            peer.Id, checkpoint.OwnerUid, checkpoint.Session, checkpoint.Revision, checkpoint.RequestId);
        if (!AcceptsPlayerInput(peer.Id)) { Reject("input-not-owned"); return; }
        if (!ActiveRemoteCharas.TryGetValue(peer.Id, out var actor)) { Reject("actor-missing"); return; }
        if (actor.IsPC) { Reject("actor-is-host-pc"); return; }
        if (!_profileChannels.TryGetValue(peer.Id, out var channel)) { Reject("channel-missing"); return; }
        if (channel.RejectionReason(actor, checkpoint) is { } reason) { Reject(reason); return; }

        void Reject(string reason) => EmpLog.Warning("ProfileTrace host-reject peer={Peer} uid={Uid} session={Session} revision={Revision} reason={Reason}",
            peer.Id, checkpoint.OwnerUid, checkpoint.Session, checkpoint.Revision, reason);
        try {
            if (channel.Accept(actor, checkpoint) is { } receipt) {
                _profileSave?.Receive(peer.Id, receipt);
                var sent = peer.Send(receipt);
                EmpLog.Information("ProfileTrace host-receipt peer={Peer} uid={Uid} session={Session} revision={Revision} request={Request} sent={Sent}",
                    peer.Id, receipt.OwnerUid, receipt.Session, receipt.Revision, receipt.RequestId, sent);
                EmpLog.Debug("Personal profile checkpoint accepted: actor {Uid}, revision {Revision}", actor.uid, receipt.Revision);
            }
        } catch (Exception ex) {
            EmpLog.Warning(ex, "Rejected personal profile checkpoint from peer {Peer}, actor {Uid}", peer.Id, actor.uid);
        }
    }
}
