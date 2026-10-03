using System.Collections.Generic;
using System.Reflection.Emit;
using ElinTogether.Helper;
using ElinTogether.Models;
using ElinTogether.Net;
using EModding.Helper;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Chara), nameof(Chara.Pick))]
internal static class CharaPickThingEvent
{
    [HarmonyPrefix]
    [HarmonyBefore("AutoActAllyExpansion")]
    internal static bool OnCharaPickThingy(Chara __instance, Thing t, ref Thing __result)
    {
        if (NetSession.Instance.Connection is not { } connection) {
            return true;
        }

        if (SkipClientReplayPickup(__instance, t) || (connection.IsClient && !CardCache.Contains(t))) {
            // pick self without returning null
            __result = t;
            return false;
        }

        if (CharaProgressCompleteEvent.ShouldPack(true)) {
            DeferPickup(t, null, CharaPickThingDelta.PickType.Pick);

            __result = t;
            return false;
        }

        // Ordinary pickup is already synchronized by AddThing, TryStackTo and
        // Zone.AddCard. Publishing an attempt here also published rejected pickups.
        return true;
    }

    internal static bool SkipClientReplayPickup(Chara? actor, Thing item)
    {
        // Completion replay is presentation of a host decision, not a new pickup.
        // Run before Ally Expansion can redirect the remote actor to EClass.pc.
        // Explicit product handoffs use Simulate(), so remain ordinary local input.
        if (NetSession.Instance.Connection is not ElinNetClient ||
            !ElinDelta.IsRemoteStateLanding || !CharaProgressCompleteDelta.IsReplaying) return false;
        EmpLog.Debug("PickupTrace suppressed completion replay pickup: actor {ActorUid}, item {ItemUid}, parent {ParentUid}",
            actor?.uid, item.uid, (item.parent as Card)?.uid);
        return true;
    }

    internal static void DeferPickup(Thing thing, Point? pos, CharaPickThingDelta.PickType type)
    {
        var owner = CharaProgressCompleteEvent.Chara!;
        // A deferred product must already exist on the host. This preserves loot
        // on rejection/disconnect and lets shared-card collection validate it.
        // Existing zone hooks publish this placement before the pickup handoff.
        if (thing.parent is null) EClass._zone.AddCard(thing, pos ?? owner.pos);
        CharaProgressCompleteEvent.Pack(new CharaPickThingDelta {
            Owner = owner, Thing = thing, Pos = pos, Type = type,
        });
        CardCache.KeepAlive(thing);
    }
}

[HarmonyPatch(typeof(Chara), nameof(Chara.TryPickGroundItem))]
internal static class CharaTryPickGroundItemEvent
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> OnTryPickGroundItem(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .End()
            .MatchStartBackwards(
                new OperandContains(OpCodes.Callvirt, nameof(Card.IsPC)))
            .EnsureValid("Chara.TryPickGroundItem npc property")
            .SetInstructionAndAdvance(
                Transpilers.EmitDelegate((Chara chara) => chara.IsPlayer))
            .InstructionEnumeration();
    }
}

[HarmonyPatch(typeof(Chara), nameof(Chara.PickOrDrop), typeof(Point), typeof(Thing), typeof(bool))]
internal static class CharaPickOrDropEvent
{
    [HarmonyPrefix]
    [HarmonyBefore("AutoActAllyExpansion")]
    internal static bool OnCharaPickOrDrop(Chara __instance, Point p, Thing t)
    {
        if (NetSession.Instance.Connection is not { } connection) {
            return true;
        }

        if (CharaPickThingEvent.SkipClientReplayPickup(__instance, t) || (connection.IsClient && !CardCache.Contains(t))) {
            return false;
        }

        if (!CharaProgressCompleteEvent.ShouldPack(true)) {
            return true;
        }

        CharaPickThingEvent.DeferPickup(t, p, CharaPickThingDelta.PickType.PickOrDrop);

        return false;
    }
}

[HarmonyPatch(typeof(Map), nameof(Map.TrySmoothPick), typeof(Point), typeof(Thing), typeof(Chara))]
internal static class CharaTrySmoothPickEvent
{
    [HarmonyPrefix]
    [HarmonyBefore("AutoActAllyExpansion")]
    internal static bool OnTrySmoothPick(Point p, Thing t, Chara c)
    {
        if (NetSession.Instance.Connection is not { } connection) {
            return true;
        }

        if (CharaPickThingEvent.SkipClientReplayPickup(c, t) || (connection.IsClient && !CardCache.Contains(t))) {
            return false;
        }

        if (!CharaProgressCompleteEvent.ShouldPack(true)) {
            return true;
        }

        CharaPickThingEvent.DeferPickup(t, p, CharaPickThingDelta.PickType.TrySmoothPick);

        return false;
    }
}
