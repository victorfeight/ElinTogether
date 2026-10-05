using System;
using System.Collections.Generic;
using ElinTogether.Net;
using ElinTogether.Helper;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

public enum AreaOperation { Settings, Create, Expand, Shrink, Delete }

[MessagePackObject]
public sealed class AreaCommand : ElinDelta
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public int ZoneUid { get; set; }
    [Key(2)] public long Revision { get; set; }
    [Key(3)] public AreaOperation Operation { get; set; }
    [Key(4)] public int AreaUid { get; set; }
    [Key(5)] public string AreaType { get; set; } = "";
    [Key(6)] public Position? Start { get; set; }
    [Key(7)] public Position? End { get; set; }
    [Key(8)] public AreaSettings? Before { get; set; }
    [Key(9)] public AreaSettings? After { get; set; }
    protected override void OnApply(ElinNetBase net) { if (net is ElinNetHost host) AreaManagement.Execute(host, this); }
}

internal sealed class AreaManagement : EClass
{
    private static readonly HashSet<(int, Guid)> Completed = [];
    [ElinPreLoad] private static void Reset(GameIOContext _) => Completed.Clear();
    internal static void Submit(AreaCommand command)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client) return;
        if (!BuildingPlanning.HasState) { Msg.Say("Waiting for the host's area state."); return; }
        command.Id = Guid.NewGuid(); command.ZoneUid = _zone.uid; command.Revision = BuildingPlanning.ClientRevision;
        client.Delta.AddRemote(command);
    }
    internal static void Execute(ElinNetHost host, AreaCommand command)
    {
        if (command.ZoneUid != _zone.uid || !host.AcceptsPlayerInput(command.OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(command.OriginPeer, out var actor) ||
            !actor.IsInActiveMap || actor.isDead || actor.IsDisabled || command.Id == Guid.Empty ||
            !Completed.Add((command.OriginPeer, command.Id))) return;
        void Reject(string why) => host.SendDeltaTo(command.OriginPeer, new MsgSayDelta { Text = why, R = 1, G = 1, B = 1, A = 1 });
        var previous = pc;
        using var simulation = ElinDelta.Simulate();
        using var messages = MsgRelayContext.RedirectTo(actor);
        try {
            if (!_zone.IsPCFactionOrTent || !Enum.IsDefined(typeof(AreaOperation), command.Operation)) { Reject("Areas can only be edited in your settlement or tent."); return; }
            player.chara = actor;
            _map.rooms.mapIDs.TryGetValue(command.AreaUid, out var target);
            if (command.Operation == AreaOperation.Settings) {
                if (target == null || command.Before is not { Valid: true } before || command.After is not { Valid: true } after) { Reject("This area is no longer available."); return; }
                var fields = before.Difference(after);
                var current = AreaSettings.Capture(target);
                if ((current.Difference(before) & fields) != 0 && (current.Difference(after) & fields) != 0) { Reject("The area settings changed. Reopen the menu and try again."); return; }
                after.Write(target, fields);
                // Only these two native menu actions rebuild rooms. Rebuilding
                // on rename would regenerate the name of an unplated room.
                if ((fields & (2 | 4)) != 0) _map.rooms.RefreshAll();
            } else {
                if (command.Operation is AreaOperation.Delete or AreaOperation.Shrink && command.Revision != BuildingPlanning.Capture().Revision) { Reject("The building plan changed. Try the area edit again."); return; }
                var area = target as Area;
                if (command.Operation == AreaOperation.Delete) {
                    if (area == null) { Reject("This area no longer exists."); return; }
                    _map.rooms.RemoveArea(area);
                } else {
                    if (command.Start is not { IsInActiveMapBounds: true } start || command.End is not { IsInActiveMapBounds: true } end ||
                        (long)(Math.Abs(start.X - end.X) + 1) * (Math.Abs(start.Z - end.Z) + 1) > 4096) { Reject("The selected area is invalid or too large."); return; }
                    ActionMode mode;
                    if (command.Operation == AreaOperation.Create) {
                        if (!sources.areas.map.ContainsKey(command.AreaType)) { Reject("Unknown area type."); return; }
                        mode = new AM_CreateArea { area = Area.Create(command.AreaType) };
                    } else {
                        if (area == null) { Reject("This area no longer exists."); return; }
                        mode = new AM_ExpandArea { area = area, shrink = command.Operation == AreaOperation.Shrink };
                    }
                    for (var x = Math.Max(start.X, end.X); x >= Math.Min(start.X, end.X); x--)
                        for (var z = Math.Min(start.Z, end.Z); z <= Math.Max(start.Z, end.Z); z++) {
                            var point = new Point(x, z);
                            var hit = mode._HitTest(point, start);
                            if (hit is HitResult.Valid or HitResult.Warning) mode.OnProcessTiles(point, -1);
                        }
                }
            }
            EmpLog.Information("Area command applied: peer {Peer}, operation {Operation}, area {Area}", command.OriginPeer, command.Operation, command.AreaUid);
        } finally {
            player.chara = previous;
            BuildingPlanning.Dirty();
            host.Delta.AddRemote(BuildingPlanning.Capture());
        }
    }
}
