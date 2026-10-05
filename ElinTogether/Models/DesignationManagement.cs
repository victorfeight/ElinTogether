using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

public enum DesignationOperation { Mine, Dig, Cut, Harvest, Cancel, Undo }

[MessagePackObject]
public sealed class DesignationCommand : ElinDelta
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public int ZoneUid { get; set; }
    [Key(2)] public long Revision { get; set; }
    [Key(3)] public DesignationOperation Operation { get; set; }
    [Key(4)] public Position? Start { get; set; }
    [Key(5)] public Position? End { get; set; }
    [Key(6)] public int Mode { get; set; }
    [Key(7)] public int Ramp { get; set; }
    [Key(8)] public bool Instant { get; set; }
    [Key(9)] public bool Roof { get; set; }
    protected override void OnApply(ElinNetBase net) { if (net is ElinNetHost host) DesignationManagement.Execute(host, this); }
}

internal sealed class DesignationManagement : EClass
{
    private static readonly HashSet<(int, Guid)> Completed = [];
    private static readonly Dictionary<int, List<TaskDesignation[]>> Undo = [];
    private static Map? _owner;
    internal static void Reset() { Completed.Clear(); Undo.Clear(); _owner = null; }
    internal static void Remember(int peer, TaskDesignation[] tasks)
    {
        if (_owner != _map) { Reset(); _owner = _map; }
        if (tasks.Length == 0) return;
        if (!Undo.TryGetValue(peer, out var batches)) Undo[peer] = batches = [];
        batches.Add(tasks); if (batches.Count > 10) batches.RemoveAt(0);
    }
    internal static void Submit(DesignationCommand command)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client) return;
        if (!BuildingPlanning.HasState) { Msg.Say("Waiting for the host's building plan."); return; }
        command.Id = Guid.NewGuid(); command.ZoneUid = _zone.uid; command.Revision = BuildingPlanning.ClientRevision;
        client.Delta.AddRemote(command);
    }
    internal static bool TrySubmit(ActionMode mode, Point start, Point end)
    {
        DesignationCommand? command = mode switch {
            AM_Mine mine => new() { Operation = DesignationOperation.Mine, Mode = (int)mine.mode, Ramp = mine.ramp },
            AM_Dig dig => new() { Operation = DesignationOperation.Dig, Mode = (int)dig.mode, Ramp = dig.ramp },
            AM_Cut => new() { Operation = DesignationOperation.Cut },
            AM_Harvest => new() { Operation = DesignationOperation.Harvest },
            AM_RemoveDesignation => new() { Operation = DesignationOperation.Cancel },
            _ => null,
        };
        if (command == null) return false;
        command.Start = start; command.End = end; command.Instant = player.instaComplete;
        command.Roof = mode.IsRoofEditMode(); Submit(command); return true;
    }
    internal static void Execute(ElinNetHost host, DesignationCommand command)
    {
        if (!host.AcceptsPlayerInput(command.OriginPeer) || command.ZoneUid != _zone.uid ||
            !host.ActiveRemoteCharas.TryGetValue(command.OriginPeer, out var actor) || !actor.IsInActiveMap || actor.isDead || actor.IsDisabled) return;
        if (_owner != _map) { Reset(); _owner = _map; }
        if (command.Id == Guid.Empty || !Completed.Add((command.OriginPeer, command.Id))) return;
        void Reject(string why) => host.SendDeltaTo(command.OriginPeer, new MsgSayDelta { Text = why, R = 1, G = 1, B = 1, A = 1 });
        var previous = pc;
        using var simulation = ElinDelta.Simulate();
        using var messages = MsgRelayContext.RedirectTo(actor);
        try {
            if (!_zone.IsPCFactionOrTent || !Enum.IsDefined(typeof(DesignationOperation), command.Operation)) { Reject("Work designations require your settlement or tent."); return; }
            player.chara = actor;
            var designations = _map.tasks.designations;
            void Cancel(TaskDesignation task) {
                if (!task.isDestroyed && designations.mapAll.TryGetValue(task.pos.index, out var current) && current == task)
                    designations.TryRemoveDesignation(task.pos);
            }
            if (command.Operation == DesignationOperation.Undo) {
                if (Undo.TryGetValue(command.OriginPeer, out var batches)) {
                    while (batches.Count > 0) {
                        var batch = batches[batches.Count - 1]; batches.RemoveAt(batches.Count - 1);
                        if (!batch.Any(t => !t.isDestroyed)) continue;
                        foreach (var t in batch) Cancel(t);
                        return;
                    }
                }
                Reject("You have no pending work batch to undo here."); return;
            }
            if (command.Operation == DesignationOperation.Cancel && command.Revision != BuildingPlanning.Capture().Revision) { Reject("The building plan changed. Select the tiles again."); return; }
            if (command.Start is not { IsInActiveMapBounds: true } start || command.End is not { IsInActiveMapBounds: true } end ||
                (long)(Math.Abs(start.X - end.X) + 1) * (Math.Abs(start.Z - end.Z) + 1) > 4096) { Reject("Invalid work selection."); return; }
            if (command.Operation == DesignationOperation.Mine && (!Enum.IsDefined(typeof(TaskMine.Mode), command.Mode) || command.Ramp < 3 || command.Ramp > 5) ||
                command.Operation == DesignationOperation.Dig && (!Enum.IsDefined(typeof(TaskDig.Mode), command.Mode) || command.Ramp < 3 || command.Ramp > 5)) { Reject("Invalid terrain mode."); return; }
            if (command.Operation != DesignationOperation.Cancel) {
                TerrainManagement.Execute(actor, command, Reject);
                return;
            }
            var cancel = new HashSet<TaskDesignation>();
            for (var x = Math.Max(start.X, end.X); x >= Math.Min(start.X, end.X); x--)
                for (var z = Math.Min(start.Z, end.Z); z <= Math.Max(start.Z, end.Z); z++) {
                    var p = new Point(x, z);
                    if (!p.cell.isSeen) continue;
                    if (designations.mapAll.TryGetValue(p.index, out var existing)) cancel.Add(existing);
                }
            foreach (var task in cancel) Cancel(task);
            EmpLog.Information("Designation cancellation: peer {Peer}, cancelled {Cancelled}", command.OriginPeer, cancel.Count);
        } finally {
            player.chara = previous; BuildingPlanning.Dirty(); host.Delta.AddRemote(BuildingPlanning.Capture());
        }
    }
}
