using System;
using ElinTogether.Helper;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;
using UnityEngine;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class PersonalFaithPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Religion), nameof(Religion.JoinFaith))]
    private static bool Choose(Religion __instance, Chara c, Religion.ConvertType type)
    {
        if (NetSession.Instance.Connection is not ElinNetClient || !c.IsPC) return true;
        FaithTransactions.Submit(FaithAction.Convert, __instance.id, conversion: type);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InvOwnerOffering), nameof(InvOwnerOffering._OnProcess))]
    private static bool Offer(InvOwnerOffering __instance, Thing t)
    {
        if (NetSession.Instance.Connection is not ElinNetClient) return true;
        FaithTransactions.Submit(FaithAction.Offer, altar: __instance.altar.owner, item: t);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TraitAltar), nameof(TraitAltar.OnOffer))]
    private static void BeforeLocalOffering(TraitAltar __instance, Chara c, Thing t, out ScopeExit? __state) =>
        __state = FaithTransactions.BeginLocal(c, __instance, t);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TraitAltar), nameof(TraitAltar.OnOffer))]
    private static void AfterLocalOffering(ScopeExit? __state) => __state?.Dispose();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TraitAltar), nameof(TraitAltar._OnOffer))]
    private static bool AwardOffering(TraitAltar __instance, Chara c, Thing t, int takeoverMod)
    {
        if (FaithTransactions.Actor != c || NetSession.Instance.Connection is not ElinNetHost || !c.IsRemotePlayer) return true;
        var value = __instance.Deity.GetOfferingValue(t, t.Num);
        value = value * (EClass.debug.enable ? 1000 : c.HasElement(1228) ? 130 : 100) / 100;
        Msg.Say(takeoverMod != 0 || value >= 200 ? "god_offer1" : value >= 100 ? "god_offer2" : value >= 50 ? "god_offer3" : "god_offer4", t);
        if (takeoverMod == 0 && value >= 200) c.faith.Talk("offer");
        if (takeoverMod != 0) value += __instance.Deity.GetOfferingValue(t, 1) * takeoverMod;
        if (value > 0) {
            var award = OwnerProgressionAwards.Record(c, Guid.NewGuid(), 85, value);
            var pending = OwnerProgressionAwards.Read(c);
            var stored = pending.Find(a => a.Id == award.Id)!;
            stored.Offering = true;
            stored.Contraband = t.GetBool(115);
            OwnerProgressionAwards.Save(c, pending);
        }
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ElementContainer), nameof(ElementContainer.ModExp))]
    private static bool AwardPrayer(ElementContainer __instance, int ele, float a, bool chain)
    {
        if (FaithTransactions.Actor is null || NetSession.Instance.Connection is not ElinNetHost ||
            __instance.Card is not Chara { IsRemotePlayer: true } actor || a <= 0 || chain) return true;
        OwnerProgressionAwards.Record(actor, Guid.NewGuid(), ele, (int)a);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Game), nameof(Game.Save))]
    private static void Save() => PersonalFaith.CaptureLocal();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.OnAdvanceDay))]
    private static void AdvanceDay()
    {
        if (NetSession.Instance.Connection is ElinNetHost host) {
            PersonalFaith.CaptureLocal();
            foreach (var actor in host.ActiveRemoteCharas.Values) PersonalFaith.AdvanceActive(actor);
        } else if (NetSession.Instance.IsClient) PersonalFaith.RestoreDay();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.OnAdvanceHour))]
    private static void AdvanceHour()
    {
        if (NetSession.Instance.Connection is ElinNetHost host)
            foreach (var actor in host.ActiveRemoteCharas.Values) PersonalFaith.AdvanceHour(actor);
        else if (NetSession.Instance.IsClient) PersonalFaith.RestoreJoinedPlayer();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ElementContainerFaction), nameof(ElementContainerFaction.OnJoinFaith))]
    [HarmonyPatch(typeof(ElementContainerFaction), nameof(ElementContainerFaction.OnLeaveFaith))]
    private static bool SharedFactionFaith()
    {
        // These callbacks reprice every faction member's global artifact bonuses
        // using pc.faith. A personal conversion must not change the host-led faction.
        return FaithTransactions.Actor is not { IsRemotePlayer: true };
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.GetPietyValue))]
    private static void PlayerPiety(Chara __instance, ref int __result)
    {
        // The vanilla _IsPC save flag is false on the host's remote character.
        // Both humans need the player formula, not the NPC level-based formula.
        if (NetSession.Instance.Connection is not null && __instance.IsPlayer)
            __result = 10 + (int)(Mathf.Sqrt(__instance.c_daysWithGod) * 2f + __instance.Evalue(85)) / 2;
    }
}
