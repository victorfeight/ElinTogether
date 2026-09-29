using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal class PauseGame
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AM_Adv), nameof(AM_Adv.ShouldPauseGame), MethodType.Getter)]
    internal static void OnGetShouldPauseGame(ref bool __result)
    {
        if (ActionModeCombat.Paused) {
            __result = true;
            return;
        }

        if (ActionModeCombat.Phase is ActionModeCombat.CombatPhase.Executing or ActionModeCombat.CombatPhase.Advancing) {
            // Dispatch or elapsed-time settlement must run even when the host is not due.
            __result = false;
            return;
        }

        if (!__result) {
            return;
        }

        // The host's reconstructed child can be empty between a client's steps.
        // Keep time moving while that connected player still reports a live goal.
        if (NetSession.Instance.Connection is ElinNetHost host) {
            __result = !host.ActiveRemoteCharas.Any(pair => PlayerActivity.IsBusy(pair.Value,
                host.States.TryGetValue(pair.Key, out var state) ? state.LastAct : 0));
        } else {
            __result = !EClass.pc.party.members
                .Any(c => c.IsRemotePlayer && PlayerActivity.IsBusy(c));
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UI), nameof(UI.IsPauseGame), MethodType.Getter)]
    public static void GetIsPauseGame(UI __instance, ref bool __result)
    {
        if (NetSession.Instance.HasActiveConnection) {
            // Menus must not stop other players, but Game.Pause is a mandatory
            // world pause. Quest timers otherwise keep queuing return callbacks
            // while character ticks are already stopped by Game.isPaused.
            __result = Game.isPaused;
        }
    }
}
