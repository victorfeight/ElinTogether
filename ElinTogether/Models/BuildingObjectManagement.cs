using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class BuildingObjectCommand : ElinDelta
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public int ZoneUid { get; set; }
    [Key(2)] public bool Move { get; set; }
    [Key(3)] public int[] Items { get; set; } = [];
    [Key(4)] public Position[] Before { get; set; } = [];
    [Key(5)] public Position End { get; set; } = new() { X = -1, Z = -1 };
    [Key(6)] public int Direction { get; set; }
    [Key(7)] public int Altitude { get; set; }
    [Key(8)] public bool Roof { get; set; }
    [Key(9)] public bool Instant { get; set; }
    [Key(10)] public bool IgnoreStack { get; set; }
    [Key(11)] public bool Free { get; set; }
    [Key(12)] public float X { get; set; }
    [Key(13)] public float Y { get; set; }
    protected override void OnApply(ElinNetBase net) { if (net is ElinNetHost host) BuildingObjectManagement.Execute(host, this); }
}

internal sealed class BuildingObjectManagement : EClass
{
    private static readonly HashSet<(int, Guid)> Completed = [];
    internal static void ResetNetwork() => Completed.Clear();
    [ElinPreLoad] private static void Reset(GameIOContext _) => ResetNetwork();
    internal static bool SubmitPutAway(Card? card)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client || card is not Thing item || !item.trait.CanPutAway) return false;
        client.Delta.AddRemote(new BuildingObjectCommand { Id = Guid.NewGuid(), ZoneUid = _zone.uid,
            Items = [item.uid], Before = [(Position)item.pos], End = item.pos });
        return true;
    }
    internal sealed class AM_RemoteMove : AM_MoveInstalled
    {
        internal bool Roof;
        public override string id => "MoveInstalled";
        public override bool IsRoofEditMode(Card? c = null) => Roof && (c == null || !c.trait.CanOnlyCarry);
    }
    internal static bool TrySubmit(ActionMode mode, Point start, Point end)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client) return false;
        var request = new BuildingObjectCommand { Id = Guid.NewGuid(), ZoneUid = _zone.uid, End = end, Instant = player.instaComplete };
        if (mode is AM_MoveInstalled move) {
            if (move.target is not Thing item || move.moldCard == null) return false;
            request.Move = true; request.Items = [item.uid]; request.Before = [(Position)item.pos];
            request.Direction = move.moldCard.dir; request.Altitude = move.moldCard.altitude; request.Roof = move.IsRoofEditMode();
            request.IgnoreStack = move.moldCard.ignoreStackHeight; request.Free = move.moldCard.freePos;
            request.X = move.moldCard.fx; request.Y = move.moldCard.fy;
            move.target = null; // The host result moves the original card.
        } else if (mode is AM_Deconstruct dismantle) {
            var items = new Dictionary<int, Thing>();
            for (var x = Math.Max(start.x, end.x); x >= Math.Min(start.x, end.x); x--)
                for (var z = Math.Min(start.z, end.z); z <= Math.Max(start.z, end.z); z++) {
                    var p = new Point(x, z);
                    if (!p.IsValid || !p.cell.isSeen) continue;
                    foreach (var item in p.ListCards().OfType<Thing>())
                        if (item.trait.CanPutAway && !(dismantle.ignoreInstalled && item.IsInstalled)) items[item.uid] = item;
                }
            request.Items = items.Keys.ToArray(); request.Before = items.Values.Select(t => (Position)t.pos).ToArray();
        } else return false;
        client.Delta.AddRemote(request); return true;
    }
    internal static void Execute(ElinNetHost host, BuildingObjectCommand request)
    {
        if (request.ZoneUid != _zone.uid || !host.AcceptsPlayerInput(request.OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(request.OriginPeer, out var actor) || !actor.IsInActiveMap || actor.isDead || actor.IsDisabled ||
            request.Id == Guid.Empty || !Completed.Add((request.OriginPeer, request.Id))) return;
        void Reject(string why) => host.SendDeltaTo(request.OriginPeer, new MsgSayDelta { Text = why, R = 1, G = 1, B = 1, A = 1 });
        if (!_zone.IsPCFactionOrTent || !request.End.IsInActiveMapBounds || request.Items.Length == 0 || request.Items.Length > 4096 ||
            request.Before.Length != request.Items.Length || request.Items.Distinct().Count() != request.Items.Length ||
            request.Move && request.Items.Length != 1 || !float.IsFinite(request.X) || !float.IsFinite(request.Y) ||
            request.Direction < 0 || request.Direction > 3 || request.Altitude < 0 || request.Altitude > 63) { Reject("Invalid building item selection."); return; }
        var items = new List<Thing>();
        for (var i = 0; i < request.Items.Length; i++) {
            if (CardCache.Find(request.Items[i]) is not Thing { isDestroyed: false } item || item.parent != _zone ||
                !request.Before[i].IsInActiveMapBounds || item.pos != (Point)request.Before[i] ||
                (!request.Move && !item.trait.CanPutAway) || item.trait.CanOnlyCarry) { Reject("A selected item moved or is no longer available."); return; }
            items.Add(item);
        }
        using var simulation = ElinDelta.Simulate();
        using var messages = MsgRelayContext.RedirectTo(actor);
        using var context = new ConstructionContext(actor, new() { Instant = request.Instant, Roof = request.Roof, IgnoreStackHeight = request.IgnoreStack });
        if (!request.Move) {
            // Same native operation as AM_Deconstruct.Perform, but fixed UIDs
            // prevent a newly dropped adjacent item entering a delayed request.
            foreach (var item in items) {
                if (item.parent != _zone) continue;
                item.PlaySound(item.material.GetSoundDead(item.sourceCard));
                _map.PutAway(item);
                host.Delta.AddRemote(CardConstructionState.Capture(item));
            }
            BuildMenu.dirtyCat = true; return;
        }
        var target = items[0];
        if (!_map.bounds.Contains(request.End)) { Reject("The destination is outside the building area."); return; }
        var preview = target.Duplicate(1);
        try {
            preview.dir = request.Direction; preview.altitude = request.Altitude;
            preview.ignoreStackHeight = request.IgnoreStack; preview.freePos = request.Free; preview.fx = request.X; preview.fy = request.Y;
            var mode = new AM_RemoteMove { Roof = request.Roof, target = target, moldCard = preview, list = _map.tasks.designations.moveInstalled };
            scene.actionMode = mode; mode.CreateNewMold();
            mode.mold.dir = request.Direction; mode.mold.altitude = request.Altitude; mode.mold.pos.Set(request.End);
            // SetTarget does this in vanilla before validating a new destination.
            foreach (var old in mode.list.items.Where(t => t.target == target).ToArray()) old.Destroy();
            BuildingPlanning.Dirty();
            if (mode.mold.GetHitResult() is not (HitResult.Valid or HitResult.Warning)) { Reject("The item cannot be moved there."); return; }
            mode.OnBeforeProcessTiles(); mode.OnProcessTiles(request.End, -1);
            DesignationManagement.Remember(request.OriginPeer, _map.tasks.undo.items.SelectMany(i => i.list).OfType<TaskDesignation>().Where(t => !t.isDestroyed).ToArray());
            host.Delta.AddRemote(CardConstructionState.Capture(target));
            BuildingPlanning.Dirty();
        } finally { preview.Destroy(); }
    }
}
