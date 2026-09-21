using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Card), nameof(Card.ModNum))]
internal static class CardModNumEvent
{
    [HarmonyPrefix]
    internal static bool OnCardModNum(Card __instance, ref int a)
    {
        if (CardModNumDelta.IsOverwriting || !__instance.IsResolved) {
            return true;
        }

        a = 0;
        return false;
    }

    [HarmonyPostfix]
    internal static void OnCardModNumEnd(Card __instance, int a)
    {
        if (NetSession.Instance.Connection is not { } connection || a == 0 || CardModNumDelta.IsOverwriting) {
            return;
        }

        if (!CardCache.Contains(__instance)) {
            if (connection.IsHost && RemoteCraft.ProductReceiver is { } uncachedReceiver) {
                EmpLog.Debug("Remote craft stack count not queued: card {CardUid} ({CardId}) num {CardNum}, root {RootUid}, receiver {ReceiverUid}, card uncached",
                    __instance.uid, __instance.id, __instance.Num,
                    __instance.GetRootCard()?.uid ?? -1, uncachedReceiver.uid);
            }
            return;
        }

        if (connection.IsClient && PendingUid.IsPending(__instance.uid)) {
            return;
        }

        var delta = new CardModNumDelta {
            Card = __instance,
            Num = __instance.Num,
        };

        if (connection.IsHost && RemoteCraft.ProductReceiver is { } receiver) {
            EmpLog.Debug("Remote craft stack count changed card {CardUid} ({CardId}) to {CardNum}, root {RootUid}, receiver {ReceiverUid}, packed {Packed}",
                __instance.uid, __instance.id, __instance.Num,
                __instance.GetRootCard()?.uid ?? -1, receiver.uid,
                CharaProgressCompleteEvent.ShouldPack(false));
        }

        if (CharaProgressCompleteEvent.ShouldPack(false)) {
            CharaProgressCompleteEvent.Pack(delta);
            return;
        }

        connection.Delta.AddRemote(delta);
    }
}