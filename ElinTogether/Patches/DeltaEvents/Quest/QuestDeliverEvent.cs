using System.Collections.Generic;
using System.Reflection;
using ElinTogether.Helper;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class QuestDeliverEvent
{
    internal static IEnumerable<MethodBase> TargetMethods() =>
        OverrideMethodComparer.FindAllOverrides(typeof(Quest), nameof(Quest.Deliver), typeof(Chara), typeof(Thing));

    [HarmonyPrefix]
    internal static bool Before(Quest __instance, Chara c, Thing? t, ref bool __result)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client) return true;
        __result = false;
        // Delivery is an intent, never a consuming replay of a host result.
        if (ElinDelta.IsApplying) return false;
        if (__instance is not QuestDeliver quest || !SharedQuests.CanClientAccept(quest)) {
            EmpPop.Information("emp_ui_quest_client".lang()); return false;
        }
        var item = t ?? quest.GetDestThing();
        if (item == null || quest.IsExpired || !EClass.game.quests.list.Contains(quest)) return false;
        client.Delta.AddRemote(new QuestDeliverDelta {
            Uid = quest.uid, ZoneUid = EClass._zone.uid, Recipient = c, Item = item,
        });
        EmpLog.Information("Requesting quest delivery: quest {Quest}, recipient {Recipient}, item {Item}", quest.uid, c.uid, item.uid);
        return false;
    }
}
