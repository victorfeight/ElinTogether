using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class PartyBoardPatch
{
    [HarmonyPostfix, HarmonyPatch(typeof(LayerPeople), nameof(LayerPeople.CreateParty))]
    internal static void Open(LayerPeople __result)
    {
        if (NetSession.Instance.Connection == null) return;
        if (__result.multi.owners.FirstOrDefault() is ListPeopleParty owner) PartyBoardManagement.OpenList = new(owner);
        PartyBoardManagement.Submit(PartyBoardOperation.Refresh);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(ListPeopleParty), nameof(ListPeopleParty.OnClick))]
    internal static bool Click(ListPeopleParty __instance, Chara c)
    {
        if (NetSession.Instance.Connection == null) return true;
        PartyBoardManagement.Submit(__instance.main ? PartyBoardOperation.Join : PartyBoardOperation.Dismiss, c);
        return false; // No speculative MoveZone, membership or MoveToOther UI edits.
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Party), nameof(Party.Disband))]
    internal static bool Disband(Party __instance)
    {
        if (NetSession.Instance.Connection == null || __instance != EClass.pc.party) return true;
        PartyBoardManagement.Submit(PartyBoardOperation.Disband);
        return false; // Protect humans from BOTH removal and the following MoveZone.
    }
}

// Hook the two native callbacks whose work isn't expressed by a single method.
// Matching call sites avoids depending on compiler-generated lambda numbers.
internal static class PartyBoardCallback
{
    internal static IEnumerable<MethodBase> Find(MethodInfo call, Type capture)
    {
        var matches = typeof(ListPeopleParty).GetNestedTypes(AccessTools.all)
            .SelectMany(t => t.GetMethods(AccessTools.all))
            .Where(m => m.GetMethodBody() != null && PatchProcessor.GetOriginalInstructions(m).Any(i => i.Calls(call))).ToArray();
        if (matches.Length != 1 || matches[0].DeclaringType!.GetFields(AccessTools.all).Count(f => f.FieldType == capture) != 1)
            throw new InvalidOperationException($"Party board callback changed: {call.Name}/{capture.Name}.");
        return matches;
    }

    internal static T Capture<T>(object closure) => (T)closure.GetType().GetFields(AccessTools.all)
        .Single(f => f.FieldType == typeof(T)).GetValue(closure)!;
}

[HarmonyPatch]
internal static class PartyBoardPresetPatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => PartyBoardCallback.Find(
        AccessTools.Method(typeof(ListPeopleParty), nameof(ListPeopleParty.JoinParty)), typeof(Player.PartySetup));

    [HarmonyPrefix]
    internal static bool Load(object __instance)
    {
        if (NetSession.Instance.Connection == null) return true;
        PartyBoardManagement.Submit(PartyBoardOperation.Load, setup: PartyBoardCallback.Capture<Player.PartySetup>(__instance));
        return false;
    }
}

[HarmonyPatch]
internal static class PartyBoardFavoritePatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => PartyBoardCallback.Find(
        AccessTools.PropertySetter(typeof(Card), nameof(Card.isFav)), typeof(Chara));

    [HarmonyPrefix]
    internal static bool Toggle(object __instance)
    {
        if (NetSession.Instance.Connection == null) return true;
        PartyBoardManagement.Submit(PartyBoardOperation.Favorite, PartyBoardCallback.Capture<Chara>(__instance));
        return false;
    }
}
