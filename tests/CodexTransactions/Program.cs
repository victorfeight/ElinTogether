using ElinTogether.Patches;
using ElinTogether.Models;
using ElinTogether.Net;

// Exercise the production transaction handler against an in-memory game/transport boundary.
var host = new ElinNetHost();
NetSession.Instance.Connection = host;
EClass.sources.charas.map["putit"] = new();
EClass.pc = new Chara { uid = 1 };
var client = new Chara { uid = 719 };
host.ActiveRemoteCharas[7] = client;
var entry = EClass.player.codex.GetOrCreate("putit");
CodexRequestDelta Request() => new() { Action = CodexRequestDelta.Operation.Withdraw, Species = "putit", OriginPeer = 7 };
void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
entry.numCard = 2;
var first = Request(); first.Apply(host);
Check(entry.numCard == 1 && client.things.Items.Count == 1 && EClass.pc.things.Items.Count == 0, "withdraw delivers to requesting client, debits once");
first.Apply(host);
Check(entry.numCard == 1 && client.things.Items.Count == 1, "duplicate request does not spend twice");
client.things.Full = true; client.things.GridSize = client.things.Count;
Request().Apply(host);
Check(entry.numCard == 1 && client.things.Items.Count == 1 && ThingGen.Last!.isDestroyed, "full inventory leaves book intact, discards provisional item");
Check(host.Replies.Count == 1, "failure response reaches requester");
client.things.Stack = client.things.Items[0];
Request().Apply(host);
Check(entry.numCard == 0 && client.things.Items[0].Num == 2, "full bag accepts compatible stack");
Request().Apply(host);
Check(entry.numCard == 0 && client.things.Items[0].Num == 2, "last card cannot be withdrawn again");
entry.numCard = 1;
var unknown = Request(); unknown.OriginPeer = 99; unknown.Apply(host);
Check(entry.numCard == 1, "unknown peer cannot withdraw");
client.things.Full = false; client.things.GridSize = 40; client.things.Stack = null;
var second = new Chara { uid = 720 }; host.ActiveRemoteCharas[8] = second;
Request().Apply(host);
var competing = Request(); competing.OriginPeer = 8; competing.Apply(host);
Check(entry.numCard == 0 && second.things.Items.Count == 0, "competing requests for final card have one winner");
var collectible = new Thing { parent = client, trait = new TraitCard(), c_idRefCard = "putit", Num = 3 };
CardCache.Cards[123] = collectible;
var collect = new CodexRequestDelta { Action = CodexRequestDelta.Operation.Collect, ThingUid = 123, OriginPeer = 7 };
collect.Apply(host); collect.Apply(host);
Check(entry.numCard == 3 && collectible.isDestroyed, "deposit consumes stack and credits once");
new CodexRequestDelta { Action = CodexRequestDelta.Operation.Collect, ThingUid = 123, OriginPeer = 7 }.Apply(host);
Check(entry.numCard == 3, "new request cannot collect already consumed item");
var other = new Thing { parent = second, trait = new TraitCard(), c_idRefCard = "putit" }; CardCache.Cards[124] = other;
new CodexRequestDelta { Action = CodexRequestDelta.Operation.Collect, ThingUid = 124, OriginPeer = 7 }.Apply(host);
Check(entry.numCard == 3 && !other.isDestroyed, "cannot deposit another player's inventory");

// A full-looking NPC grid may be entirely occupied by hotbar/equipment.
var crowded = new Chara { uid = 721 }; host.ActiveRemoteCharas[9] = crowded;
crowded.things.Full = true; crowded.things.GridSize = 4;
for (var i = 0; i < 2; i++) crowded.AddThing(new Thing { invY = 1 }, false);
for (var i = 0; i < 2; i++) crowded.AddThing(new Thing { isEquipped = true }, false);
entry.numCard = 1;
var remoteFree = Request(); remoteFree.OriginPeer = 9; remoteFree.Apply(host);
Check(entry.numCard == 0 && crowded.things.Count == 5, "remote full-looking inventory excludes hotbar and equipment");
// Four normal entries really fill a four-slot bag, even with extra worn items.
foreach (var t in crowded.things.Take(4)) { t.invY = 0; t.isEquipped = false; }
entry.numCard = 1;
var actualFull = Request(); actualFull.OriginPeer = 9; actualFull.Apply(host);
Check(entry.numCard == 1 && crowded.things.Count == 5, "fallback never bypasses actual backpack capacity");

// Actual production routing prefixes, with only transport/formatting mocked.
host.Replies.Clear();
var result = "";
using (PersonalMsgSayPatch.ForCollector(client)) {
 Check(!PersonalMsgSayPatch.OnCollectionMessage("addedCards", 3, "3", null, ref result) &&
       host.LastPeer == 7 && host.Replies.Count == 1 && result == "addedCards:True",
       "collection routes to collector and suppresses host, preserving plural selection");
 using (PersonalMsgSayPatch.ForCollector(EClass.pc)) {
  Check(PersonalMsgSayPatch.OnCollectionMessage("addedCards", 1, "1", null, ref result) && host.Replies.Count == 1,
        "nested host collection stays local");
 }
 Check(PersonalMsgSayPatch.CodexCollector == client, "nested scope restores collector");
}
Check(PersonalMsgSayPatch.CodexCollector == null, "collector does not leak past request");
try { using var scope = PersonalMsgSayPatch.ForCollector(client); throw new InvalidOperationException(); }
catch (InvalidOperationException) {}
Check(PersonalMsgSayPatch.CodexCollector == null, "collector restored after exception");
PersonalMsgSayPatch.FirstTimeCraftReceiver = client;
Check(!PersonalMsgSayPatch.OnFirstTimeCraftMessage("firstTimeCraft", "chair", null, null, null, ref result) &&
      host.LastPeer == 7 && host.Replies.Count == 2, "first craft reaches crafter without host duplicate");
host.Deliver = false;
Check(PersonalMsgSayPatch.OnFirstTimeCraftMessage("firstTimeCraft", "chair", null, null, null, ref result),
      "failed delivery retains vanilla message");
host.Deliver = true;
Msg.ignoreAll = true;
Check(PersonalMsgSayPatch.OnFirstTimeCraftMessage("firstTimeCraft", "chair", null, null, null, ref result) &&
      host.Replies.Count == 2, "muted vanilla messages are not forwarded");
Msg.ignoreAll = false;
RemoteCraft.ProductReceiver = client;
Check(!PersonalMsgSayPatch.OnPersonalMessage("crafted", new Thing(), null, null, null, ref result) && host.LastPeer == 7,
      "crafted confirmation retains crafter routing");
PersonalMsgSayPatch.RefuelPeer = 8;
Check(!PersonalMsgSayPatch.OnPersonalMessage("fueled", new Thing(), null, null, null, ref result) && host.LastPeer == 8,
      "refuel confirmation retains actor routing");
PersonalMsgSayPatch.RemoteToggleReplay = true;
var sent = host.Replies.Count;
Check(!PersonalMsgSayPatch.OnRemoteToggleMessage("open", client, new Thing(), null, null, ref result) &&
      host.Replies.Count == sent, "toggle replay suppressed without sending duplicate");
Check(PersonalMsgSayPatch.OnPersonalMessage("unrelated", new Thing(), null, null, null, ref result) && host.Replies.Count == sent,
      "unclassified messages retain vanilla behavior");
