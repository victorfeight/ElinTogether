using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;

int checks = 0;
void Check(bool value, string name) {
    if (!value) throw new Exception(name);
    checks++;
    Console.WriteLine("PASS " + name);
}
Thing Item(int uid, string id = "money", bool host = true, int num = 1) => new() { uid = uid, id = id, IsHostOwned = host, Num = num };
var client = new ElinNetClient();
NetSession.Instance.Connection = client;
var inv = new UIInventory { Items = [Item(1, num: 3), Item(2, num: 7), Item(3, num: 11)] };
inv.Sort();
Check(inv.Visits <= 9 && client.Delta.Items.Count == 2, "three stackable items terminate with exactly two requests");
Check(inv.Items.All(t => !t.isDestroyed) && inv.Items.Sum(t => t.Num) == 21, "client does not predict deletion or change quantities");
Check(InvSortEvent.QueuedSources is null, "sort scope is cleared");
var removed = new HashSet<int>();
foreach (var request in client.Delta.Items) {
    Check(!removed.Contains(request.To.uid) && removed.Add(request.Card.uid), "request targets a surviving stack and consumes its source once");
    ElinDelta.IsApplying = true;
    Check(request.Card.TryStackTo(request.To), "host-confirmed merge takes the vanilla path");
    ElinDelta.IsApplying = false;
}
Check(inv.Items.Count(t => !t.isDestroyed) == 1 && inv.Items.Single(t => !t.isDestroyed).Num == 21, "ordered merges preserve all 21 items in one stack");
client.Delta.Items.Clear();
inv.Sort();
Check(client.Delta.Items.Count == 0, "refresh after confirmation queues no redundant merges");
Check(Item(10).TryStackTo(Item(11)) && client.Delta.Items.Count == 1, "normal drag stacking retains deferred-success contract");
client.Delta.Items.Clear();
inv = new UIInventory { Items = [Item(20), Item(21, "plat"), Item(22)] };
inv.Items[2].invY = 1;
inv.Sort();
Check(client.Delta.Items.Count == 0, "different items and reserved inventory row are not merged");
inv = new UIInventory { Items = [Item(30, host: false), Item(31, host: false)] };
inv.Sort();
Check(inv.Items.Count(t => !t.isDestroyed) == 1 && client.Delta.Items.Count == 0, "client-local stacks still merge synchronously");
inv = new UIInventory { Items = [Item(40), Item(41, host: false)] };
inv.Sort();
Check(inv.Items.All(t => !t.isDestroyed) && client.Delta.Items.Count == 0, "mixed authority cannot merge");
InvSortEvent.Begin(out var outer);
var scope = InvSortEvent.QueuedSources!;
scope.Add(50);
try {
    InvSortEvent.Begin(out var inner);
    try { throw new InvalidOperationException(); }
    finally { InvSortEvent.End(inner); }
} catch (InvalidOperationException) { }
Check(ReferenceEquals(scope, InvSortEvent.QueuedSources) && scope.Contains(50), "nested sort shares deduplication and restores scope after exception");
InvSortEvent.End(outer);
Check(InvSortEvent.QueuedSources is null, "outer finalizer clears scope after exception");
foreach (var connection in new ElinNetBase?[] { null, new ElinNetBase() }) {
    NetSession.Instance.Connection = connection;
    inv = new UIInventory { Items = [Item(60), Item(61), Item(62)] };
    inv.Sort();
    Check(inv.Items.Single(t => !t.isDestroyed).Num == 3, "solo/host vanilla sort still consolidates all stacks");
}
Console.WriteLine($"{checks} checks passed");
