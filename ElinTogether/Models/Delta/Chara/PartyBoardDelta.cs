using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

public enum PartyBoardOperation { Refresh, Join, Dismiss, Disband, Load, Favorite }

[MessagePackObject]
public class PartyBoardCommandDelta : ElinDelta
{
    [Key(0)] public Guid RequestId { get; init; }
    [Key(1)] public int ZoneUid { get; init; }
    [Key(2)] public PartyBoardOperation Operation { get; init; }
    [Key(3)] public int MemberUid { get; init; }
    [Key(4)] public int[] Members { get; set; } = [];
    [Key(5)] public int RideUid { get; init; }
    [Key(6)] public int ParasiteUid { get; init; }
    [Key(7)] public bool Favorite { get; init; }
    protected override void OnApply(ElinNetBase net) { if (net is ElinNetHost host) PartyBoardManagement.Execute(host, this); }
}

[MessagePackObject]
public class PartyBoardMember
{
    [Key(0)] public required RemoteCard Card { get; init; }
    [Key(1)] public bool Joined { get; init; }
    [Key(2)] public int ZoneUid { get; init; }
    [Key(3)] public required Position Pos { get; init; }
    [Key(4)] public bool Favorite { get; init; }
    [Key(5)] public int HostUid { get; init; }
    [Key(6)] public int RideUid { get; init; }
    [Key(7)] public int ParasiteUid { get; init; }
}

[MessagePackObject]
public class PartyBoardStateDelta : ElinDelta
{
    [Key(0)] public int ZoneUid { get; init; }
    [Key(1)] public PartyBoardMember[] Members { get; set; } = [];
    [Key(2)] public int[] PartyOrder { get; set; } = [];

    internal static PartyBoardStateDelta Capture(IEnumerable<Chara> touched, bool includeCandidates = false)
    {
        var chars = new HashSet<Chara>(touched.Concat(pc.party.members));
        if (includeCandidates) chars.UnionWith(game.cards.globalCharas.Values.Where(c => c.IsPCFaction));
        // Include mount counterparts even when they aren't board candidates.
        var pending = new Queue<Chara>(chars);
        while (pending.Count > 0) {
            var c = pending.Dequeue();
            foreach (var linked in new[] { c.host, c.ride, c.parasite })
                if (linked != null && chars.Add(linked)) pending.Enqueue(linked);
        }
        return new() {
            ZoneUid = _zone.uid,
            PartyOrder = pc.party.members.Select(c => c.uid).ToArray(),
            Members = chars.Select(c => new PartyBoardMember {
                Card = RemoteCard.Create(c, addToCache: true, withData: true), Joined = c.party == pc.party && pc.party.members.Contains(c),
                ZoneUid = c.currentZone?.uid ?? 0, Pos = c.pos, Favorite = c.isFav,
                HostUid = c.host?.uid ?? 0, RideUid = c.ride?.uid ?? 0, ParasiteUid = c.parasite?.uid ?? 0,
            }).ToArray(),
        };
    }

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || _zone.uid != ZoneUid) return;
        var resolved = Members.ToDictionary(m => m.Card.Uid, m => m.Card.Find() as Chara);
        if (resolved.Values.Any(c => c == null) || PartyOrder.Any(uid => !resolved.ContainsKey(uid)) || Members.Any(m =>
            new[] { m.HostUid, m.RideUid, m.ParasiteUid }.Any(uid => uid != 0 && !resolved.ContainsKey(uid))) ||
            Members.Any(m => m.ZoneUid != 0 && game.spatials.Find(m.ZoneUid) == null)) {
            net.Delta.DeferLocal(this);
            return;
        }
        // Mirror resolved links, not Ride/Unride: those replay messages, random
        // dismount positions, membership changes and sometimes MakeAlly.
        Chara? Find(int uid) => uid == 0 ? null : resolved[uid];
        var changed = new List<Chara>();
        var rehosted = new HashSet<Chara>();
        foreach (var m in Members) {
            var c = resolved[m.Card.Uid]!;
            var host = Find(m.HostUid); var ride = Find(m.RideUid); var parasite = Find(m.ParasiteUid);
            if (c.host == host && c.ride == ride && c.parasite == parasite) continue;
            if (c.host != host) rehosted.Add(c);
            c.host = host; c.ride = ride; c.parasite = parasite;
            changed.Add(c);
        }
        // Set links before RemoveMember so its native Unride branch cannot
        // replay an already-resolved dismount on the client.
        var joined = Members.Where(m => m.Joined).Select(m => m.Card.Uid).ToHashSet();
        foreach (var c in pc.party.members.ToArray())
            if (!PartyMembership.Protected(c) && !joined.Contains(c.uid)) pc.party.Stub_RemoveMember(c);
        foreach (var m in Members) {
            var c = resolved[m.Card.Uid]!;
            // A newly transmitted off-map global NPC must also appear in the
            // vanilla candidate list, which enumerates globalCharas, not CardCache.
            if (c.IsGlobal && !game.cards.globalCharas.ContainsKey(c.uid)) game.cards.globalCharas[c.uid] = c;
            c.isFav = m.Favorite;
            if (!PartyMembership.Protected(c)) {
                new PartyMemberDelta { Member = m.Card, Joined = m.Joined, DestZoneUid = m.ZoneUid, DestPos = m.Pos }.Apply(net);
                if (m.ZoneUid == 0 && c.parent is Zone old) old.RemoveCard(c);
                else if (m.ZoneUid == _zone.uid && m.Pos.IsInActiveMapBounds && c.pos != m.Pos) c.MoveImmediate(m.Pos);
            }
        }
        foreach (var c in changed) {
            if (c.IsInActiveMap) { if (rehosted.Contains(c)) c._CreateRenderer(); c.SyncRide(); }
            c.SetDirtySpeed(); c.Refresh();
        }
        var party = pc.party;
        var ordered = PartyOrder.Select(uid => resolved[uid]!).Concat(party.members)
            .Where(c => c.party == party).Distinct().ToArray();
        party.members.Clear(); party.members.AddRange(ordered);
        party.uidMembers.Clear(); party.uidMembers.AddRange(ordered.Select(c => c.uid));
        WidgetRoster.SetDirty();
        PartyBoardManagement.RefreshUI();
        EmpLog.Debug("Party board state applied: zone {Zone}, members {Count}, mount changes {Mounts}", ZoneUid, joined.Count, changed.Count);
    }
}
