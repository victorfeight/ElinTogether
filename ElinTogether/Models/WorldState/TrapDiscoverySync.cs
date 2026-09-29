using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

// Discovery is monotonic for existing traps. Reuse the native flag/save data,
// and reconcile through snapshots so lost reports and late joins repair themselves.
internal static class TrapDiscoverySync
{
    private static bool IsTrap(Thing thing) => !thing.isDestroyed &&
        thing.pos is { IsValid: true } && thing.trait is TraitTrap;

    internal static int[] Capture() => EClass._map.things
        .Where(t => IsTrap(t) && !t.isHidden).Select(t => t.uid).ToArray();

    internal static void Apply(int zoneUid, int[]? revealed)
    {
        if (revealed is null || EClass._zone.uid != zoneUid ||
            NetSession.Instance.Connection is not ElinNetClient client) return;
        var known = revealed.ToHashSet();
        var local = Capture().Where(uid => !known.Contains(uid)).ToArray();
        Reveal(revealed);
        // Local search is player-owned. Report only actual discoveries missing
        // from host state; subsequent snapshots acknowledge or retry the report.
        if (local.Length != 0) client.Delta.AddRemote(new TrapDiscoveryDelta {
            ZoneUid = zoneUid, Revealed = local,
        });
    }

    internal static void Reveal(int[] revealed)
    {
        var known = revealed.ToHashSet();
        // Never use RemoteCard.Find here: stale discoveries must not resurrect
        // a disarmed/destroyed trap or load a card from a different map.
        foreach (var trap in EClass._map.things) {
            if (!IsTrap(trap) || !trap.isHidden || !known.Contains(trap.uid)) continue;
            trap.SetHidden(false);
            EmpLog.Debug("Shared trap discovery: zone {ZoneUid}, trap {TrapUid}", EClass._zone.uid, trap.uid);
        }
    }
}

[MessagePackObject]
public class TrapDiscoveryDelta : ElinDelta
{
    [Key(0)] public int ZoneUid { get; init; }
    [Key(1)] public required int[] Revealed { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is not ElinNetHost host || Revealed is null ||
            ZoneUid != EClass._zone.uid ||
            !host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var actor) || !actor.IsInActiveMap) return;
        // Cooperative discovery report, not a host reroll of the client's search.
        TrapDiscoverySync.Reveal(Revealed);
    }
}
