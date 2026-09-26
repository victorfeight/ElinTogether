using System;
using System.Collections.Generic;
using ElinTogether.Net;
using ElinTogether.Helper;
using ElinTogether.Patches;
using HarmonyLib;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class AutoActStepDelta : ElinDelta
{
    public enum Step { Clean, Water, Refill, Disarm, Chat }
    [Key(0)] public Guid RequestId { get; init; }
    [Key(1)] public required RemoteCard Owner { get; init; }
    [Key(2)] public Step Kind { get; init; }
    [Key(3)] public required Position Pos { get; init; }
    [Key(4)] public int ZoneUid { get; init; }
    [Key(5)] public RemoteCard? Tool { get; init; }
    [Key(6)] public RemoteCard? Target { get; init; }
    [Key(7)] public bool Reply { get; set; }
    [Key(8)] public bool Success { get; set; }
    [Key(9)] public List<Tile> Tiles { get; set; } = [];
    [Key(10)] public int StaminaChange { get; set; }
    [Key(11)] public int Charges { get; set; }
    [Key(12)] public bool HasTargetState { get; set; }
    [Key(13)] public int TargetInterest { get; set; }
    [Key(14)] public int TargetAffinity { get; set; }
    [Key(15)] public int TrapFailures { get; set; }
    private static readonly HashSet<Guid> Completed = [];
    [ElinPreLoad] private static void Reset(GameIOContext _) => Completed.Clear();

    [MessagePackObject]
    public class Tile
    {
        [Key(0)] public required Position Pos { get; init; }
        [Key(1)] public bool Watered { get; init; }
        [Key(2)] public byte Decal { get; init; }
        [Key(3)] public int[]? Effect { get; init; }
        [Key(4)] public string[]? Strings { get; init; }
        internal static Tile Capture(Point p) => new() {
            Pos = p, Watered = p.cell.isWatered, Decal = p.cell.decal,
            Effect = p.cell.effect is { } effect ? (int[])effect.ints.Clone() : null,
            Strings = p.cell.effect is { } e ? (string[])e.strs.Clone() : null,
        };
        internal void Apply()
        {
            if (!Pos.IsInActiveMapBounds) return;
            Point p = Pos;
            p.cell.isWatered = Watered;
            _map.SetDecal(p.x, p.z, Decal);
            _map.SetLiquid(p.x, p.z, Effect is null ? null : new CellEffect { ints = Effect, strs = Strings! });
        }
    }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is not ElinNetHost host) {
            if (!Reply) return;
            if (ZoneUid == _zone.uid) {
                foreach (var tile in Tiles) tile.Apply();
                if (HasTargetState && Target?.Find() is Chara npc) {
                    npc.interest = TargetInterest;
                    npc._affinity = TargetAffinity;
                }
                if (Success && Kind == Step.Disarm && Target?.Find() is { } trapCard) trapCard.SetInt(60, TrapFailures);
                if (Success && Tool?.Find() is { } tool) tool.c_charges = Charges;
                if (Success && Owner.Find() is Chara { IsPC: true } actor) actor.stamina.Mod(StaminaChange);
            }
            AutoActCustomActions.Complete(RequestId, Success && ZoneUid == _zone.uid);
            return;
        }
        if (Reply || RequestId == Guid.Empty || !Completed.Add(RequestId)) return;
        // Reply data is host-owned; never relay caller-supplied state, even on rejection.
        Success = false;
        Tiles = [];
        StaminaChange = 0;
        Charges = 0;
        HasTargetState = false;
        TargetInterest = TargetAffinity = TrapFailures = 0;
        Reply = true;
        if (!host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var owner) || owner.uid != Owner.Uid ||
            owner.isDead || !owner.IsInActiveMap || ZoneUid != _zone.uid || !Pos.IsInActiveMapBounds ||
            owner.Dist((Point)Pos) > 1) { host.Delta.AddRemote(this); return; }
        var toolCard = Tool?.Find();
        if (Kind is not (Step.Disarm or Step.Chat) && (toolCard is null || toolCard.isDestroyed || toolCard != owner.held || toolCard.GetRootCard() != owner)) {
            host.Delta.AddRemote(this); return;
        }
        var stamina = owner.stamina.value;
        try {
            using var simulation = Simulate();
            Success = Execute(owner, toolCard);
            Charges = toolCard?.c_charges ?? 0;
            StaminaChange = owner.stamina.value - stamina;
        } catch (Exception ex) {
            EmpLog.Warning("Auto Act {Step} failed for {Uid}: {Error}", Kind, owner.uid, ex.Message);
        }
        host.Delta.AddRemote(this);
    }

    private bool Execute(Chara owner, Card? tool)
    {
        Point pos = Pos;
        switch (Kind) {
            case Step.Refill:
                if (tool?.trait is not TraitToolWaterCan can || !ActDrawWater.HasWaterSource(pos)) return false;
                tool.SetCharge(can.MaxCharge);
                owner.PlaySound("water_draw");
                owner.Say("water_draw", owner, tool);
                return true;
            case Step.Clean:
                if (tool?.trait is not TraitBroom || !TaskClean.CanClean(pos)) return false;
                RunCustom("AutoActMod.Actions.AutoActClean+SubActClean", owner, pos, "pos");
                Tiles.Add(Tile.Capture(pos));
                return true;
            case Step.Water:
                if (tool?.trait is not TraitToolWaterCan || tool.c_charges <= 0 || !TaskWater.ShouldWater(pos)) return false;
                var range = tool.Evalue(770);
                range = range <= 0 ? 1 : Math.Min(tool.c_charges, 2 + range / 10);
                RunCustom("AutoActMod.Actions.AutoActWater+SubActWater", owner, pos, "dest");
                foreach (var p in _map.ListPointsInSquare(pos, range - 1, false, false)) Tiles.Add(Tile.Capture(p));
                return true;
            case Step.Chat:
                if (Target?.Find() is not Chara npc || npc.isDead || npc.IsHostile(owner) ||
                    npc.pos.Distance(pos) > 0 || !(npc.IsHumanSpeak || owner.HasElement(1640))) return false;
                var previous = game.player.chara;
                var previousAffinity = Affinity.CC;
                try {
                    game.player.chara = owner;
                    if (npc.interest > 0) npc.affinity.OnTalkRumor();
                } finally { game.player.chara = previous; Affinity.CC = previousAffinity; }
                HasTargetState = true;
                TargetInterest = npc.interest;
                TargetAffinity = npc._affinity;
                return true;
            case Step.Disarm:
                if (Target?.Find() is not { isDestroyed: false, trait: TraitTrap trap } card ||
                    card.pos.Distance(pos) > 0 || !trap.CanDisarmTrap) return false;
                // Vanilla disarm uses pc for feat checks and recovered scrap ownership.
                var saved = game.player.chara;
                try {
                    game.player.chara = owner;
                    if (!trap.TryDisarmTrap(owner) && owner.Evalue(1656) < 3 && rnd(2) == 0) trap.ActivateTrap(owner);
                } finally { game.player.chara = saved; }
                TrapFailures = card.GetInt(60);
                return true;
            default: return false;
        }
    }

    private static void RunCustom(string name, Chara owner, Point pos, string field)
    {
        var type = AccessTools.TypeByName(name) ?? throw new InvalidOperationException("Auto Act custom task missing");
        var task = (AIAct)Activator.CreateInstance(type)!;
        task.SetOwner(owner);
        AccessTools.Field(type, field).SetValue(task, pos);
        if (field == "dest") {
            var parentType = AccessTools.TypeByName("AutoActMod.Actions.AutoActWater");
            var parent = (AIAct)Activator.CreateInstance(parentType, pos)!;
            parent.SetOwner(owner);
            AccessTools.Field(parentType, "waterCan").SetValue(parent, owner.held.trait);
            task.parent = parent;
        }
        // Navigation/delay already happened on the owning client; execute exactly one
        // bounded custom step using the installed mod's implementation, not a copy.
        using var sequence = task.Run().GetEnumerator();
        var budget = 32;
        while (sequence.MoveNext()) {
            if (sequence.Current == AIAct.Status.Fail || --budget == 0) throw new InvalidOperationException("Custom step failed");
        }
    }
}
