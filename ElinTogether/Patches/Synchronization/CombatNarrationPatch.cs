using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class CombatNarrationScopePatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => [
        AccessTools.Method(typeof(AttackProcess), nameof(AttackProcess.Perform)),
        AccessTools.Method(typeof(ActMelee), nameof(ActMelee.Attack)),
        AccessTools.Method(typeof(Card), nameof(Card.DamageHP), [typeof(long), typeof(int), typeof(int),
            typeof(AttackSource), typeof(Card), typeof(bool), typeof(Thing), typeof(Chara), typeof(int)]),
    ];

    [HarmonyPrefix]
    internal static void Before(out CombatNarration.Scope? __state) => __state = CombatNarration.Begin();

    // Always unwind, including native errors and early returns. Completed fragments
    // are presentation of work already done, not evidence that an action succeeded.
    [HarmonyFinalizer]
    internal static void After(CombatNarration.Scope? __state) => CombatNarration.End(__state);
}

[HarmonyPatch]
internal static class CombatNarrationMessagePatch
{
    private static readonly Dictionary<MethodBase, CombatText.Format> Formats = [];
    internal static CombatText? Pending { get; private set; }

    internal static IEnumerable<MethodBase> TargetMethods()
    {
        var shapes = new (CombatText.Format Kind, Type[] Types)[] {
            (CombatText.Format.Raw, [typeof(string)]),
            (CombatText.Format.Strings, [typeof(string), typeof(string), typeof(string), typeof(string), typeof(string)]),
            (CombatText.Format.Cards, [typeof(string), typeof(Card), typeof(Card), typeof(string), typeof(string)]),
            (CombatText.Format.CardStrings, [typeof(string), typeof(Card), typeof(string), typeof(string), typeof(string)]),
            (CombatText.Format.CardNumber, [typeof(string), typeof(Card), typeof(int), typeof(string)]),
            (CombatText.Format.Number, [typeof(string), typeof(int), typeof(string), typeof(string)]),
        };
        foreach (var (kind, types) in shapes) {
            var method = AccessTools.Method(typeof(Msg), nameof(Msg.Say), types);
            Formats[method] = kind;
            yield return method;
        }
    }

    [HarmonyPrefix]
    internal static bool Before(MethodBase __originalMethod, object[] __args, ref string __result,
        out CombatText? __state)
    {
        __state = Pending;
        Pending = null;
        if (CombatNarration.Current is null || CombatNarration.Rendering || Msg.ignoreAll ||
            !EClass.sources.langGame.map.TryGetValue((string)__args[0], out var row) || row.group != "combat") return true;
        if (NetSession.Instance.Connection is ElinNetClient) {
            // Includes local prediction and remote replay. The host supplies the
            // complete hit/miss narration; neither client roll may author its text.
            Msg.SetColor(); Msg.alwaysVisible = false;
            __result = "";
            return false;
        }
        var kind = Formats[__originalMethod];
        Pending = new CombatText {
            Kind = kind,
            Speaker = CombatNarrationSpeakerPatch.Current?.Speaker,
            First = kind is CombatText.Format.Cards or CombatText.Format.CardStrings or CombatText.Format.CardNumber
                ? __args[1] as Card : null,
            Second = kind == CombatText.Format.Cards ? __args[2] as Card : null,
            Number = kind == CombatText.Format.Number ? (int)__args[1] : kind == CombatText.Format.CardNumber ? (int)__args[2] : 0,
            Refs = __args.Skip(kind switch {
                CombatText.Format.Raw => __args.Length,
                CombatText.Format.Strings => 1,
                CombatText.Format.CardStrings or CombatText.Format.Number => 2,
                _ => 3,
            }).Select(x => x as string).ToArray(),
        };
        return true;
    }

    [HarmonyFinalizer]
    internal static void After(CombatText? __state) => Pending = __state;
}

[HarmonyPatch]
internal static class CombatNarrationTextPatch
{
    [HarmonyPostfix, HarmonyPatch(typeof(Msg), nameof(Msg.GetGameText))]
    internal static void Template(string __result)
    {
        if (CombatNarrationMessagePatch.Pending is { } pending) pending.Template = __result;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Msg), nameof(Msg.SayRaw))]
    internal static bool Capture(string text, ref string __result)
    {
        if (Msg.ignoreAll || MsgRelayContext.IsSuppressing || CombatNarration.Rendering || NetSession.Instance.Connection is not ElinNetHost ||
            CombatNarration.Current is not { } scope || CombatNarrationMessagePatch.Pending is not { } pending) return true;
        var color = Msg.currentColor;
        pending.R = color.r; pending.G = color.g; pending.B = color.b; pending.A = color.a;
        pending.Fallback = text;
        scope.Text.Add(pending);
        if (CombatNarrationSpeakerPatch.Current is not { ShowLocally: false }) return true;
        __result = text;
        Msg.SetColor(); Msg.alwaysVisible = false;
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Msg), nameof(Msg.NewLine))]
    internal static void NewLine()
    {
        if (!CombatNarration.Rendering && NetSession.Instance.Connection is ElinNetHost &&
            CombatNarration.Current is { Text.Count: > 0 } scope)
            scope.Text.Add(new CombatText { NewLine = true });
    }
}

// Capture messages visible to a remote player even when the host cannot see the
// speaker. Preserve the host's original visibility; only clients resolve their own.
[HarmonyPatch]
internal static class CombatNarrationSpeakerPatch
{
    internal sealed class Frame
    {
        internal required Card Speaker;
        internal bool ShowLocally;
    }
    internal static Frame? Current { get; private set; }
    private static bool _checking;

    internal static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(Card))
        .Where(m => m.Name == nameof(Card.Say));

    [HarmonyPrefix]
    internal static void Before(Card __instance, string lang, out Frame? __state)
    {
        __state = Current;
        Current = null;
        if (CombatNarration.Current is null || NetSession.Instance.Connection is not ElinNetHost ||
            !EClass.sources.langGame.map.TryGetValue(lang, out var row) || row.group != "combat") return;
        bool show;
        try { _checking = true; show = __instance.ShouldShowMsg; }
        finally { _checking = false; }
        Current = new Frame { Speaker = __instance, ShowLocally = show };
    }

    [HarmonyFinalizer]
    internal static void After(Frame? __state) => Current = __state;

    [HarmonyPatch(typeof(Card), nameof(Card.ShouldShowMsg), MethodType.Getter)]
    internal static class Visibility
    {
        [HarmonyPostfix]
        internal static void After(Card __instance, ref bool __result)
        {
            if (!_checking && Current?.Speaker == __instance && __instance is not Chara { isDead: true }) __result = true;
        }
    }
}
