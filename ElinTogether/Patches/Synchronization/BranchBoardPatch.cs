using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

internal static class BranchBoardUI
{
    internal static WeakReference<LayerHome>? Home;
    internal static WeakReference<LayerTech>? Tech;
    internal static WeakReference<LayerPolicy>? Policy;
    internal static WeakReference<LayerQuestBoard>? Hire;
    internal static void RefreshBuildMenu()
    {
        EClass.player.hotbars.ResetHotbar(2);
        if (WidgetHotbar.HotBarMainMenu) WidgetHotbar.HotBarMainMenu.RebuildPage();
        if (WidgetMenuPanel.Instance) WidgetMenuPanel.Instance.OnChangeActionMode();
    }
    internal static void Refresh()
    {
        if (Home?.TryGetTarget(out var h) == true && h) h.RefreshFeat();
        if (Tech?.TryGetTarget(out var t) == true && t) t.RefreshTech();
        if (Policy?.TryGetTarget(out var p) == true && p) p.RefreshPolicy();
        if (Hire?.TryGetTarget(out var q) == true && q) q.RefreshHire();
    }
}

[HarmonyPatch]
internal static class BranchBoardOpenPatch
{
    internal static IEnumerable<MethodBase> TargetMethods() => new[] { typeof(LayerHome), typeof(LayerTech), typeof(LayerPolicy), typeof(LayerQuestBoard) }
        .Select(t => AccessTools.Method(t, "OnInit"));
    [HarmonyPostfix] internal static void Open(object __instance)
    {
        if (NetSession.Instance.Connection == null || !EClass._zone.IsPCFaction) return;
        switch (__instance) {
            case LayerHome h: BranchBoardUI.Home = new(h); break;
            case LayerTech t: BranchBoardUI.Tech = new(t); break;
            case LayerPolicy p: BranchBoardUI.Policy = new(p); break;
            case LayerQuestBoard q: BranchBoardUI.Hire = new(q); break;
        }
        BranchBoardManagement.Submit(new() { Operation = BranchBoardOperation.Refresh });
    }
}

// Intercept the complete confirmed native callback, before its payment. Match
// operation call sites, never compiler-generated lambda names or translated text.
[HarmonyPatch]
internal static class BranchBoardCallbackPatch
{
    private static readonly Dictionary<MethodBase, Type> Roots = [];
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in new[] { typeof(LayerHome), typeof(ItemResearch), typeof(LayerPolicy) }) {
            var matches = type.GetNestedTypes(AccessTools.all).SelectMany(t => t.GetMethods(AccessTools.all | BindingFlags.DeclaredOnly))
                .Where(m => m.GetMethodBody() != null && PatchProcessor.GetOriginalInstructions(m).Any(i => type == typeof(LayerPolicy)
                    ? i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.DeclaringType == typeof(Policy) && f.Name == "active"
                    : i.operand is MethodInfo call && call.Name == (type == typeof(LayerHome) ? "ModBase" : "CompletePlan"))).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException($"Settlement board callback changed: {type.Name}");
            Roots[matches[0]] = type;
            yield return matches[0];
        }
    }

    private static T Capture<T>(object closure, Type root)
    {
        IEnumerable<T> Walk(object value, HashSet<object> seen) {
            if (!seen.Add(value)) yield break;
            foreach (var field in value.GetType().GetFields(AccessTools.all).Where(f => !f.IsStatic)) {
                var child = field.GetValue(value);
                if (child is T target) yield return target;
                else if (child != null && field.FieldType.DeclaringType == root)
                    foreach (var match in Walk(child, seen)) yield return match;
            }
        }
        return Walk(closure, []).Single();
    }

    [HarmonyPrefix] internal static bool Click(object __instance, MethodBase __originalMethod)
    {
        if (NetSession.Instance.Connection == null) return true;
        var root = Roots[__originalMethod];
        BranchBoardCommand request;
        if (root == typeof(LayerHome)) {
            var e = Capture<Element>(__instance, root);
            request = new() { Operation = BranchBoardOperation.Upgrade, Target = e.id,
                ZoneUid = Capture<LayerHome>(__instance, root).branch.owner.uid,
                Expected = e.ValueWithoutLink, Price = EClass.Branch.GetTechUpgradeCost(e) };
        } else if (root == typeof(ItemResearch)) {
            var p = Capture<ResearchPlan>(__instance, root);
            request = new() { Operation = BranchBoardOperation.Research, ZoneUid = p.branch.owner.uid, Plan = p.id, Expected = p.rank, Price = p.source.tech };
        } else {
            var p = Capture<Policy>(__instance, root);
            request = new() { Operation = BranchBoardOperation.Policy, ZoneUid = p.branch.owner.uid, Target = p.id, Active = !p.active };
        }
        BranchBoardManagement.Submit(request);
        return false;
    }
}

[HarmonyPatch]
internal static class BranchBoardMutationPatch
{
    [HarmonyPostfix, HarmonyPatch(typeof(FactionBranch), nameof(FactionBranch.UpdateReqruits))]
    internal static void Candidates(FactionBranch __instance) => BranchBoardManagement.Publish(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(FactionBranch), nameof(FactionBranch.Recruit))]
    internal static void Recruited(FactionBranch __instance, Chara c)
    {
        if (NetSession.Instance.Connection is not ElinNetHost) return;
        ReserveManagement.Changed(c);
        BranchBoardManagement.Publish(__instance);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.CompletePlan))]
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.AddPlan), typeof(ResearchPlan))]
    internal static void Research(ResearchManager __instance) => BranchBoardManagement.Publish(__instance.branch);

    [HarmonyPostfix, HarmonyPatch(typeof(PolicyManager), nameof(PolicyManager.RefreshEffects))]
    internal static void Policy(PolicyManager __instance) => BranchBoardManagement.Publish(__instance.owner);
}

[HarmonyPatch]
internal static class BranchHirePatch
{
    [HarmonyPrefix, HarmonyPatch(typeof(FactionBranch), nameof(FactionBranch.UpdateReqruits))]
    internal static bool Generate() => !NetSession.Instance.IsClient;

    [HarmonyPrefix, HarmonyPatch(typeof(DramaOutcome), nameof(DramaOutcome.chara_hired))]
    [HarmonyPatch(typeof(DramaOutcome), nameof(DramaOutcome.chara_hired_ticket))]
    internal static bool Hire(DramaOutcome __instance, MethodBase __originalMethod)
    {
        if (NetSession.Instance.Connection == null) return true;
        // Non-board free invitations have separate native eligibility and routing.
        if (NetSession.Instance.IsHost && !EClass.Branch.IsRecruit(__instance.cc)) return true;
        BranchBoardManagement.Submit(new() {
            Operation = __originalMethod.Name == nameof(DramaOutcome.chara_hired_ticket) ? BranchBoardOperation.HireTicket : BranchBoardOperation.Hire,
            Target = __instance.cc.uid, Price = CalcGold.Hire(__instance.cc),
        });
        return false;
    }
}
