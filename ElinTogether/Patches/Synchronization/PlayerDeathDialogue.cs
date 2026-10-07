using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Vanilla's three death-dialog callbacks outlive Scene.OnUpdate. A rescue must
// invalidate them, including the delayed callback that opens the choice window.
[HarmonyPatch]
internal static class PlayerDeathDialogue
{
    private static Dialog? _dialog;

    internal static IEnumerable<MethodBase> TargetMethods()
    {
        var methods = typeof(Scene).GetNestedTypes(AccessTools.all)
            .SelectMany(t => t.GetMethods(AccessTools.all | BindingFlags.DeclaredOnly))
            .Where(m => m.GetMethodBody() != null).ToArray();
        foreach (var marker in new[] { "pc_revive_town", "pc_deathChoice", "crawlup" }) {
            var matches = methods.Where(m => PatchProcessor.GetOriginalInstructions(m)
                .Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, marker))).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException($"Native death dialogue changed: {marker}.");
            yield return matches[0];
        }
    }

    [HarmonyPrefix]
    internal static bool StillDead() => NetSession.Instance.Connection == null || EClass.pc.isDead;

    internal static void Rescued(Chara actor)
    {
        if (NetSession.Instance.Connection == null || !actor.IsPC || actor.isDead) return;
        EClass.player.deathDialog = false;
        CharaReviveEvent.ClearPendingLastWords();
        var dialog = _dialog;
        _dialog = null;
        if (dialog != null && dialog) dialog.Close(); // OnKill callback is guarded above.
    }

    internal static void Track(string title, Dialog dialog)
    {
        if (NetSession.Instance.Connection != null && EClass.pc.isDead &&
            (title == "dialogLastword" || title == "pc_deathChoice".lang())) _dialog = dialog;
    }

    [ElinPreLoad] private static void Reset(GameIOContext context) => _dialog = null;
}

[HarmonyPatch]
internal static class PlayerDeathDialogTracking
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Dialog), nameof(Dialog.InputName));
        yield return AccessTools.Method(typeof(Dialog), nameof(Dialog.List)).MakeGenericMethod(typeof(string));
    }

    [HarmonyPostfix]
    internal static void Opened(string langDetail, Dialog __result) => PlayerDeathDialogue.Track(langDetail, __result);
}
