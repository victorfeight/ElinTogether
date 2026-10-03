using System;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Core), nameof(Core.Update))]
internal static class CoreNetworkTrace
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    internal static void Before(out NetworkFlightRecorder.Scope __state)
    {
        __state = default;
        if (NetSession.Instance.Connection is not { } connection) return;
        connection.Trace.Expect("core", true);
        __state = connection.Trace.Enter("core", "Core.Update");
        connection.CaptureTraceContext();
    }

    [HarmonyFinalizer]
    internal static void After(Exception? __exception, NetworkFlightRecorder.Scope __state)
    {
        if (__exception is not null) NetSession.Instance.Connection?.Trace.Fault("Core.Update: " + __exception);
        __state.Dispose();
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.Save))]
internal static class SaveNetworkTrace
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    internal static void Before(out NetworkFlightRecorder.Scope __state)
    {
        __state = default;
        if (NetSession.Instance.Connection is not { } connection) return;
        connection.Trace.Mark("save", "begin");
        __state = connection.Trace.Enter("save", "Game.Save");
    }

    [HarmonyFinalizer]
    internal static void After(Exception? __exception, NetworkFlightRecorder.Scope __state)
    {
        __state.Dispose();
        NetSession.Instance.Connection?.Trace.Mark("save", __exception is null ? "returned" : "threw " + __exception);
    }
}
