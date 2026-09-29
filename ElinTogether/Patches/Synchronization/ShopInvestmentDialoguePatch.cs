using ElinTogether.Models;
using ElinTogether.Helper;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(DramaCustomSequence), nameof(DramaCustomSequence.Build))]
internal static class ShopInvestmentDialoguePatch
{
    [HarmonyPostfix]
    internal static void After(DramaCustomSequence __instance, Chara c)
    {
        if (NetSession.Instance.Connection is not ElinNetClient) return;
        var events = __instance.events;
        for (var i = 0; i + 1 < events.Count; i++) {
            if (events[i].step != "_investShop" || events[i + 1] is not DramaEventMethod method) continue;
            var original = method.action;
            method.action = () => {
                original(); // Native quote, translated topics, cancel and choices.
                var choices = __instance.manager.lastTalk.choices;
                var level = c.c_invest;
                var valid = int.TryParse(GameLang.refDrama1, out var price) && choices.Count == 3 &&
                    choices[0].text == "yes".lang() && choices[2].text == "quickInvest".lang();
                if (!valid) {
                    // Never fall back to spending locally if the native UI changes.
                    foreach (var choice in choices) {
                        if (choice.onJump is not null) choice.onJump = () => __instance.TempGoto(__instance.StepDefault);
                    }
                    EmpLog.Warning("Shop investment dialogue shape changed; client investment disabled");
                    return;
                }
                var submitted = false;
                void Submit(bool quick) {
                    if (submitted) return;
                    submitted = true;
                    DramaEventTalk? waiting = null;
                    ShopInvestment.Submit(c, level, price, result => {
                        if (LayerDrama.Instance is not { } layer || !layer || layer.drama != __instance.manager ||
                            __instance.sequence.isExited || __instance.manager.lastTalk != waiting ||
                            EClass._zone.uid != result.ZoneUid || !c.IsInActiveMap) return;
                        if (quick && result.Accepted) __instance.TempGoto("_investShop");
                        else __instance.TempGoto(__instance.StepDefault);
                    });
                    __instance._TempTalk("tg", "Waiting for the shopkeeper...", __instance.StepDefault);
                    waiting = __instance.manager.lastTalk;
                }
                choices[0].onJump = () => Submit(false);
                choices[2].onJump = () => Submit(true);
            };
            return;
        }
    }
}
