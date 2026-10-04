using System;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;

namespace ElinTogether.Models;

internal sealed class ReserveManagement : EClass
{
    internal static WeakReference<ListPeopleCallReserve>? OpenList;
    internal static WeakReference<BaseListPeople>? OpenResidents;

    internal static void Submit(Chara? chara, ReserveOperation operation)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client) return;
        client.Delta.AddRemote(new ReserveCommandDelta {
            ZoneUid = _zone.uid, MemberUid = chara?.uid ?? 0, Operation = operation,
        });
        EmpLog.Debug("Reserve command requested: actor {Actor}, member {Member}, operation {Operation}, zone {Zone}",
            pc.uid, chara?.uid, operation, _zone.uid);
    }

    internal static void Execute(ElinNetHost host, ReserveCommandDelta request)
    {
        if (!host.AcceptsPlayerInput(request.OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(request.OriginPeer, out var actor)) return;
        string? reason = null;
        if (!Enum.IsDefined(typeof(ReserveOperation), request.Operation) || request.ZoneUid != _zone.uid ||
            !_zone.IsPCFaction || Branch == null || !actor.IsInActiveMap || actor.IsDisabled) {
            reason = "The hearthstone's reserve list is no longer available here.";
        } else if (request.Operation == ReserveOperation.Refresh) {
            host.SendDeltaTo(request.OriginPeer, ReserveStateDelta.Capture());
            return;
        }
        // Reserve entries need not be global or present in CardCache.
        var target = request.Operation == ReserveOperation.Store
            ? Home.GetChildren().SelectMany(b => b.members).FirstOrDefault(c => c.uid == request.MemberUid)
            : Home.listReserve.FirstOrDefault(i => i.chara.uid == request.MemberUid)?.chara;
        if (reason == null && (target == null || target.IsPlayer || target.GetBool("remote_chara")))
            reason = "That character is no longer an available reserve.";
        if (reason == null && request.Operation == ReserveOperation.Release && !target!.trait.CanBeBanished)
            reason = "This character cannot be released.";
        if (reason == null && request.Operation == ReserveOperation.Store) {
            if (!target!.IsAliveInCurrentZone || target.IsGuest() || !target.IsHomeMember() ||
                Home.listReserve.Any(i => i.chara.uid == target.uid))
                reason = "That resident is no longer available to send to reserve.";
            else if (Home.listReserve.Count >= Home.GetMaxReserve())
                reason = "There is no room in the reserve.";
        }
        if (reason != null) {
            EmpLog.Warning("Reserve command rejected: peer {Peer}, member {Member}, operation {Operation}, reason {Reason}",
                request.OriginPeer, request.MemberUid, request.Operation, reason);
            host.SendDeltaTo(request.OriginPeer, new MsgSayDelta { Text = reason, R = 1, G = 1, B = 1, A = 1 });
            host.SendDeltaTo(request.OriginPeer, ReserveStateDelta.Capture());
            return;
        }

        var previous = pc;
        using var simulation = ElinDelta.Simulate();
        using var messages = MsgRelayContext.RedirectTo(actor);
        try {
            player.chara = actor;
            if (request.Operation == ReserveOperation.Recruit) Branch!.Recruit(target!);
            else if (request.Operation == ReserveOperation.Store) Home.AddReserve(target!);
            else {
                Home.RemoveReserve(target!);
                target!.OnBanish();
            }
            // Native reserve mutation hooks publish the final result at flush,
            // after Recruit/OnBanish has completed its zone and faction changes.
            EmpLog.Information("Reserve command resolved: actor {Actor}, member {Member}, operation {Operation}, zone {Zone}",
                actor.uid, target!.uid, request.Operation, _zone.uid);
        } finally {
            player.chara = previous;
        }
    }

    internal static void Changed(Chara chara)
    {
        if (NetSession.Instance.Connection is ElinNetHost host)
            host.Delta.AddRemote(new ReserveStateDelta { CaptureSource = chara });
    }

    internal static void RefreshList()
    {
        if (OpenList?.TryGetTarget(out var owner) == true && owner.layer && owner.list)
            owner.list.List();
        if (OpenResidents?.TryGetTarget(out var residents) == true && residents.layer && residents.list) {
            residents.list.List();
            foreach (var tab in residents.layer.multi.owners) tab.RefreshTab();
        }
    }
}
