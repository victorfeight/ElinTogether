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
        ActionModeCombat.QueueInput(CreateAct(__instance, _cc, _tc, _tp));
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
        ActionModeCombat.QueueInput(new QueuedCombatAct(a.GetText(),
            () => __instance.UseAbility(a, tc, point, pt),
            () => TargetExists(tc) && !__instance.HasCooldown(a.id) &&
                  a.CanPerform(__instance, tc, point) && a.ValidatePerform(__instance, tc, point)));
        _deferred = true;
        __result = false;
        return false;
    }

    // Shared by shortcuts and ActPlan clicks. Preserve captured equipment around both
    // validation and execution; Act instances (including ACT.Throw) are singletons.
    internal static QueuedCombatAct CreateAct(Act act, Chara actor, Card? target, Point? point)
    {
        var pos = (point ?? target?.pos ?? actor.pos).Copy();
        var ranged = actor.ranged;
        var throwAct = act as ActThrow;
        var thrown = throwAct?.target;
        bool WithEquipment(bool execute)
        {
            if (!TargetExists(target)) return false;
            if (act is ActRanged && (ranged is null || ranged.isDestroyed ||
                !ranged.CanAutoFire(actor, target))) return false;
            if (act is ActThrow and not ActRanged && (thrown is null || thrown.isDestroyed ||
                thrown.GetRootCard() != actor)) return false;
            var oldRanged = actor.ranged;
            var oldThrown = throwAct?.target;
            try {
                if (act is ActRanged) actor.ranged = ranged;
                if (throwAct is not null) throwAct.target = thrown;
                return execute ? act.Perform(actor, target, pos) : act.CanPerform(actor, target, pos);
            } finally {
                actor.ranged = oldRanged;
                if (throwAct is not null) throwAct.target = oldThrown;
            }
        }
        return new QueuedCombatAct(act.GetText(), () => WithEquipment(true), () => WithEquipment(false));
    }

    private static bool TargetExists(Card? target) => target is not { isDestroyed: true } &&
        target is not Chara { isDead: true };

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
