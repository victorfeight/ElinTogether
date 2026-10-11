using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Helper;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(DramaCustomSequence), nameof(DramaCustomSequence.Build))]
internal static class NpcNewsDialoguePatch
{
    [HarmonyPostfix]
    internal static void After(DramaCustomSequence __instance, Chara c)
    {
        if (NetSession.Instance.Connection is null) return;
        var events = __instance.events;
        for (var i = 0; i + 1 < events.Count; i++) {
            if (events[i].step != "_news" || events[i + 1] is not DramaEventMethod method) continue;
            method.action = () => {
                __instance._TempTalk("tg", "Waiting for information...", __instance.StepDefault);
                var waiting = __instance.manager.lastTalk;
                NpcNews.Submit(c, result => {
                    if (LayerDrama.Instance is not { } layer || !layer || layer.drama != __instance.manager ||
                        __instance.sequence.isExited || __instance.manager.lastTalk != waiting ||
                        EClass._zone.uid != result.ZoneUid || !c.IsInActiveMap) return;
                    if (!result.Accepted) { __instance.TempGoto(__instance.StepDefault); return; }
                    GameLang.refDrama1 = result.SiteName ?? "";
                    __instance.sequence.tempEvents.Clear();
                    __instance._TempTalk("tg", __instance.GetTopic(c, result.Site is null ? "news2" : "news1"), null);
                });
            };
        }
    }
}
