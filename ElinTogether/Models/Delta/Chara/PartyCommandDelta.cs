using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

// Resident dialogue intent; never use MakeAlly here (it also changes residency).
[MessagePackObject]
public class PartyCommandDelta : ElinDelta
{
    [Key(0)] public required RemoteCard Member { get; init; }
    [Key(1)] public int ZoneUid { get; init; }
    [Key(2)] public bool Join { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is not ElinNetHost host || !host.AcceptsPlayerInput(OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var actor)) return;

        if (Member.Find() is not Chara target || ZoneUid != _zone.uid ||
            !actor.IsInActiveMap || !target.IsInActiveMap || actor.IsDisabled ||
            target.isDead || !PartyMembership.ManagedBy(target, actor) || actor == target || actor.Dist(target) > 3) {
            Reject(host, "The companion is no longer available here.");
            return;
        }

        var sharedParty = pc.party;
        if (actor.party != sharedParty) {
            Reject(host, "Your character is not in the shared party.");
            return;
        }

        var previous = pc;
        using var simulation = Simulate();
        using var messages = MsgRelayContext.RedirectTo(actor);
        try {
            // Trait.CanJoinPartyResident reads EClass.pc.CHA. Validate the
            // requesting player's stats, not the host's, then restore them.
            player.chara = actor;
            if (Join) {
                if (!target.IsHomeMember() || target.memberType == FactionMemberType.Livestock ||
                    !target.trait.CanJoinParty || !target.trait.CanJoinPartyResident) {
                    Reject(host, "This resident cannot join your party right now.");
                    return;
                }
                PartyMembership.Join(target, actor, recall: false, showMsg: true);
            } else {
                if (target.isSummon || target.host != null || target.homeZone == null ||
                    target.party == null && !target.IsHomeMember() ||
                    target.party != null && target.party != sharedParty) {
                    Reject(host, "This companion cannot be sent home right now.");
                    return;
                }
                // A client from an earlier build may have a local-only member.
                // A repeated dismissal also reconciles that stale membership.
                PartyMembership.Dismiss(target, actor, showMsg: true);
            }
            // Explicit result also covers an already-satisfied request.
            host.SendDeltaTo(OriginPeer, new PartyMemberDelta {
                Member = target, Joined = target.party == sharedParty,
                DestZoneUid = target.currentZone?.uid ?? 0, DestPos = target.pos,
            });
            EmpLog.Information("Party command resolved: peer {Peer}, actor {Actor}, member {Member}, join {Join}, home {Home}, zone {Zone}",
                OriginPeer, actor.uid, target.uid, Join, target.homeZone?.uid, target.currentZone?.uid);
        } finally {
            player.chara = previous;
        }
    }

    private void Reject(ElinNetHost host, string reason)
    {
        EmpLog.Warning("Party command rejected: peer {Peer}, member {Member}, join {Join}, reason {Reason}",
            OriginPeer, Member.Uid, Join, reason);
        host.SendDeltaTo(OriginPeer, new MsgSayDelta { Text = reason, R = 1, G = 1, B = 1, A = 1 });
    }
}
