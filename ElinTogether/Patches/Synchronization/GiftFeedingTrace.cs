using System.Collections.Generic;
using ElinTogether.Helper;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Gift availability is queried every UI frame. Record changed decisions only;
// eating and consumption are actual action boundaries, never polling.
[HarmonyPatch]
internal static class GiftFeedingTrace
{
    private static readonly Dictionary<(int Actor, int Item), string> Decisions = [];

    [ElinPreLoad]
    private static void Reset(GameIOContext _) => Decisions.Clear();

    private static bool Relevant(Chara actor) => actor.IsPlayer || actor.GetBool("remote_chara");

    private static string State(Chara actor) =>
        $"role={NetSession.Instance.Connection?.GetType().Name ?? "solo"} " +
        $"companion={NetSession.Instance.Connection is ElinNetHost host && host.IsCompanionControlled(actor)} " +
        $"hunger={actor.hunger.value} phase={actor.hunger.GetPhase()} " +
        $"slots={PlayerBackpackCapacity.Used(actor.things)}/{actor.things.GridSize} total={actor.things.Count}";

    [HarmonyPostfix, HarmonyPatch(typeof(Chara), nameof(Chara.CanAcceptGift))]
    private static void Acceptance(Chara __instance, Card t, bool __result)
    {
        if (!Relevant(__instance)) return;
        // Do not include the continuously changing hunger value in the UI cache.
        var decision = $"accepted={__result} slots={PlayerBackpackCapacity.Used(__instance.things)}/{__instance.things.GridSize}";
        var key = (__instance.uid, t.uid);
        if (Decisions.TryGetValue(key, out var previous) && previous == decision) return;
        if (Decisions.Count >= 64) Decisions.Clear();
        Decisions[key] = decision;
        EmpLog.Information("GiftTrace acceptance: recipient {Recipient}, item {Item}, decision {Decision}, state {State}",
            __instance.uid, t.uid, decision, State(__instance));
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Chara), nameof(Chara.IsValidGiftWeight))]
    private static void Weight(Chara __instance, Card t, bool __result)
    {
        if (!__result && Relevant(__instance))
            EmpLog.Information("GiftTrace weight rejected: recipient {Recipient}, item {Item}, weight {Weight}, limit {Limit}, state {State}",
                __instance.uid, t.uid, __instance.ChildrenWeight, __instance.WeightLimit, State(__instance));
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Chara), nameof(Chara.TryUse))]
    private static void Use(Chara __instance, Thing t, bool __result)
    {
        if (Relevant(__instance))
            EmpLog.Information("GiftTrace use: recipient {Recipient}, item {Item}, started {Started}, state {State}",
                __instance.uid, t.uid, __result, State(__instance));
    }

    [HarmonyPostfix, HarmonyPatch(typeof(FoodEffect), nameof(FoodEffect.Proc))]
    private static void Ate(Chara c, Thing food, bool consume)
    {
        if (Relevant(c))
            EmpLog.Information("GiftTrace food applied: recipient {Recipient}, item {Item}, consume {Consume}, state {State}",
                c.uid, food.uid, consume, State(c));
    }
}
