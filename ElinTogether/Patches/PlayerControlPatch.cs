using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class PlayerControlPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(Chara), nameof(Chara.Tick))]
    private static bool Tick(Chara __instance)
    {
        if (__instance != EClass.pc) return true;
        if (NetSession.Instance.Connection is ElinNetClient client)
            return !(client.ControlInputBlocked || client.ZoneTransitionPending) || ElinDelta.IsApplying;
        if (NetSession.Instance.Connection is ElinNetHost host && host.IsCompanionControlled(__instance) &&
            !__instance.isDead) {
            __instance.FindNewEnemy();
            if (__instance.enemy is { IsAliveInCurrentZone: true } && __instance.ai is not GoalCombat)
                __instance.SetAI(new GoalCombat());
            else if (__instance.HasNoGoal || !__instance.ai.IsRunning) ChooseHostGoal();
        }
        return true;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Chara), nameof(Chara.ChooseNewGoal))]
    private static bool ChooseGoal(Chara __instance)
    {
        if (__instance != EClass.pc || NetSession.Instance.Connection is not ElinNetHost host ||
            !host.IsCompanionControlled(__instance)) return true;
        ChooseHostGoal();
        return false;
    }

    internal static void ChooseHostGoal()
    {
        var actor = EClass.pc;
        if (actor.isDead) return;
        actor.FindNewEnemy();
        if (actor.enemy is { IsAliveInCurrentZone: true }) { actor.SetAI(new GoalCombat()); return; }
        var host = (ElinNetHost)NetSession.Instance.Connection!;
        var leader = host.ActiveRemoteCharas.FirstOrDefault(p => host.AcceptsPlayerInput(p.Key) &&
            !p.Value.isDead && p.Value.currentZone == actor.currentZone).Value;
        actor.SetAI(leader is not null && actor.Dist(leader) > 3
            ? new AI_Goto(leader, 3) : new GoalIdle());
    }
}

[HarmonyPatch]
internal static class PlayerControlInputPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(AM_Adv), nameof(AM_Adv._OnUpdateInput));
        yield return AccessTools.Method(typeof(ButtonAbility), nameof(ButtonAbility.TryUse));
        yield return AccessTools.Method(typeof(ActPlan.Item), nameof(ActPlan.Item.Perform));
        yield return AccessTools.Method(typeof(InvOwner.Transaction), nameof(InvOwner.Transaction.Process));
        foreach (var type in typeof(InvOwner).Assembly.GetTypes().Where(t => typeof(InvOwner).IsAssignableFrom(t)))
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            if (!method.IsAbstract && method.Name is "OnClick" or "OnRightClick" or "OnDrag" or "OnProcess") yield return method;
    }

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool AllowInput() => !PlayerControl.LocalInputBlocked || ElinDelta.IsApplying;
}
