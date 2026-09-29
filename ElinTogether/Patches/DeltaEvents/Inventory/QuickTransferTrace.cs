using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ElinTogether.Patches;

// Diagnostics only: never consume input, refresh an inventory, resolve a RemoteCard,
// or change a transfer result. Gesture/attempt IDs are local to each process;
// compare peers by session, item UID, container UID and packet origin.
internal static class QuickTransferTrace
{
    private static int _gesture;
    private static int _edgeFrame = -1;
    private static int _attempt;
    private static float _until;
    private static bool _held;
    private static string? _hover;
    private static string? _lastAttempt;
    private static int _repeats;
    private static object? _connection;
    internal static int ActiveAttempt;
    internal static bool Enabled => NetSession.Instance.Connection is not null;
    internal static bool Watching => Enabled && (Time.realtimeSinceStartup <= _until ||
        (EInput.isShiftDown && Input.GetMouseButton(0)));

    internal static string Item(Card? c) => c is null ? "none" :
        $"{c.uid}/{c.id} count={c.Num} parent={(c.parent as Card)?.uid ?? -1} slot={c.invX},{c.invY} destroyed={c.isDestroyed}";

    internal static string Button(ButtonGrid? b) => b == null ? "none" :
        $"button={b.GetInstanceID()} index={b.index} owner={b.invOwner?.Container?.uid ?? -1} item=[{Item(b.card)}]";

    internal static void Log(string stage, string detail)
    {
        if (!Enabled) return;
        var p = Input.mousePosition;
        EmpLog.Debug("QuickTransfer {Stage}: gesture {Gesture}, attempt {Attempt}, frame {Frame}, time {Time}, mouse {MouseX},{MouseY}, shift {Shift}, rawShift {RawShift}, leftDown {Down}, leftHeld {Held}, leftUp {Up}, rightHeld {Right}, skip {Skip}, replay {Replay}, detail {Detail}",
            stage, _gesture, ActiveAttempt, Time.frameCount, Time.realtimeSinceStartup, p.x, p.y,
            EInput.isShiftDown, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
            Input.GetMouseButtonDown(0), Input.GetMouseButton(0), Input.GetMouseButtonUp(0),
            Input.GetMouseButton(1), EInput.skipFrame, ElinDelta.IsApplying, detail);
    }

    private static void FlushRepeats()
    {
        if (_repeats > 0) Log("repeat-summary", $"unchanged attempts={_repeats} last={_lastAttempt}");
        _repeats = 0;
        _lastAttempt = null;
    }

    internal static void ObserveMouse()
    {
        if (!Enabled) { _held = false; _until = 0; _hover = null; return; }
        if (!ReferenceEquals(_connection, NetSession.Instance.Connection)) {
            _connection = NetSession.Instance.Connection;
            _held = false;
            _edgeFrame = -1;
            _until = 0;
            _hover = _lastAttempt = null;
            _repeats = 0;
            Log("trace-start", "quick-transfer-v2; local gesture IDs; correlate peers by item/container UID");
        }
        var held = Input.GetMouseButton(0);
        if (Input.GetMouseButtonDown(0) && _edgeFrame != Time.frameCount) {
            FlushRepeats();
            _edgeFrame = Time.frameCount;
            _gesture++;
            _hover = null;
            if (EInput.isShiftDown) _until = Time.realtimeSinceStartup + 1f;
            if (Watching) Log("press", "physical mouse-down");
        }
        if (_held && !held && Watching) {
            FlushRepeats();
            Log("release", "physical mouse release observed");
            _until = Time.realtimeSinceStartup + 1f;
        }
        _held = held;
        if (!Watching || InputModuleEX.list is null) return;
        var hover = Button(InputModuleEX.GetComponentOf<ButtonGrid>());
        if (hover == _hover) return;
        _hover = hover;
        Log("hover", hover);
    }

    internal sealed record Attempt(Thing Item, int Previous, bool Logged);

    [HarmonyPatch(typeof(ActionMode), nameof(ActionMode.UpdateInput))]
    internal static class InputTrace
    {
        [HarmonyPrefix]
        internal static void Before() => ObserveMouse();
    }

    [HarmonyPatch(typeof(UIButton), nameof(UIButton.OnPointerDown))]
    internal static class PointerTrace
    {
        [HarmonyPrefix]
        internal static void Before(UIButton __instance, PointerEventData eventData)
        {
            if (!Enabled || __instance is not ButtonGrid b) return;
            ObserveMouse();
            if (Watching) Log("pointer-down", $"eventButton={eventData.button} instant={b.instantClick} {Button(b)}");
        }
    }

    [HarmonyPatch(typeof(InvOwner), nameof(InvOwner.OnClick))]
    internal static class ClickTrace
    {
        [HarmonyPrefix]
        internal static void Before(ButtonGrid button)
        {
            if (Watching) Log("button-callback", Button(button));
        }
    }

    [HarmonyPatch(typeof(InvOwner), nameof(InvOwner.OnShiftClick))]
    internal static class TransferTrace
    {
        [HarmonyPrefix]
        internal static void Before(ButtonGrid b, bool rightMouse, out Attempt? __state)
        {
            __state = null;
            if (!Enabled || b?.card is not Thing thing) return;
            ObserveMouse();
            _until = Time.realtimeSinceStartup + 1f;
            var previous = ActiveAttempt;
            ActiveAttempt = ++_attempt;
            // Keep the call path for distinct attempts, including different slots.
            // Repeated no-op held frames are summarized, not dumped at frame rate.
            var frames = new StackTrace(1, false).GetFrames();
            var route = "unknown";
            if (frames is not null) foreach (var frame in frames) {
                var method = frame.GetMethod();
                if (method?.Name?.Contains("UpdateInput") == true) { route = "held-input"; break; }
                if (method?.Name?.Contains("OnClick") == true) { route = "button-callback"; break; }
            }
            var key = $"route={route} right={rightMouse} {Button(b)}";
            var logged = key != _lastAttempt;
            if (logged) {
                FlushRepeats();
                _lastAttempt = key;
                Log("transfer-enter", key + " stack=" + new StackTrace(1, false));
            } else _repeats++;
            __state = new(thing, previous, logged);
        }

