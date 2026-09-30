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
        if (ActiveRemoteCharas.Count == 0) return true;
        var savedGame = game;
        var peers = Socket.Peers.Where(p => ActiveRemoteCharas.ContainsKey(p.Id)).ToArray();
        var barrier = new PlayerProfileSaveBarrier();
        _profileSave = barrier;
        try {
            if (peers.Length != ActiveRemoteCharas.Count) return SaveFailed();
            foreach (var peer in peers) {
                if (!peer.IsConnected || !_profileChannels.TryGetValue(peer.Id, out var channel)) return SaveFailed();
                var request = barrier.Add(peer.Id, channel.OwnerUid, channel.Session);
                if (!peer.Send(request)) return SaveFailed();
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
        if (!ActiveRemoteCharas.TryGetValue(peer.Id, out var actor) || actor.IsPC ||
            !_profileChannels.TryGetValue(peer.Id, out var channel)) return;
        try {
            if (channel.Accept(actor, checkpoint) is { } receipt) {
                _profileSave?.Receive(peer.Id, receipt);
                peer.Send(receipt);
                EmpLog.Debug("Personal profile checkpoint accepted: actor {Uid}, revision {Revision}", actor.uid, receipt.Revision);
            }
        } catch (Exception ex) {
            EmpLog.Warning(ex, "Rejected personal profile checkpoint from peer {Peer}, actor {Uid}", peer.Id, actor.uid);
        }
    }
}
