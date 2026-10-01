using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using ElinTogether.Models;
using HarmonyLib;

namespace ElinTogether.Patches;

// Observe actual native decisions; never rerun GetDest/stacking/grid refresh for diagnostics.
// Attempt numbers are process-local. Correlate peers using session, actor and item UID.
internal static class PickupTrace
{
    private static int _nextAttempt;
    [ThreadStatic] private static Attempt? _current;

    internal sealed class Attempt
    {
        internal readonly int Id = ++_nextAttempt;
        internal readonly Attempt? Previous = _current;
        internal readonly Chara Actor;
        internal readonly Card Item;
        internal readonly string Before;
        internal readonly string Operation;
        internal readonly string Context;
        internal string Destination = "not-evaluated";
        internal string Overflow = "not-evaluated";
        internal readonly string Route;

        internal Attempt(Chara actor, Card item, string operation)
        {
            Actor = actor;
            Item = item;
            Before = Describe(item);
            Operation = operation;
            Context = $"ai={actor.ai?.GetType().Name} currentAI={actor.ai?.Current?.GetType().Name} packedOwner={CharaProgressCompleteEvent.Chara?.uid} packedAction={CharaProgressCompleteEvent.Action?.GetType().Name} replayOwner={CharaProgressCompleteDelta.Current?.Owner.Uid}";
            // Only explicit drop entry points need a caller trace, not every pickup.
            Route = operation is nameof(Chara.DropHeld) or nameof(Chara.DropThing)
                ? new StackTrace(2, false).ToString() : "";
        }
    }

    private static string Describe(Card? item) => item is null ? "none" :
        $"{QuickTransferTrace.Item(item)} material={item.idMaterial} parentType={item.parent?.GetType().Name} pos={item.pos?.x},{item.pos?.z}";

    private static Attempt? Begin(Chara actor, Card? item, string operation)
    {
        if (!QuickTransferTrace.Enabled || item is null) return null;
        return _current = new Attempt(actor, item, operation);
    }

    private static void End(Attempt? state, Exception? error, string result)
    {
        if (state is null) return;
        _current = state.Previous;
        EmpLog.Debug("PickupTrace {Operation}: attempt {Attempt}, actor {ActorUid}, item {ItemUid}, applying {Applying}, progressReplay {ProgressReplay}, before {Before}, after {After}, destination {Destination}, overflow {Overflow}, result {Result}, error {Error}, route {Route}, context {Context}",
            state.Operation, state.Id, state.Actor.uid, state.Item.uid, ElinDelta.IsApplying,
            CharaProgressCompleteDelta.IsReplaying, state.Before, Describe(state.Item),
            state.Destination, state.Overflow, result, error?.ToString(), state.Route, state.Context);
    }

    [HarmonyPatch(typeof(Chara), nameof(Chara.Pick))]
    internal static class Pick
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        internal static void Before(Chara __instance, Thing t, out Attempt? __state) =>
            __state = Begin(__instance, t, nameof(Chara.Pick));

        [HarmonyFinalizer]
        internal static void After(Attempt? __state, Thing __result, Exception? __exception) =>
            End(__state, __exception, Describe(__result));
    }

    [HarmonyPatch(typeof(ThingContainer), nameof(ThingContainer.GetDest))]
    internal static class Destination
    {
        [HarmonyPostfix]
        internal static void After(ThingContainer __instance, ThingContainer.DestData __result)
        {
            if (_current is not { } state || !ReferenceEquals(__instance, state.Actor.things)) return;
            var freeSlots = 0;
            if (__instance.grid is { } grid)
                foreach (var item in grid) if (item is null) freeSlots++;
            state.Destination = $"valid={__result.IsValid} container={__result.container?.uid} stack=[{Describe(__result.stack)}] count={__instance.Count} hasGrid={__instance.HasGrid} gridSize={__instance.GridSize} freeGridSlots={freeSlots}";
        }
    }

    [HarmonyPatch(typeof(ThingContainer), nameof(ThingContainer.IsOverflowing))]
    internal static class Overflow
    {
        [HarmonyPostfix]
        internal static void After(ThingContainer __instance, bool __result)
        {
            if (_current is { } state && ReferenceEquals(__instance, state.Actor.things))
                state.Overflow = $"{__result} count={__instance.Count} hasGrid={__instance.HasGrid} gridCount={__instance.grid?.Count}";
        }
    }

    [HarmonyPatch]
    internal static class Release
    {
        internal static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Chara), nameof(Chara.PickHeld));
            yield return AccessTools.Method(typeof(Chara), nameof(Chara.DropHeld));
            yield return AccessTools.Method(typeof(Chara), nameof(Chara.DropThing));
        }

        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        internal static void Before(Chara __instance, MethodBase __originalMethod, object[] __args, out Attempt? __state) =>
            __state = Begin(__instance, __originalMethod.Name == nameof(Chara.DropThing)
                ? __args[0] as Card : __instance.held, __originalMethod.Name);

        [HarmonyFinalizer]
        internal static void After(Attempt? __state, Exception? __exception) =>
            End(__state, __exception, "see-after");
    }
}
