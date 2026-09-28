using ElinTogether.Models;
using ElinTogether.Net;

int checks = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); checks++; }
EClass.player.knownBGMs.UnionWith([1, 3, 110]);
var captured = MusicCollectionSync.Capture();
EClass.player.knownBGMs.Add(124);
Check(captured.ToHashSet().SetEquals([1, 3, 110]), "capture owns its data while host collection changes");

var menu = new LayerEditPlaylist();
UnityEngine.Object.Views = [menu];
NetSession.Instance.Connection = new ElinNetClient();
EClass.player = new();
var originalSet = EClass.player.knownBGMs;
originalSet.UnionWith([1, 3, 999]);
MusicCollectionSync.Apply(captured);
Check(ReferenceEquals(originalSet, EClass.player.knownBGMs) && originalSet.SetEquals([1, 3, 110]) && menu.Refreshes == 1,
    "host unlock arrives, stale client-only unlock removed, set reference and open menu preserved");
MusicCollectionSync.Apply([110, 3, 1, 110]);
Check(menu.Refreshes == 1, "repeated/reordered collection does not rebuild menu or duplicate tracks");
MusicCollectionSync.Apply([1, 3, 110, 124, 125]);
Check(originalSet.Contains(124) && originalSet.Contains(125) && menu.Refreshes == 2,
    "later full snapshot repairs multiple missed unlocks");
MusicCollectionSync.Apply(null);
Check(originalSet.Count == 5 && menu.Refreshes == 2, "absent snapshot field leaves collection intact");
MusicCollectionSync.Apply([]);
Check(originalSet.Count == 0 && menu.Refreshes == 3, "explicit empty host collection differs from absent field");
foreach (var connection in new object?[] { new ElinNetHost(), null }) {
    NetSession.Instance.Connection = connection;
    MusicCollectionSync.Apply(captured);
    Check(originalSet.Count == 0 && menu.Refreshes == 3, "host/solo collection cannot be overwritten by reconciliation");
}
NetSession.Instance.Connection = new ElinNetClient();
EClass.player = new();
UnityEngine.Object.Views = [];
MusicCollectionSync.Apply([1, 3, 7]);
Check(EClass.player.knownBGMs.SetEquals([1, 3, 7]), "new session receives its own collection without a menu or static cache");
Console.WriteLine($"{checks} checks passed");
