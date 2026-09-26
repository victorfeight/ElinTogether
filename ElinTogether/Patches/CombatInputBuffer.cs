using System.Collections.Generic;
using System.Reflection;
using ElinTogether.Models;
using HarmonyLib;

namespace ElinTogether.Patches;

// Vanilla's Fire/Wait shortcut and ability buttons can perform an action before
// calling EndTurn. Buffer at the input boundary, before ammunition/mana/effects.
// Scripted effects, network replay, counters and scheduled AI execution are exempt.
[HarmonyPatch]
internal static class CombatInputBuffer
{
    private static int _inputDepth;
    private static bool _deferred;

    internal static void BeginInput(out int __state)
    {
        __state = _inputDepth++;
        if (__state == 0) _deferred = false;
    }

    internal static void EndInput(int __state)
    {
        _inputDepth = __state;
        if (__state == 0) _deferred = false;
    }

    private static bool ShouldBuffer(Chara chara) => _inputDepth > 0 && chara.IsPC &&
        ActionModeCombat.Activated && !ActionModeCombat.IsDispatching && !ElinDelta.IsApplying;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Act), nameof(Act.Perform), typeof(Chara), typeof(Card), typeof(Point))]
    private static bool BeforeAct(Act __instance, Chara _cc, Card? _tc, Point? _tp, ref bool __result)
    {
        if (!ShouldBuffer(_cc)) return true;
        var pos = (_tp ?? _tc?.pos ?? _cc.pos).Copy();
        var ranged = _cc.ranged;
        var throwAct = __instance as ActThrow;
        var thrown = throwAct?.target;
        ActionModeCombat.QueueInput(new QueuedCombatAct(__instance.GetText(), () => {
            if (_tc is { isDestroyed: true }) return false;
            if (__instance is ActRanged && ranged is not null &&
                (ranged.isDestroyed || ranged.GetRootCard() != _cc)) return false;
            if (__instance is ActThrow and not ActRanged && thrown is not null &&
                (thrown.isDestroyed || thrown.GetRootCard() != _cc)) return false;
            var oldRanged = _cc.ranged;
            var oldThrown = throwAct?.target;
            try {
                if (__instance is ActRanged) _cc.ranged = ranged;
                if (throwAct is not null) throwAct.target = thrown;
                return __instance.Perform(_cc, _tc, pos);
            } finally {
                _cc.ranged = oldRanged;
                if (throwAct is not null) throwAct.target = oldThrown;
            }
        }));
        _deferred = true;
        __result = false;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.UseAbility), typeof(Act), typeof(Card), typeof(Point), typeof(bool))]
    private static bool BeforeAbility(Chara __instance, Act a, Card? tc, Point? pos, bool pt, ref bool __result)
    {
        if (!ShouldBuffer(__instance)) return true;
        var point = pos?.Copy();
        ActionModeCombat.QueueInput(new QueuedCombatAct(a.GetText(), () =>
            tc is not { isDestroyed: true } && a.CanPerform(__instance, tc, point) &&
            __instance.UseAbility(a, tc, point, pt)));
        _deferred = true;
        __result = false;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.EndTurn))]
    private static bool BeforeEndTurn() => !(_deferred && ShouldBuffer(EClass.pc));
}

[HarmonyPatch]
internal static class CombatInputScope
{
    private static IEnumerable<MethodBase> TargetMethods() => [
        AccessTools.Method(typeof(AM_Adv), nameof(AM_Adv._OnUpdateInput)),
        AccessTools.Method(typeof(ButtonAbility), nameof(ButtonAbility.TryUse)),
    ];

    [HarmonyPrefix]
    private static void Before(out int __state) => CombatInputBuffer.BeginInput(out __state);

    [HarmonyFinalizer]
    private static void After(int __state) => CombatInputBuffer.EndInput(__state);
}