        [HarmonyFinalizer]
        internal static void After(Attempt? __state, Exception? __exception)
        {
            if (__state is null) return;
            if (__state.Logged || __exception is not null)
                Log("transfer-exit", $"{Item(__state.Item)} error={__exception}");
            ActiveAttempt = __state.Previous;
        }
    }

    [HarmonyPatch(typeof(ButtonGrid), nameof(ButtonGrid.SetCardGrid))]
    internal static class BindingTrace
    {
        [HarmonyPrefix]
        internal static void Before(ButtonGrid __instance, Card c, InvOwner owner)
        {
            if (!Watching || (__instance.card == c && (owner is null || owner == __instance.invOwner))) return;
            Log("button-rebind", $"old=[{Button(__instance)}] newOwner={owner?.Container?.uid ?? -1} new=[{Item(c)}]");
        }
    }

    [HarmonyPatch(typeof(UIInventory), nameof(UIInventory.Sort))]
    internal static class SortTrace
    {
        [HarmonyPrefix]
        internal static void Before(UIInventory __instance)
        {
            if (Watching) Log("sort", $"container={__instance.owner?.Container?.uid} stack={new StackTrace(1, false)}");
        }
    }

    [HarmonyPatch(typeof(ThingContainer), nameof(ThingContainer.RefreshGrid), new Type[] { })]
    internal static class GridTrace
    {
        [HarmonyPrefix]
        internal static void Before(ThingContainer __instance, out List<(Thing Item, int X, int Y)>? __state)
        {
            __state = null;
            if (!Watching) return;
            __state = new(__instance.Count);
            foreach (var item in __instance) __state.Add((item, item.invX, item.invY));
        }

        [HarmonyPostfix]
        internal static void After(ThingContainer __instance, List<(Thing Item, int X, int Y)>? __state)
        {
            if (__state is null) return;
            foreach (var old in __state) {
                if (old.X != old.Item.invX || old.Y != old.Item.invY)
                    Log("grid-reassign", $"container={__instance.owner?.uid} oldSlot={old.X},{old.Y} new=[{Item(old.Item)}]");
            }
        }
    }

    // These logs run even when the receiving peer has no inventory open.
    private static string? Packet(ElinDelta delta) => delta switch {
        CardAddThingDelta d => $"add uid={d.Thing.Uid} parent={d.Parent.Uid} stack={d.TryStack} slot={d.DestInvX},{d.DestInvY}",
        CardTryStackToDelta d => $"stack uid={d.Card.Uid} to={d.To.Uid} parent={d.Parent?.Uid}",
        CardRemoveThingDelta d => $"remove uid={d.Thing.Uid}",
        _ => null,
    };

    [HarmonyPatch(typeof(ElinDeltaManager), nameof(ElinDeltaManager.AddRemote))]
    internal static class QueueTrace
    {
        [HarmonyPostfix]
        internal static void After(ElinDelta delta)
        {
            if (Enabled && Packet(delta) is { } detail)
                Log("packet-queued", $"origin={delta.OriginPeer} {detail}");
        }
    }

    [HarmonyPatch]
    internal static class PacketTrace
    {
        internal static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CardAddThingDelta), "OnApply");
            yield return AccessTools.Method(typeof(CardTryStackToDelta), "OnApply");
            yield return AccessTools.Method(typeof(CardRemoveThingDelta), "OnApply");
        }

        [HarmonyPrefix]
        internal static void Before(ElinDelta __instance)
        {
            Log("packet-apply", $"origin={__instance.OriginPeer} {Packet(__instance)}");
        }
    }

    internal sealed record Move(Thing Item, string Before);

    [HarmonyPatch(typeof(Card), nameof(Card.AddThing), typeof(Thing), typeof(bool), typeof(int), typeof(int))]
    internal static class MoveTrace
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        internal static void Before(Card __instance, Thing t, bool tryStack, int destInvX, int destInvY, out Move? __state)
        {
            __state = null;
            if (!Enabled || t is null) return;
            __state = new(t, Item(t));
            Log("move-enter", $"{__state.Before} destination={__instance.uid} stack={tryStack} slot={destInvX},{destInvY}");
        }

        [HarmonyFinalizer]
        internal static void After(Move? __state, Thing __result, Exception? __exception)
        {
            if (__state is null) return;
            Log("move-exit", $"before=[{__state.Before}] after=[{Item(__state.Item)}] returned=[{Item(__result)}] error={__exception}");
        }
    }

    [HarmonyPatch(typeof(Card), nameof(Card.TryStackTo))]
    internal static class StackTracePatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        internal static void Before(Card __instance, Thing to, out string? __state)
        {
            __state = null;
            if (!Enabled || ActiveAttempt == 0) return;
            __state = $"source=[{Item(__instance)}] target=[{Item(to)}]";
            Log("stack-enter", __state);
        }

        [HarmonyFinalizer]
        internal static void After(Card __instance, Thing to, string? __state, bool __result, Exception? __exception)
        {
            if (__state is null) return;
            Log("stack-exit", $"before=[{__state}] source=[{Item(__instance)}] target=[{Item(to)}] result={__result} error={__exception}");
        }
    }
}
