using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;

namespace ElinTogether.Models;

internal sealed class PartyBoardManagement : EClass
{
    private static readonly HashSet<(int, Guid)> Completed = [];
    private static readonly Queue<(int, Guid)> CompletionOrder = [];
    internal static WeakReference<ListPeopleParty>? OpenList;

    [ElinPreLoad]
    private static void Reset(GameIOContext _) { Completed.Clear(); CompletionOrder.Clear(); OpenList = null; }

    internal static void Submit(PartyBoardOperation operation, Chara? target = null, Player.PartySetup? setup = null)
    {
        var request = new PartyBoardCommandDelta {
            RequestId = Guid.NewGuid(), ZoneUid = _zone.uid, Operation = operation,
            MemberUid = target?.uid ?? 0, Favorite = target != null && !target.isFav,
            Members = setup?.uids.ToArray() ?? [], RideUid = setup?.ride ?? 0, ParasiteUid = setup?.parasite ?? 0,
        };
        switch (NetSession.Instance.Connection) {
            case ElinNetClient client: client.Delta.AddRemote(request); break;
            case ElinNetHost host: Execute(host, request, pc); break;
        }
    }

    internal static bool CanJoin(Chara c, Chara actor, bool preset = false) => PartyMembership.ManagedBy(c, actor) && c.IsPCFaction &&
        (preset || c.memberType == FactionMemberType.Default) && !c.isDead && !c.HasCondition<ConSuspend>() &&
        c.currentZone != null && c.trait.CanJoinParty && c.trait.CanJoinPartyResident;

    internal static bool CanDismiss(Chara c, Chara actor) => PartyMembership.ManagedBy(c, actor) && c.party == actor.party && c.homeZone != null;

    internal static void Execute(ElinNetHost host, PartyBoardCommandDelta request, Chara? localActor = null)
    {
        var actor = localActor;
        if (actor == null && (!host.AcceptsPlayerInput(request.OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(request.OriginPeer, out actor))) return;
        var peer = localActor == null ? request.OriginPeer : 0;
        if (request.RequestId == Guid.Empty || !Completed.Add((peer, request.RequestId))) return;
        CompletionOrder.Enqueue((peer, request.RequestId));
        if (CompletionOrder.Count > 256) Completed.Remove(CompletionOrder.Dequeue());
        void Reject(string reason) {
            EmpLog.Warning("Party board rejected: peer {Peer}, operation {Operation}, reason {Reason}", peer, request.Operation, reason);
            if (peer == 0) Msg.Say(reason);
            else host.SendDeltaTo(peer, new MsgSayDelta { Text = reason, R = 1, G = 1, B = 1, A = 1 });
        }
        if (!Enum.IsDefined(typeof(PartyBoardOperation), request.Operation) || request.ZoneUid != _zone.uid ||
            !_zone.IsPCFaction || !actor!.IsInActiveMap || actor.IsDisabled || actor.party != pc.party ||
            request.Members == null || request.Members.Length > 256) {
            Reject("The party board is no longer available here.");
            return;
        }
        Chara? Find(int uid) => game.cards.globalCharas.GetValueOrDefault(uid);
        var before = pc.party.members.ToArray();
        var touched = new HashSet<Chara>(before);
        var previous = pc;
        using var simulation = ElinDelta.Simulate();
        using var messages = MsgRelayContext.RedirectTo(actor);
        try {
            player.chara = actor;
            switch (request.Operation) {
                case PartyBoardOperation.Join:
                    if (Find(request.MemberUid) is not { } recruit || !CanJoin(recruit, actor)) { Reject("That companion cannot join your party right now."); return; }
                    touched.Add(recruit);
                    PartyMembership.Join(recruit, actor, recall: true, showMsg: false);
                    break;
                case PartyBoardOperation.Dismiss:
                    if (Find(request.MemberUid) is not { } dismiss || !CanDismiss(dismiss, actor)) { Reject("That companion cannot be sent home right now."); return; }
                    touched.Add(dismiss);
                    PartyMembership.Dismiss(dismiss, actor, showMsg: false);
                    break;
                case PartyBoardOperation.Disband:
                case PartyBoardOperation.Load:
                    // Preflight the candidate list using the requesting character's
                    // native eligibility. Vanilla skips unavailable preset entries.
                    var candidates = request.Operation == PartyBoardOperation.Load
                        ? request.Members.Distinct().Select(Find).OfType<Chara>().Where(c => CanJoin(c, actor, preset: true)).ToArray() : [];
                    foreach (var c in before.Where(c => CanDismiss(c, actor))) PartyMembership.Dismiss(c, actor, showMsg: false);
                    foreach (var c in candidates) {
                        touched.Add(c);
                        PartyMembership.Join(c, actor, recall: true, showMsg: false);
                        if (c.host != null || c.IsMultisize) continue;
                        if (c.uid == request.RideUid) ActRide.Ride(actor, c, parasite: false, talk: false);
                        else if (c.uid == request.ParasiteUid) ActRide.Ride(actor, c, parasite: true, talk: false);
                    }
                    break;
                case PartyBoardOperation.Favorite:
                    if (Find(request.MemberUid) is not { } favorite || (!favorite.IsPCFaction && favorite.party != actor.party)) { Reject("That character is not on the party roster."); return; }
                    touched.Add(favorite);
                    favorite.isFav = request.Favorite;
                    break;
                case PartyBoardOperation.Refresh:
                    break;
            }
            EmpLog.Information("Party board resolved: peer {Peer}, actor {Actor}, operation {Operation}, members {Count}",
                peer, actor.uid, request.Operation, actor.party.members.Count);
        } finally {
            player.chara = previous;
            // Opening also includes candidates to repair old client-only joins
            // and favorite flags. Later actions send only party/affected NPCs.
            var result = PartyBoardStateDelta.Capture(touched, request.Operation == PartyBoardOperation.Refresh);
            if (request.Operation == PartyBoardOperation.Refresh && peer != 0) host.SendDeltaTo(peer, result);
            else host.Delta.AddRemoteImmediate(result);
            RefreshUI();
        }
    }

    internal static void RefreshUI()
    {
        if (OpenList?.TryGetTarget(out var owner) == true && owner.layer && owner.list) owner.RefreshAll(false);
    }
}
