using System;
using System.Collections.Generic;
using ElinTogether.Net;
using ElinTogether.Helper;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class TpSpellRequestDelta : ElinDelta
{
    private static readonly HashSet<(int, Guid)> Completed = [];
    [ElinPreLoad] private static void Reset(GameIOContext _) => Completed.Clear();
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public required RemoteCard Owner { get; init; }
    [Key(2)] public int ActId { get; init; }
    [Key(3)] public int ZoneUid { get; init; }
    [Key(4)] public required Position Pos { get; init; }
    [Key(5)] public RemoteCard? Target { get; init; }
    [Key(6)] public int PowerMod { get; set; } = 100;
    [Key(7)] public bool ForceTarget { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is not ElinNetHost host || Id == Guid.Empty || !host.AcceptsPlayerInput(OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var actor) || actor.uid != Owner.Uid ||
            !Completed.Add((OriginPeer, Id))) return;
        var element = actor.elements.GetElement(ActId);
        var act = element?.act;
        if (actor.isDead || actor.IsDisabled || !actor.IsInActiveMap || ZoneUid != _zone.uid ||
            !Pos.IsInActiveMapBounds || PowerMod < 0 || PowerMod > 1000 ||
            element is null || element.Value <= 0 || act is null || !TpSpellBridge.IsManaged(act)) {
            host.SendDeltaTo(OriginPeer, new MsgSayDelta {
                Text = "The Tp spell could not execute: caster, spell, or map state changed.", R = 1, G = 1, B = 1, A = 1,
            });
            EmpLog.Warning("TpSpell rejected: id={Id} act={Act} peer={Peer} zone={Zone}", Id, ActId, OriginPeer, ZoneUid);
            return;
        }
        var oldCC = Act.CC;
        var oldTC = Act.TC;
        var oldTP = Act.TP.Copy();
        var oldPower = Act.powerMod;
        var oldForce = Act.forcePt;
        var oldAct = Act.CurrentAct;
        var oldRequest = TpSpellBridge.RequestId;
        using var simulation = Simulate();
        try {
            TpSpellBridge.RequestId = Id;
            Act.powerMod = PowerMod;
            Act.forcePt = ForceTarget;
            // Match vanilla MP's cast boundary: costs/stock/XP were handled by
            // client UseAbility. Perform the effect once, never UseAbility again.
            var success = act.Perform(actor, Target?.Find(), Pos);
            EmpLog.Information("TpSpell executed: id={Id} act={Act} actor={Actor} zone={Zone} dispatched={Success}",
                Id, ActId, actor.uid, ZoneUid, success);
            if (ActId == TpSpellBridge.Return && success)
                EmpPop.Information($"{actor.NameSimple} cast Return. Shared travel is controlled on the host.");
        } finally {
            Act.CC = oldCC;
            Act.TC = oldTC;
            Act.TP.Set(oldTP);
            Act.powerMod = oldPower;
            Act.forcePt = oldForce;
            Act.CurrentAct = oldAct;
            TpSpellBridge.RequestId = oldRequest;
        }
    }
}

[MessagePackObject]
public sealed class TpSpellResultDelta : ElinDelta
{
    [Key(0)] public int ZoneUid { get; init; }
    [Key(1)] public int ActId { get; init; }
    [Key(2)] public required RemoteCard Owner { get; init; }
    [Key(3)] public List<Move> Moves { get; set; } = [];
    [Key(4)] public List<CharaStateSnapshot> Characters { get; set; } = [];
    [Key(5)] public WeatherStateSnapshot? Weather { get; set; }
    [Key(6)] public Guid Id { get; init; }
    [MessagePackObject]
    public sealed class Move
    {
        [Key(0)] public required RemoteCard Card { get; init; }
        [Key(1)] public required Position Pos { get; init; }
    }
    protected override void OnApply(ElinNetBase net)
    {
        if (net is not ElinNetClient || ZoneUid != _zone.uid) return;
        foreach (var move in Moves) {
            if (!move.Pos.IsInActiveMapBounds || move.Card.Find() is not Chara { isDead: false, IsInActiveMap: true } actor) continue;
            // Native teleport checks/RNG ran on host. Apply the resolved position,
            // including our own player (ordinary predicted moves skip that case).
            if (actor.Stub_Move(move.Pos, Card.MoveType.Force) != Card.MoveResult.Success) continue;
            actor.ai.Cancel();
            actor.renderer.SetFirst(true, actor.pos.PositionCenter());
            if (actor.IsPC) {
                screen.FocusPC(); screen.RefreshPosition(); player.haltMove = true;
            }
        }
        foreach (var snapshot in Characters) snapshot.ApplyReconciliation();
        Weather?.Apply();
        EmpLog.Information("TpSpell received: id={Id} act={Act} actor={Actor} zone={Zone} moves={Moves} minions={Minions}",
            Id, ActId, Owner.Uid, ZoneUid, Moves.Count, Characters.Count);
    }
}
