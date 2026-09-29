using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Helper;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Player), nameof(Player.ModKarma))]
internal static class PersonalKarmaPatch
{
    [HarmonyPrefix]
    internal static bool Before(int a) => PersonalKarma.BeforeChange(a);
}

// Scope synchronous native execution, never an IEnumerator across yields.
// A finalizer restores the previous actor even when another patch skips/throws.
[HarmonyPatch]
internal static class KarmaActorScopePatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        return OverrideMethodComparer.FindAllOverrides(typeof(AIAct), nameof(AIAct.OnProgressComplete))
            .Concat(OverrideMethodComparer.FindAllOverrides(typeof(Trait), nameof(Trait.OnRead), typeof(Chara)))
            .Concat(CardOnUseEvent.TargetMethods())
            .Concat(CharaActPerformEvent.TargetMethods())
            .Concat(new MethodBase[] {
                AccessTools.Method(typeof(Act), nameof(Act.Perform), [typeof(Chara), typeof(Card), typeof(Point)]),
                AccessTools.Method(typeof(FoodEffect), nameof(FoodEffect.Proc)),
                AccessTools.Method(typeof(FoodEffect), nameof(FoodEffect.ProcTrait)),
                AccessTools.Method(typeof(FoodEffect), nameof(FoodEffect.ProcNutrition)),
                AccessTools.Method(typeof(Chara), nameof(Chara.Die)),
                AccessTools.Method(typeof(Chara), nameof(Chara.TryDropBossLoot)),
                AccessTools.Method(typeof(Card), nameof(Card.Kick), [typeof(Chara), typeof(bool), typeof(bool), typeof(bool), typeof(bool)]),
                AccessTools.Method(typeof(Chara), nameof(Chara.HoldCard)),
                AccessTools.Method(typeof(ActEffect), nameof(ActEffect.Poison)),
                AccessTools.Method(typeof(Trait), nameof(Trait.OnLockOpen)),
                AccessTools.Method(typeof(Trait), nameof(Trait.TryPryOpenLock)),
                AccessTools.Method(typeof(Card), nameof(Card.TryUnrestrain)),
                AccessTools.Method(typeof(ActEffect), nameof(ActEffect.DamageEle)),
                AccessTools.Method(typeof(Player), nameof(Player.OnAdvanceDay)),
                AccessTools.Method(typeof(TraitAltar), nameof(TraitAltar._OnOffer)),
                AccessTools.Method(typeof(ConTransmuteBat), nameof(ConTransmuteBat.CheckSeen)),
            }).Distinct();
    }

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    internal static void Before(object? __instance, MethodBase __originalMethod, object[] __args, out IDisposable __state)
    {
        Card? actor = __instance switch {
            AIAct ai => ai.owner,
            ConTransmuteBat bat => bat.owner,
            Chara c when __originalMethod.Name == nameof(Chara.Die) => __args[1] as Card,
            Card c => c,
            _ => __args.FirstOrDefault() as Card,
        };
        if (__originalMethod.Name == nameof(Act.Perform))
            actor = __args.Length == 0 ? Act.CC : __args[0] as Card;
        if (__originalMethod.DeclaringType == typeof(ActEffect))
            actor = __args[__originalMethod.Name == nameof(ActEffect.Poison) ? 1 : 0] as Card;
        if (__originalMethod.Name == nameof(Card.TryUnrestrain)) actor = __args[1] as Card;
        // Boss loot is a world reward, not the victim's personal action.
        if (__originalMethod.DeclaringType == typeof(Player) ||
            __originalMethod.Name == nameof(Chara.TryDropBossLoot)) actor = PersonalKarma.HostPlayer;
        __state = PersonalKarma.For(actor, __instance is DynamicAct or DynamicAIAct ||
            __originalMethod.Name == nameof(Chara.HoldCard));
    }

    [HarmonyFinalizer]
    internal static void After(IDisposable? __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(Zone), nameof(Zone.IsCrime))]
internal static class PersonalCrimePatch
{
    [HarmonyPostfix]
    internal static void After(Zone __instance, Chara c, Act act, ref bool __result)
    {
        if (NetSession.Instance.Connection is ElinNetHost && PersonalKarma.IsHuman(c))
            __result = act.IsHostileAct && __instance.HasLaw && !__instance.IsPCFaction;
    }
}

[HarmonyPatch(typeof(Zone), nameof(Zone.RefreshCriminal))]
internal static class PersonalGuardRefreshPatch
{
    private static HashSet<int> _criminals = [];

