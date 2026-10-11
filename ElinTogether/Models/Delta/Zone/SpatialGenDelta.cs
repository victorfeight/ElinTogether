using System;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class SpatialGenDelta : ElinDelta
{
    [Key(0)]
    public required string ZoneFullName { get; init; }

    [Key(1)]
    public required int ZoneUid { get; init; }

    [Key(2)]
    public required Position Pos { get; init; }

    [Key(3)]
    public required int ParentZoneUid { get; init; }

    [Key(4)]
    public int Icon { get; init; }

    [Key(5)] public SiteState? State { get; init; }

    [MessagePackObject]
    public sealed class SiteState
    {
        [Key(0)] public string? Name { get; init; }
        [Key(1)] public int Prefix { get; init; }
        [Key(2)] public int Danger { get; init; }
        [Key(3)] public bool Known { get; init; }
        [Key(4)] public bool Random { get; init; }
        [Key(5)] public int Expires { get; init; }
        internal void Apply(Zone zone)
        {
            zone.name = Name;
            zone.idPrefix = Prefix;
            zone._dangerLv = Danger;
            zone.isKnown = Known;
            zone.isRandomSite = Random;
            zone.dateExpire = Expires;
        }
    }

    public static SpatialGenDelta Create(Zone zone)
    {
        return new() {
            ZoneFullName = zone.ZoneFullName,
            ZoneUid = zone.uid,
            Pos = new() { X = zone.x, Z = zone.y },
            ParentZoneUid = zone.parent?.uid ?? -1,
            Icon = zone.icon,
            State = new() { Name = zone.name, Prefix = zone.idPrefix, Danger = zone._dangerLv,
                Known = zone.isKnown, Random = zone.isRandomSite, Expires = zone.dateExpire },
        };
    }

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost) {
            // reject every single zone creation from clients
            return;
        }

        var remoteZone = game.spatials.Find(ZoneUid);
        if (remoteZone?.ZoneFullName == ZoneFullName && remoteZone.uid == ZoneUid) {
            // Creation and discovery may arrive separately; update the existing
            // object without generating another site or replacing UI references.
            ApplyState(remoteZone);
            if (remoteZone.parent is Region existingRegion)
                existingRegion.elomap.SetZone(remoteZone.x, remoteZone.y, remoteZone, true);
            return;
        }

        var (_, zoneId, zoneLv) = Zone.ParseZoneFullName(ZoneFullName);
        var parent = game.spatials.Find(ParentZoneUid);
        remoteZone = SpatialGen.Create(zoneId, parent, false, Pos.X, Pos.Z) as Zone;

        if (remoteZone is null) {
            return;
        }

        remoteZone.lv = zoneLv;
        remoteZone.uid = ZoneUid;
        ApplyState(remoteZone);

        if (game.spatials.Find(ZoneUid) is { } exist) {
            EmpLog.Warning("Zone uid {ZoneUid} taken by local zone {LocalZoneFullName}, " +
                           "replacing with host {ZoneFullName}",
                ZoneUid, exist.ZoneFullName, ZoneFullName);
        }

        game.spatials.map[ZoneUid] = remoteZone;
        game.spatials.uidNext = Math.Max(game.spatials.uidNext, ZoneUid + 1);

        // update on overworld
        if (parent is Region region) {
            region.elomap.SetZone(Pos.X, Pos.Z, remoteZone, true);
        }

        SpatialGenEvent.HeldRefZones[ZoneUid] = remoteZone;
    }

    private void ApplyState(Zone zone)
    {
        zone.icon = Icon;
        State?.Apply(zone);
    }
}
