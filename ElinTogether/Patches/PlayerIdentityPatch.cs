using ElinTogether.Models;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(Chara), nameof(Chara.IsPC), MethodType.Getter)]
internal static class PlayerIdentityPatch
{
    [HarmonyPrefix]
    internal static bool Resolve(Chara __instance, ref bool __result)
    {
        // Save-probe deserialization invokes faith callbacks before core.game is
        // installed. Vanilla dereferences core.game.player here; there is no local
        // player to match during that interval. Preserve its reference comparison
        // once the game exists, except while replaying the watched character.
        var local = EClass.core?.game?.player?.chara;
        __result = ReferenceEquals(__instance, local) &&
            !(PlayerControl.WatchingOwnCharacter && ElinDelta.IsApplying);
        return false;
    }
}
