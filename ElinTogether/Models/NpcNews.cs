using System;
using System.Collections.Generic;
using ElinTogether.Net;
using ElinTogether.Helper;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class NpcNewsRequest : ElinDelta
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public int ZoneUid { get; init; }
    [Key(2)] public required RemoteCard Npc { get; init; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host) NpcNews.Resolve(host, OriginPeer, this);
    }
}

[MessagePackObject]
public sealed class NpcNewsResult : ElinDelta
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public int ZoneUid { get; init; }
    [Key(2)] public required RemoteCard Npc { get; init; }
    [Key(3)] public bool Accepted { get; set; }
    [Key(4)] public int? LastNews { get; set; }
    [Key(5)] public SpatialGenDelta? Site { get; set; }
    [Key(6)] public string? SiteName { get; set; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetClient) NpcNews.Receive(net, this);
    }
}

internal sealed class NpcNews : EClass
{
    private sealed record Pending(ElinNetBase Net, Chara Actor, Action<NpcNewsResult> Reply);
    private static readonly Dictionary<Guid, Pending> PendingReplies = [];
    private static readonly HashSet<(int, Guid)> Resolved = [];
    private static readonly HashSet<Guid> Received = [];
    internal static void Reset() { PendingReplies.Clear(); Resolved.Clear(); Received.Clear(); }
    [ElinPreLoad] private static void OnLoad(GameIOContext _) => Reset();

    internal static void Submit(Chara npc, Action<NpcNewsResult> reply)
    {
        if (NetSession.Instance.Connection is not { } net || PlayerControl.LocalInputBlocked) return;
        var request = new NpcNewsRequest { Id = Guid.NewGuid(), ZoneUid = _zone.uid, Npc = npc };
        PendingReplies.Add(request.Id, new(net, pc, reply));
        if (net is ElinNetHost host) Resolve(host, 0, request);
        else net.Delta.AddRemote(request);
    }

    internal static void Resolve(ElinNetHost host, int peer, NpcNewsRequest request)
    {
        if (request.Id == Guid.Empty || !Resolved.Add((peer, request.Id))) return;
        var actor = peer == 0 ? pc : host.ActiveRemoteCharas.GetValueOrDefault(peer);
        var npc = request.Npc.Find() as Chara;
        var result = new NpcNewsResult { Id = request.Id, ZoneUid = request.ZoneUid, Npc = request.Npc };
        if (actor is not null && !actor.isDead && actor.IsInActiveMap && !host.IsCompanionControlled(actor) &&
            (peer == 0 || host.AcceptsPlayerInput(peer)) && request.ZoneUid == _zone.uid &&
            npc is not null && !npc.isDead && npc.IsInActiveMap && actor.Dist(npc) <= 3 &&
            npc.trait.HaveNews && npc.memberType != FactionMemberType.Livestock && !npc.IsGuest() &&
            (!npc.IsPCParty || _zone.IsPCFaction)) {
            result.LastNews = npc.GetInt(37);
            if (npc.GetInt(37) + 10080 < world.date.GetRaw()) {
                // Native _news uses the speaking PC's level/fame and position.
                // Scope only synchronous generation; never switch PC across UI/network waits.
                var previous = pc;
                Zone? site;
                using (ElinDelta.Simulate()) {
                    try {
                        player.chara = actor;
                        site = world.region.CreateRandomSite(_zone, 5);
                        if (site is not null) site.isKnown = true;
                        npc.SetInt(37, world.date.GetRaw()); // Native cooldown also applies when no site fits.
                    } finally { player.chara = previous; }
                }
                result.Accepted = true;
                result.LastNews = npc.GetInt(37);
                result.Site = site is null ? null : SpatialGenDelta.Create(site);
                result.SiteName = site?.Name;
            }
        }
        if (result.Accepted) {
            host.Delta.AddRemote(result);
            Receive(host, result);
        } else if (peer == 0) Receive(host, result);
        else host.SendDeltaTo(peer, result);
        EmpLog.Information("NPC news resolved: request {Request}, peer {Peer}, NPC {Npc}, accepted {Accepted}, site {Site}, sourceZone {Zone}",
            request.Id, peer, npc?.uid, result.Accepted, result.Site?.ZoneUid, request.ZoneUid);
    }

    internal static void Receive(ElinNetBase net, NpcNewsResult result)
    {
        if (!Received.Add(result.Id)) return;
        if (net is ElinNetClient) {
            result.Site?.Apply(net);
            if (result.LastNews is { } last && result.Npc.Find() is Chara npc) npc.SetInt(37, last);
        }
        if (result.SiteName is { } name) Msg.Say("discoverZone", name);
        if (PendingReplies.Remove(result.Id, out var pending) && pending.Net == net && pending.Actor == pc)
            pending.Reply(result);
    }
}
