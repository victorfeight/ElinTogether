using System.Runtime.CompilerServices;
using HarmonyLib;

namespace ElinTogether.Patches;

// Movement owns its presentation timing. The receiver must not substitute its own
// AI tick cost or turbo, and a decision pause must not change an in-flight step.
[HarmonyPatch(typeof(CharaRenderer), nameof(CharaRenderer.UpdatePosition))]
internal static class CombatPresentation
{
    private sealed class Step(Point position, Zone zone, float duration, float turbo)
    {
        internal readonly Point Position = position.Copy();
        internal readonly Zone Zone = zone;
        internal readonly float Duration = duration;
        internal readonly float Turbo = turbo;
    }

    private static readonly ConditionalWeakTable<Chara, Step> Steps = new();

    internal static void Record(Chara chara, float duration, float turbo)
    {
        Steps.Remove(chara);
        // Packet values affect drawing only. Reject corrupt or nonsensical timing.
        if (!(duration > 0f) || float.IsInfinity(duration) ||
            !(turbo > 0f) || float.IsInfinity(turbo)) return;
        Steps.Add(chara, new Step(chara.pos, chara.currentZone, duration, turbo));
    }

    internal readonly record struct RenderState(bool Applied, float Speed, float Delta, float ActTime);

    [HarmonyPrefix]
    internal static void Before(CharaRenderer __instance, out RenderState __state)
    {
        __state = default;
        var chara = __instance.owner;
        if (!ActionModeCombat.Activated || Game.isPaused ||
            !Steps.TryGetValue(chara, out var step) || step.Zone != chara.currentZone ||
            !step.Position.Equals(chara.pos) ||
            (!__instance.IsMoving && __instance.movePoint.Equals(chara.pos))) return;

        __state = new(true, RenderObject.gameSpeed, RenderObject.gameDelta, chara.actTime);
        RenderObject.gameSpeed = ActionMode.GameSpeeds[EClass.game.gameSpeedIndex] * step.Turbo;
        RenderObject.gameDelta = Core.delta * RenderObject.gameSpeed;
        // Vanilla captures this only when the logical position changes. Restore it
        // immediately afterwards so drawing can never change scheduler accounting.
        chara.actTime = step.Duration;
    }

    [HarmonyFinalizer]
    internal static void After(CharaRenderer __instance, RenderState __state)
    {
        if (!__state.Applied) return;
        __instance.owner.actTime = __state.ActTime;
        RenderObject.gameSpeed = __state.Speed;
        RenderObject.gameDelta = __state.Delta;
    }

    internal static void SnapCorrection(Chara chara, Point from)
    {
        if (from.Distance(chara.pos) > 2) {
            Steps.Remove(chara);
            chara.renderer.SetFirst(true, chara.pos.PositionCenter());
        }
    }
}
