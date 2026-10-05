using System;
using System.Collections.Generic;
using ElinTogether.Helper;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class TerrainBrushCommand : ElinDelta
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public int ZoneUid { get; set; }
    [Key(2)] public required Position Center { get; set; }
    [Key(3)] public int Mode { get; set; }
    [Key(4)] public int Radius { get; set; }
    [Key(5)] public bool Shift { get; set; }
    protected override void OnApply(ElinNetBase net) { if (net is ElinNetHost host) TerrainBrush.Execute(host, this); }
}

internal sealed class TerrainBrush : EClass
{
    private static readonly HashSet<(int, Guid)> Completed = [];
    private static Map? _owner;
    [ElinPreLoad] private static void Reset(GameIOContext _) { Completed.Clear(); _owner = null; }

    internal static void Submit(AM_Terrain mode, Point point)
    {
        if (mode.timer < 0.1f || NetSession.Instance.Connection is not ElinNetClient client) return;
        mode.lastPoint ??= point.Copy();
        mode.timer = 0f; // Native continuous brush cadence, not network polling.
        client.Delta.AddRemote(new TerrainBrushCommand {
            Id = Guid.NewGuid(), ZoneUid = _zone.uid, Center = mode.lastPoint,
            Mode = (int)mode.mode, Radius = mode.brushRadius, Shift = EInput.isShiftDown,
        });
    }

    internal static void Execute(ElinNetHost host, TerrainBrushCommand command)
    {
        if (command.ZoneUid != _zone.uid || !host.AcceptsPlayerInput(command.OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(command.OriginPeer, out var actor) ||
            !actor.IsInActiveMap || actor.isDead || actor.IsDisabled) return;
        if (!_zone.IsPCFactionOrTent || command.Center is not { IsInActiveMapBounds: true } ||
            command.Radius < 1 || command.Radius > 32 || !Enum.IsDefined(typeof(AM_Terrain.Mode), command.Mode)) {
            host.SendDeltaTo(command.OriginPeer, new MsgSayDelta { Text = "Invalid terrain brush selection.", R = 1, G = 1, B = 1, A = 1 });
            return;
        }
        if (_owner != _map) { Completed.Clear(); _owner = _map; }
        if (command.Id == Guid.Empty || !Completed.Add((command.OriginPeer, command.Id))) return;
        var shift = EInput.isShiftDown;
        try {
            EInput.isShiftDown = command.Shift;
            // Native math includes water boundaries, slopes, height clamps,
            // bridge offsets and room invalidation. Do not reimplement it.
            new AM_Terrain { mode = (AM_Terrain.Mode)command.Mode, brushRadius = command.Radius, timer = 0.1f }
                .OnProcessTiles(command.Center, 0);
            EmpLog.Debug("Terrain brush resolved: actor {Actor}, mode {Mode}, radius {Radius}, shift {Shift}, center {@Center}",
                actor.uid, command.Mode, command.Radius, command.Shift, command.Center);
        } finally { EInput.isShiftDown = shift; }
    }

    internal static Action? Capture(AM_Terrain mode, Point point)
    {
        if (mode.timer < 0.1f || NetSession.Instance.Connection is not ElinNetHost host) return null;
        var map = _map;
        var center = mode.lastPoint ?? point;
        var before = new List<(Point Point, int Height, int Bridge)>();
        map.ForeachSphere(center.x, center.z, mode.brushRadius,
            p => before.Add((p.Copy(), p.cell.height, p.cell.bridgeHeight)));
        return () => {
            if (_map != map) return;
            foreach (var old in before) {
                var cell = old.Point.cell;
                if (cell.height != old.Height || cell.bridgeHeight != old.Bridge)
                    host.Delta.AddRemote(ConstructionTerrainDelta.Capture(old.Point, ConstructionTerrainKind.Elevation));
            }
        };
    }
}
