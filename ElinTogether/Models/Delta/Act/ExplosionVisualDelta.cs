using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

// Presentation only: no card lookup, combat effect invocation, mining or drops.
[MessagePackObject]
public class ExplosionVisualDelta : ElinDelta
{
    [Key(0)] public required int ZoneUid { get; init; }
    [Key(1)] public required Position Origin { get; init; }
    [Key(2)] public required Position Center { get; init; }
    [Key(3)] public required Position[] Points { get; init; }
    [Key(4)] public required int ElementId { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || EClass._zone?.uid != ZoneUid || !Origin.IsInActiveMapBounds) return;
        Point origin = Origin;
        Point center = Center;
        var element = Element.Create(ElementId, 1);
        if (!EClass.setting.elements.ContainsKey(element.source.alias)) element = Element.Create(0, 1);
        var elementRef = EClass.setting.elements[element.source.alias];
        // Same particles, trail colour and radial delay as vanilla DamageEle.
        foreach (var pos in Points) {
            if (!pos.IsInActiveMapBounds) continue;
            Point point = pos;
            var effect = Effect.Get("Element/ball_" + (element.id == 0 ? "Void" : element.source.alias.Remove(0, 3)))
                         ?? Effect.Get("Element/ball_Fire");
            var delay = 0.04f * center.Distance(point);
            effect.SetStartDelay(delay);
            var trail = Effect.Get("trail1");
            trail.SetStartDelay(delay);
            trail.SetParticleColor(elementRef.colorTrail, changeMaterial: true, "_TintColor").Play(point);
            effect.Play(point).Flip(point.x > center.x);
        }
        origin.PlaySound("spell_ball");
        if (origin.Distance(EClass.pc.pos) < 10 && !EClass.core.config.graphic.disableShake &&
            !EClass.core.config.test.disableShake2) Shaker.ShakeCam("ball");
        EmpLog.Debug("Explosion visual applied in zone {ZoneUid} at {@Pos}, tiles {Count}", ZoneUid, Origin, Points.Length);
    }
}
