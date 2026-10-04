using ElinTogether.Net;
using ElinTogether.Helper;
using ElinTogether.Patches;
using System;
using System.Linq;

namespace ElinTogether.Models;

internal sealed class ResidentManagement : EClass
{
    // True only for a synchronously accepted host action. Clients wait for state.
    internal static bool Submit(Chara c, ResidentOperation operation, bool? maid = null)
    {
        if (Branch is not { } branch) return false;
        var request = new ResidentCommandDelta {
            ZoneUid = _zone.uid, MemberUid = c.uid, Operation = operation,
            MemberType = c.memberType == FactionMemberType.Livestock ? FactionMemberType.Default : FactionMemberType.Livestock,
            Maid = maid ?? branch.uidMaid != c.uid,
        };
        switch (NetSession.Instance.Connection) {
            case ElinNetClient client: client.Delta.AddRemote(request); break;
            case ElinNetHost host: return Execute(host, request, pc);
        }
        return false;
    }

    internal static bool Execute(ElinNetHost host, ResidentCommandDelta request, Chara? localActor = null)
    {
        var actor = localActor;
        if (actor == null && (!host.AcceptsPlayerInput(request.OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(request.OriginPeer, out actor))) return false;
        void Reject(string reason) {
            EmpLog.Warning("Resident command rejected: actor {Actor}, member {Member}, operation {Operation}, reason {Reason}",
                actor!.uid, request.MemberUid, request.Operation, reason);
            if (localActor != null) Msg.Say(reason);
            else host.SendDeltaTo(request.OriginPeer, new MsgSayDelta { Text = reason, R = 1, G = 1, B = 1, A = 1 });
        }
        if (!Enum.IsDefined(typeof(ResidentOperation), request.Operation) || request.ZoneUid != _zone.uid ||
            !_zone.IsPCFaction || Branch == null || !actor!.IsInActiveMap || actor.IsDisabled) {
            Reject("The resident board is no longer available here."); return false;
        }
        var c = Branch.members.FirstOrDefault(c => c.uid == request.MemberUid);
        if (c == null || !c.IsAliveInCurrentZone || !c.IsHomeMember() || c.IsGuest() ||
            c.homeBranch != Branch || PartyMembership.Protected(c)) {
            Reject("That resident cannot be managed here."); return false;
        }
        var previous = pc;
        var typeChanged = false;
        using var simulation = ElinDelta.Simulate();
        using var messages = MsgRelayContext.RedirectTo(actor);
        try {
            player.chara = actor;
            if (request.Operation == ResidentOperation.MemberType) {
                if (c.IsPCParty || request.MemberType is not (FactionMemberType.Default or FactionMemberType.Livestock)) {
                    Reject("Dismiss this resident from the party before changing their role."); return false;
                }
                if (c.memberType != request.MemberType) {
                    if (request.MemberType == FactionMemberType.Livestock && !world.date.IsExpired(c.GetInt(36))) {
                        Reject("This resident cannot become livestock again until their cooldown expires."); return false;
                    }
                    if (c.memberType == FactionMemberType.Livestock) c.SetInt(36, world.date.GetRaw() + 14400);
                    Branch.ChangeMemberType(c, request.MemberType);
                    typeChanged = true; // Native completion hook already published this result.
                }
            } else if (request.Maid) Branch.uidMaid = c.uid;
            else if (Branch.uidMaid == c.uid) Branch.uidMaid = 0;
            EmpLog.Information("Resident command resolved: actor {Actor}, member {Member}, operation {Operation}, type {Type}, maid {Maid}",
                actor.uid, c.uid, request.Operation, c.memberType, Branch.uidMaid);
            return true;
        } finally {
            player.chara = previous;
            // Also repair the requesting client's stale display after a rejection.
            if (!typeChanged) host.Delta.AddRemote(ResidentStateDelta.Capture(Branch, c));
            ReserveManagement.RefreshList();
        }
    }
}
