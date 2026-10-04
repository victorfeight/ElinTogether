using System.Collections.Generic;
using System.Linq;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class ExplosionVisualPatch
{
    // Item destruction can arrive before the throw replay. Do not depend on the
    // consumed item for presentation, or rerun its explosion on a client.
    [HarmonyPrefix, HarmonyPatch(typeof(Card), nameof(Card.Explode))]
    internal static bool Explode(Card __instance) => NetSession.Instance.Connection is not ElinNetClient ||
        __instance.trait.ThrowType != ThrowType.Explosive;

    [HarmonyPrefix, HarmonyPatch(typeof(ActEffect), nameof(ActEffect.DamageEle))]
    internal static void Capture(Card CC, EffectId id, Element e, List<Point> points, ActRef actref, Point center)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host || id != EffectId.Explosive ||
            actref.refThing?.trait.ThrowType != ThrowType.Explosive || points.Count == 0) return;
        var delta = new ExplosionVisualDelta {
            ZoneUid = EClass._zone.uid,
            Origin = actref.refThing.pos,
            Center = center ?? CC.pos,
            Points = points.Select(p => (Position)p).ToArray(),
            ElementId = e.id,
        };
        if (CharaProgressCompleteEvent.ShouldPack(false)) CharaProgressCompleteEvent.Pack(delta);
        else host.Delta.AddRemote(delta);
        EmpLog.Debug("Explosion visual sent in zone {ZoneUid} at {@Pos}, tiles {Count}", delta.ZoneUid, delta.Origin, delta.Points.Length);
    }
}
