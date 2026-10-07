using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class PlayerRecoveryPatch
{
    private static Chara? _resurrector;
    private sealed class EffectScope : IDisposable
    {
        private readonly Chara? _previous = _resurrector;
        private readonly IDisposable _simulation = ElinDelta.Simulate();
        internal EffectScope(Chara? caster) => _resurrector = caster;
        public void Dispose() { _resurrector = _previous; _simulation.Dispose(); }
    }

    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(Chara), nameof(Chara.Die))]
    internal static void CaptureDeath(Chara __instance, out PlayerDeathRecovery.DeathSite? __state) =>
        __state = PlayerDeathRecovery.Capture(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(Chara), nameof(Chara.Die))]
    internal static void DropAtDeath(Chara __instance, PlayerDeathRecovery.DeathSite? __state) =>
        PlayerDeathRecovery.DropOverweight(__instance, __state);

    [HarmonyPrefix, HarmonyPatch(typeof(ActEffect), nameof(ActEffect.Proc), typeof(EffectId), typeof(int),
        typeof(BlessedState), typeof(Card), typeof(Card), typeof(ActRef))]
    internal static bool Resurrection(EffectId id, Card cc, Card? tc, out IDisposable? __state)
    {
        __state = null;
        if (id != EffectId.Revive || NetSession.Instance.Connection == null) return true;
        // Scroll use / spell performance already has an intent route. Never
        // choose or revive a second target while predicting/replaying on clients.
        if (NetSession.Instance.Connection is ElinNetClient) return false;
        __state = new EffectScope(cc.Chara);
        if (cc is not Chara caster || PlayerDeathRecovery.RescueTarget(caster, tc ?? cc) is not { } rescued)
            return true;
        var point = caster.pos.GetNearestPoint(allowBlock: false, allowChara: false);
        if (point == null) return true;
        rescued.Revive(point, msg: true);
        if (!rescued.isDead && !rescued.IsPC && rescued.c_wasInPcParty && !rescued.IsPCParty)
            EClass.pc.party.AddMemeber(rescued, showMsg: true);
        EmpLog.Information("Player resurrection: caster {CasterUid}, target {TargetUid}, zone {ZoneUid}, alive {Alive}",
            caster.uid, rescued.uid, EClass._zone.uid, !rescued.isDead);
        return false;
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(ActEffect), nameof(ActEffect.Proc), typeof(EffectId), typeof(int),
        typeof(BlessedState), typeof(Card), typeof(Card), typeof(ActRef))]
    internal static void EndEffect(IDisposable? __state) => __state?.Dispose();

    internal static Chara ResurrectionOrigin() => _resurrector ?? EClass.pc;

    [HarmonyTranspiler, HarmonyPatch(typeof(Chara), nameof(Chara.GetRevived))]
    internal static IEnumerable<CodeInstruction> ReviveNearCaster(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pc = AccessTools.PropertyGetter(typeof(EClass), nameof(EClass.pc));
        var pos = AccessTools.Field(typeof(Card), nameof(Card.pos));
        var matches = Enumerable.Range(0, Math.Max(0, codes.Count - 1))
            .Where(i => codes[i].Calls(pc) && codes[i + 1].LoadsField(pos)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Native resurrection position changed.");
        codes[matches[0]].operand = AccessTools.Method(typeof(PlayerRecoveryPatch), nameof(ResurrectionOrigin));
        return codes;
    }
}
