using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

// Both native land expansion and No Tent Restrictions use this method.
[HarmonyPatch(typeof(MapBounds), nameof(MapBounds.Expand))]
internal static class MapBoundsSyncPatch
{
    [HarmonyPostfix]
    internal static void After(MapBounds __instance)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host ||
            EClass.game?.isLoading != false || EClass.game.activeZone?.map is not { } map ||
            !ReferenceEquals(map.bounds, __instance)) return;
        host.Delta.AddRemote(new MapBoundsDelta {
            ZoneUid = EClass.game.activeZone.uid, MapSize = map.Size,
            X = __instance.x, Z = __instance.z, MaxX = __instance.maxX, MaxZ = __instance.maxZ,
        });
    }
}
