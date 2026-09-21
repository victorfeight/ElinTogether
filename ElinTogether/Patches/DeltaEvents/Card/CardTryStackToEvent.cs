using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Card), nameof(Card.TryStackTo))]
internal static class CardTryStackEvent
{
    [HarmonyPrefix]
    internal static bool OnCardTryStackTo(Card __instance, Thing to, ref bool __result)
    {
        if (NetSession.Instance.IsHost && RemoteCraft.ProductReceiver is { } receiver) {
            EmpLog.Debug("Remote craft stack attempt product {ProductUid} ({ProductId}) num {ProductNum}, target {TargetUid} ({TargetId}) num {TargetNum}, target root {TargetRootUid}, receiver {ReceiverUid}, compatible {CanStack}",
                __instance.uid, __instance.id, __instance.Num, to.uid, to.id, to.Num,
                to.GetRootCard()?.uid ?? -1, receiver.uid, __instance.CanStackTo(to));
        }

        if (NetSession.Instance.Connection is not ElinNetClient client) {
            return true;
        }

        if (PendingSplit.Resolve(__instance.uid) == to.uid) {
            return true;
        }

        if (to.IsHostOwned != __instance.IsHostOwned) {
            return false;
        }

        if (!to.IsHostOwned) {
            return true;
        }

        if (!__instance.CanStackTo(to)) {
            return false;
        }

        if (ElinDelta.IsApplying) {
            return true;
        }

        __result = true;
        client.Delta.AddRemote(new CardTryStackToDelta {
            Card = __instance,
            To = to,
            Parent = to.parent as Card,
        });

        return false;
    }

    [HarmonyPostfix]
    internal static void OnCardTryStackToEnd(Card __instance, Thing to, bool __result)
    {
        if (NetSession.Instance.IsHost && RemoteCraft.ProductReceiver is { } receiver) {
            EmpLog.Debug("Remote craft stack result product {ProductUid} destroyed {ProductDestroyed}, target {TargetUid} num {TargetNum}, target root {TargetRootUid}, receiver {ReceiverUid}, merged {Merged}",
                __instance.uid, __instance.isDestroyed, to.uid, to.Num,
                to.GetRootCard()?.uid ?? -1, receiver.uid, __result);
        }
    }
}