using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class ReserveManagementPatch
{
    [HarmonyPostfix, HarmonyPatch(typeof(BaseListPeople), nameof(BaseListPeople.OnClick))]
    internal static void OpenResidentMenu(BaseListPeople __instance)
    {
        if (NetSession.Instance.Connection != null) ReserveManagement.OpenResidents = new(__instance);
    }
    [HarmonyPostfix, HarmonyPatch(typeof(LayerPeople), nameof(LayerPeople.CreateReserve))]
    internal static void Open(LayerPeople __result)
    {
        if (NetSession.Instance.Connection is not ElinNetClient) return;
        if (__result.multi.owners.FirstOrDefault() is ListPeopleCallReserve owner)
            ReserveManagement.OpenList = new(owner);
        ReserveManagement.Submit(null, ReserveOperation.Refresh);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(ListPeopleCallReserve), nameof(ListPeopleCallReserve.OnClick))]
    internal static bool Recruit(Chara c)
    {
        if (NetSession.Instance.Connection is not ElinNetClient) return true;
        ReserveManagement.Submit(c, ReserveOperation.Recruit);
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Faction), nameof(Faction.RemoveReserve))]
    internal static void BeforeRemove(Faction __instance, Chara c, out bool __state) =>
        __state = __instance == EClass.Home && __instance.listReserve.Any(i => i.chara.uid == c.uid);

    [HarmonyPostfix, HarmonyPatch(typeof(Faction), nameof(Faction.RemoveReserve))]
    internal static void Removed(Chara c, bool __state) { if (__state) ReserveManagement.Changed(c); }

    [HarmonyPostfix, HarmonyPatch(typeof(Faction), nameof(Faction.AddReserve))]
    internal static void Added(Faction __instance, Chara c)
    {
        if (__instance == EClass.Home) ReserveManagement.Changed(c);
    }
}

// Replace only the reserve dialog's confirmed release callback. Retain vanilla
// Yes/No/Yes-and-skip UI, and never intercept unrelated RemoveReserve callers.
[HarmonyPatch]
internal static class ReserveReleasePatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
        => ReserveNativeCallback.Find(typeof(ListPeopleCallReserve), nameof(Faction.RemoveReserve));

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var remove = AccessTools.Method(typeof(Faction), nameof(Faction.RemoveReserve));
        var banish = AccessTools.Method(typeof(Chara), nameof(Chara.OnBanish));
        var result = instructions.ToList();
        var removed = 0; var banished = 0;
        foreach (var code in result) {
            if (code.Calls(remove)) { code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(ReserveReleasePatch), nameof(Remove)); removed++; }
            else if (code.Calls(banish)) { code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(ReserveReleasePatch), nameof(Banish)); banished++; }
        }
        if (removed != 1 || banished != 1) throw new InvalidOperationException("Reserve release native flow changed.");
        return result;
    }

    internal static void Remove(Faction faction, Chara c)
    {
        if (NetSession.Instance.Connection is ElinNetClient) ReserveManagement.Submit(c, ReserveOperation.Release);
        else faction.RemoveReserve(c);
    }

    internal static void Banish(Chara c)
    {
        if (NetSession.Instance.Connection is not ElinNetClient) c.OnBanish();
    }
}

internal static class ReserveNativeCallback
{
    internal static IEnumerable<MethodBase> Find(Type owner, string method)
    {
        var call = AccessTools.Method(typeof(Faction), method);
        var methods = owner.GetNestedTypes(AccessTools.all).SelectMany(t => t.GetMethods(AccessTools.all))
            .Where(m => m.GetMethodBody() != null && PatchProcessor.GetOriginalInstructions(m).Any(i => i.Calls(call))).ToArray();
        if (methods.Length != 1) throw new InvalidOperationException($"Reserve callback changed: {owner.Name}/{method}, expected one native callback.");
        return methods;
    }
}

// Only the resident-menu action becomes a request. Gacha generation and other
// native AddReserve callers retain their existing behavior.
[HarmonyPatch]
internal static class ReserveStorePatch
{
    internal static IEnumerable<MethodBase> TargetMethods()
        => ReserveNativeCallback.Find(typeof(BaseListPeople), nameof(Faction.AddReserve));

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var call = AccessTools.Method(typeof(Faction), nameof(Faction.AddReserve));
        var result = instructions.ToList();
        var count = 0;
        foreach (var code in result) {
            if (!code.Calls(call)) continue;
            code.opcode = OpCodes.Call;
            code.operand = AccessTools.Method(typeof(ReserveStorePatch), nameof(Store));
            count++;
        }
        if (count != 1) throw new InvalidOperationException("Native send-to-reserve flow changed.");
        return result;
    }

    internal static void Store(Faction faction, Chara c)
    {
        if (NetSession.Instance.Connection is ElinNetClient) ReserveManagement.Submit(c, ReserveOperation.Store);
        else faction.AddReserve(c);
    }
}
