using System;
using System.Collections.Generic;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(InvOwnerBlend), nameof(InvOwnerBlend._OnProcess))]
internal static class BlendInputPatch
{
    [HarmonyPrefix]
    internal static bool Before(InvOwnerBlend __instance, Thing t)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client) return true;
        if (!ElinDelta.IsRemoteStateLanding && !PlayerControl.LocalInputBlocked &&
            __instance.owner is Thing { isDestroyed: false } potion && CardCache.Contains(potion)) {
            var target = PendingSplit.Split(t);
            if (!PendingUid.IsPending(target.Uid)) {
                client.Delta.AddRemote(new BlendRequestDelta {
                    Id = Guid.NewGuid(), ZoneUid = EClass._zone.uid, Potion = potion, Target = target,
                });
                EmpLog.Information("Blend requested: potion {Potion}, target {Target}, resumedDrag {Resumed}",
                    potion.uid, target.Uid, ThingRequest.IsReplayingIntent);
            }
        }
        return false;
    }
}

[HarmonyPatch(typeof(TraitDrink), nameof(TraitDrink.OnBlend))]
internal static class BlendOutcomePatch
{
    internal sealed class Capture
    {
        internal Capture? Previous;
        internal readonly HashSet<Thing> Items = [];
        internal required Card Potion;
        internal int Count;
    }
    private static Capture? _current;

    [HarmonyPrefix]
    internal static void Before(TraitDrink __instance, Thing t, out Capture? __state)
    {
        __state = null;
        if (NetSession.Instance.Connection is not ElinNetHost) return;
        __state = new() { Previous = _current, Potion = __instance.owner, Count = __instance.owner.Num };
        __state.Items.Add(t);
        _current = __state;
    }

    internal static void Split(Thing? item) { if (item is not null) _current?.Items.Add(item); }

    [HarmonyFinalizer]
    internal static void After(Capture? __state)
    {
        if (__state is null) return;
        _current = __state.Previous;
        if (NetSession.Instance.Connection is not ElinNetHost host) return;
        foreach (var item in __state.Items) {
            if (item.isDestroyed || !CardCache.Contains(item)) continue;
            var result = BlendResultDelta.Capture(item);
            if (CharaProgressCompleteEvent.ShouldPack(false)) CharaProgressCompleteEvent.Pack(result);
            else host.Delta.AddRemote(result);
        }
        EmpLog.Information("Blend resolved: potion {Potion}, count {Before}->{After}, targets {Targets}",
            __state.Potion.uid, __state.Count, __state.Potion.Num, __state.Items.Count);
    }
}

[HarmonyPatch(typeof(Card), nameof(Card.Split))]
internal static class BlendSplitPatch
{
    [HarmonyPostfix]
    internal static void After(Thing? __result) => BlendOutcomePatch.Split(__result);
}
