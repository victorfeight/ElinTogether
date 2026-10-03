using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(UIInventory), nameof(UIInventory.RefreshMenu))]
internal class InvRefreshMenuEvent
{
    [HarmonyPostfix]
    internal static void OnRefreshMenu(UIInventory __instance)
    {
        __instance.window.buttonSort.onClick.AddListener(() => {
            CoroutineHelper.Deferred(PropagateSaveData,
                () => !EMono.ui.contextMenu || !EMono.ui.contextMenu.currentMenu);
        });

        __instance.window.buttonShared.onClick.AddListener(PropagateSaveData);

        return;

        void PropagateSaveData()
        {
            if (ElinDelta.IsRemoteStateLanding ||
                NetSession.Instance.Connection is not { } connection ||
                __instance.owner.Container is not { isChara: false } container ||
                !ReferenceEquals(__instance.window.saveData, container.c_windowSaveData)) {
                return;
            }

            connection.Delta.AddRemote(new InvSaveDataDelta {
                Container = container,
                Data = LZ4Bytes.Create(__instance.window.saveData),
            });
        }
    }
}