    [ElinPreLoad]
    internal static void Reset(GameIOContext context) => _criminals.Clear();
    [HarmonyPrefix]
    internal static bool Before(Zone __instance)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host) return true;
        var law = __instance.HasLaw && !__instance.AllowCriminal && !__instance.IsPCFaction;
        var current = host.ActiveRemoteCharas.Values.Append(PersonalKarma.HostPlayer)
            .Where(PersonalKarma.IsCriminal).Select(c => c.uid).ToHashSet();
        var criminal = law && current.Count != 0;
        foreach (var guard in EClass._map.charas) {
            if (guard.trait is not TraitGuard) continue;
            guard.hostility = criminal ? Hostility.Neutral : Hostility.Friend;
            if (guard.enemy is not { IsPCParty: true } enemy) continue;
            var actor = ActorOwnership.PlayerFor(enemy) ?? PersonalKarma.HostPlayer;
            if (!criminal || (_criminals.Contains(actor.uid) && !current.Contains(actor.uid))) guard.SetEnemy();
        }
        _criminals = current;
        return false;
    }
}

[HarmonyPatch(typeof(Chara), nameof(Chara.IsHostile), typeof(Chara))]
internal static class PersonalGuardTargetPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var getter = AccessTools.PropertyGetter(typeof(Player), nameof(Player.IsCriminal));
        foreach (var instruction in instructions) {
            if (!instruction.Calls(getter)) { yield return instruction; continue; }
            yield return new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PersonalGuardTargetPatch), nameof(IsCriminal)));
        }
    }

    internal static bool IsCriminal(Player player, Chara target) => NetSession.Instance.Connection is ElinNetHost
        ? PersonalKarma.IsCriminal(ActorOwnership.PlayerFor(target) ?? PersonalKarma.HostPlayer)
        : player.IsCriminal;
}

// Extend only an IsPC check immediately guarding a native karma event. Other
// player-only food/read/reward branches must keep their original meaning.
[HarmonyPatch]
internal static class KarmaEligibilityPatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => [
        AccessTools.Method(typeof(TraitBaseSpellbook), nameof(TraitBaseSpellbook.OnRead)),
        AccessTools.Method(typeof(TraitIndulgence), nameof(TraitIndulgence.OnRead)),
        AccessTools.Method(typeof(FoodEffect), nameof(FoodEffect.Proc)),
        AccessTools.Method(typeof(TraitAltar), nameof(TraitAltar._OnOffer)),
        AccessTools.Method(typeof(ActEffect), nameof(ActEffect.DamageEle)),
        AccessTools.Method(typeof(Trait), nameof(Trait.OnLockOpen)),
        AccessTools.Method(typeof(Trait), nameof(Trait.TryPryOpenLock)),
        AccessTools.Method(typeof(Card), nameof(Card.TryUnrestrain)),
        AccessTools.Method(typeof(Chara), nameof(Chara.HoldCard)),
        AccessTools.Method(typeof(Card), nameof(Card.Kick), [typeof(Chara), typeof(bool), typeof(bool), typeof(bool), typeof(bool)]),
    ];

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var isPlayer = AccessTools.Method(typeof(KarmaEligibilityPatch), nameof(IsPlayer));
        if (code.Any(c => c.Calls(isPlayer))) return code;
        var isPc = AccessTools.PropertyGetter(typeof(Card), nameof(Card.IsPC));
        var modKarma = AccessTools.Method(typeof(Player), nameof(Player.ModKarma));
        for (var i = 0; i < code.Count; i++) {
            if (!code[i].Calls(modKarma)) continue;
            // Scan back only as far as the nearest IsPC gate (with no other
            // karma call between). Actual installed IL is checked in the harness.
            for (var j = i - 1; j >= Math.Max(0, i - 80); j--) {
                if (code[j].Calls(modKarma)) break;
                if (!code[j].Calls(isPc)) continue;
                code[j].opcode = OpCodes.Call;
                code[j].operand = isPlayer;
                break;
            }
        }
        return code;
    }

    internal static bool IsPlayer(Card card) => card.IsPC ||
        NetSession.Instance.Connection is ElinNetHost && card is Chara c && PersonalKarma.IsHuman(c);
}

[HarmonyPatch(typeof(Player), nameof(Player.OnAdvanceDay))]
internal static class PersonalKarmaRecoveryPatch
{
    [HarmonyPostfix]
    internal static void After()
    {
        if (NetSession.Instance.Connection is not ElinNetHost host) return;
        foreach (var actor in host.ActiveRemoteCharas.Values)
            if (PersonalKarma.Get(actor) < 0 && EClass.rnd(4) == 0) PersonalKarma.Change(actor, 1);
    }
}

[HarmonyPatch(typeof(ConTransmuteBat), nameof(ConTransmuteBat.CheckSeen))]
internal static class KarmaBatEligibilityPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var pc = AccessTools.PropertyGetter(typeof(EClass), nameof(EClass.pc));
        foreach (var instruction in instructions) {
            if (!instruction.Calls(pc)) { yield return instruction; continue; }
            yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(KarmaBatEligibilityPatch), nameof(Player)));
        }
    }
    internal static Chara Player(ConTransmuteBat condition) =>
        NetSession.Instance.Connection is ElinNetHost && PersonalKarma.IsHuman(condition.owner)
            ? condition.owner : EClass.pc;
}
