using System.Linq;
using ElinTogether.Net;
using Object = UnityEngine.Object;

namespace ElinTogether.Models;

internal static class MusicCollectionSync
{
    internal static int[] Capture() => EClass.player.knownBGMs.ToArray();

    internal static void Apply(int[]? tracks)
    {
        if (tracks is null || NetSession.Instance.Connection is not ElinNetClient) return;
        var known = EClass.player.knownBGMs;
        if (known.SetEquals(tracks)) return;
        // Copy the host's collection, not TraitTape.OnUse: replaying use here
        // would consume an item and repeat its messages/sound. Keep the set object.
        known.Clear();
        known.UnionWith(tracks);
        foreach (var view in Object.FindObjectsOfType<LayerEditPlaylist>()) view.Refresh();
        EmpLog.Debug("Music collection reconciled: {TrackCount} tracks", known.Count);
    }
}
