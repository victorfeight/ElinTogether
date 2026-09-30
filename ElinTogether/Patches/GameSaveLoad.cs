using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal class GameSaveLoad
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Game), nameof(Game.Save))]
    internal static bool OnSaveRemoteGame(bool isAutoSave, ref bool __result)
    {
        if (isAutoSave && EClass.debug.ignoreAutoSave) return true;
        if (NetSession.Instance.Connection is ElinNetHost host && !host.PreparePersonalProfilesForSave()) {
            __result = false;
            return false;
        }
        if (NetSession.Instance.IsHost) {
            return true;
        }

        __result = NetSession.Instance.Connection is ElinNetClient client && client.CheckpointPersonalProfile();
        EmpLog.Debug("Blocked saving game as client");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Game), nameof(Game.TryLoad))]
    internal static bool OnLoadRemoteGame()
    {
        if (NetSession.Instance.IsClient || EClass.game?.player?.chara is null) {
            return true;
        }

        // TODO: add full client reconnection
        EmpPop.Debug("Blocked loading game as host with active client connection");
        return false;
    }

    [ElinPreLoad]
    internal static void TerminateConnectionOnLoad(GameIOContext context)
    {
        NetSession.Instance.ResetSession();
    }

    [ElinPostSceneInit]
    internal static void TerminateConnectionOnLoad(Scene.Mode mode)
    {
        if (mode == Scene.Mode.Title) {
            NetSession.Instance.ResetSession();
        }
    }
}
