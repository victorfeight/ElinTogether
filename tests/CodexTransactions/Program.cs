using ElinTogether.Patches;
using ElinTogether.Models;
using ElinTogether.Net;

// Exercise the production transaction handler against an in-memory game/transport boundary.
var host = new ElinNetHost();
NetSession.Instance.Connection = host;
EClass.sources.charas.map["putit"] = new();
EClass.pc = new Chara { uid = 1 };
var client = new Chara { uid = 719, c_altName = "Vic" };
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
var announcementsBefore=host.Delta.Items.Count;
collect.Apply(host); collect.Apply(host);
Check(host.Delta.Items.Count==announcementsBefore+1 &&
      ((CodexCountDelta)host.Delta.Items.Last()).Message=="Vic added 3 Putit cards to the shared collection.",
      "deposit hook broadcasts actor-specific shared confirmation exactly once for a duplicate request");
Check(entry.numCard == 3 && collectible.isDestroyed, "deposit consumes stack and credits once");
new CodexRequestDelta { Action = CodexRequestDelta.Operation.Collect, ThingUid = 123, OriginPeer = 7 }.Apply(host);
Check(entry.numCard == 3, "new request cannot collect already consumed item");
var other = new Thing { parent = second, trait = new TraitCard(), c_idRefCard = "putit" }; CardCache.Cards[124] = other;
new CodexRequestDelta { Action = CodexRequestDelta.Operation.Collect, ThingUid = 124, OriginPeer = 7 }.Apply(host);
Check(entry.numCard == 3 && !other.isDestroyed, "cannot deposit another player's inventory");

// A full-looking NPC grid may be entirely occupied by hotbar/equipment.
var crowded = new Chara { uid = 721 }; host.ActiveRemoteCharas[9] = crowded;
crowded.things.CapacityDriven = true; crowded.things.GridSize = 4;
for (var i = 0; i < 2; i++) crowded.AddThing(new Thing { invY = 1 }, false);
for (var i = 0; i < 2; i++) crowded.AddThing(new Thing { isEquipped = true }, false);
entry.numCard = 1;
var remoteFree = Request(); remoteFree.OriginPeer = 9; remoteFree.Apply(host);
Check(entry.numCard == 0 && crowded.things.Count == 5, "remote full-looking inventory excludes hotbar and equipment");
// Four normal entries really fill a four-slot bag, even with extra worn items.
foreach (var t in crowded.things.Take(4)) { t.invY = 0; t.isEquipped = false; }
entry.numCard = 1;
var actualFull = Request(); actualFull.OriginPeer = 9; actualFull.Apply(host);
Check(entry.numCard == 1 && crowded.things.Count == 5, "shared capacity never bypasses actual backpack capacity");

// Actual production routing prefixes, with only transport/formatting mocked.
#if !CAPACITY_ONLY
host.Replies.Clear();
var result = "";
using (PersonalMsgSayPatch.ForCollector(client)) {
 Check(!PersonalMsgSayPatch.OnCollectionMessage("addedCards", 3, "3", null, ref result) &&
       host.Replies.Count == 0 && result == "",
       "vanilla personal collection message suppressed without a duplicate targeted packet");
 using (PersonalMsgSayPatch.ForCollector(EClass.pc)) {
  Check(!PersonalMsgSayPatch.OnCollectionMessage("addedCards", 1, "1", null, ref result) && host.Replies.Count == 0,
        "host collection also suppresses vanilla personal wording");
 }
 Check(PersonalMsgSayPatch.CodexCollector == client, "nested scope restores collector");
}
Check(PersonalMsgSayPatch.CodexCollector == null, "collector does not leak past request");
try { using var scope = PersonalMsgSayPatch.ForCollector(client); throw new InvalidOperationException(); }
catch (InvalidOperationException) {}
Check(PersonalMsgSayPatch.CodexCollector == null, "collector restored after exception");
PersonalMsgSayPatch.FirstTimeCraftReceiver = client;
Check(!PersonalMsgSayPatch.OnFirstTimeCraftMessage("firstTimeCraft", "chair", null, null, null, ref result) &&
      host.LastPeer == 7 && host.Replies.Count == 1, "first craft reaches crafter without host duplicate");
host.Deliver = false;
Check(PersonalMsgSayPatch.OnFirstTimeCraftMessage("firstTimeCraft", "chair", null, null, null, ref result),
      "failed delivery retains vanilla message");
host.Deliver = true;
Msg.ignoreAll = true;
Check(PersonalMsgSayPatch.OnFirstTimeCraftMessage("firstTimeCraft", "chair", null, null, null, ref result) &&
      host.Replies.Count == 1, "muted vanilla messages are not forwarded");
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

// Production count packet carries the shared confirmation; replay only displays it.
host.Delta.Items.Clear(); Msg.Lines.Clear();
entry.numCard = 3;
CodexCountDelta.Publish("putit", client, 3);
var announcement = (CodexCountDelta)host.Delta.Items.Single();
Check(announcement.Message == "Vic added 3 Putit cards to the shared collection." &&
      Msg.Lines.Single() == announcement.Message, "collection names actor/species/count and shows once on host");
Msg.Lines.Clear();
var receiverNet = new ElinNetBase();
announcement.Apply(receiverNet);
Check(Msg.Lines.Single() == announcement.Message && receiverNet.Delta.Items.Count == 0,
      "client sees the same shared confirmation without re-broadcasting");
host.Delta.Items.Clear(); Msg.Lines.Clear();
entry.numCard=1;
Request().Apply(host);
var withdrawal=(CodexCountDelta)host.Delta.Items.Single();
Check(withdrawal.Count==0 && withdrawal.Message=="Vic withdrew 1 Putit card from the shared collection." &&
      Msg.Lines.Count==1,"successful withdrawal is announced after delivery and debit");
host.Delta.Items.Clear(); Msg.Lines.Clear();
Request().Apply(host);
Check(host.Delta.Items.Count==0 && Msg.Lines.Count==0 && host.LastPeer==7,
      "failed withdrawal remains private and emits no shared success");
NetSession.Instance.Connection=null;
Check(PersonalMsgSayPatch.OnCollectionMessage("addedCards",1,"1",null,ref result),
      "single-player retains vanilla collection messages");
NetSession.Instance.Connection=host;

// Production talisman completion, routing, and state application.
var originalPC=EClass.pc;
var weapon=new Thing{uid=800,parent=client};
var crafting=new AI_UseCrafter{owner=client,ings=[weapon]};
var crafter=new TraitCrafter();
TalismanCraftPatch.Before(crafter,crafting,out var craftContext);
try {
 Check(EClass.pc==client && TalismanCraftPatch.Receiver==client,"talisman evaluates requesting player's feats and message context");
 weapon.ammoData=new Thing{uid=801,refVal=1234,encLV=150}; weapon.c_ammo=12;
 host.Replies.Clear(); host.Deliver=true;
 Check(!PersonalMsgSayPatch.OnRemoteToggleMessage("talisman_pc",weapon,weapon.ammoData,null,null,ref result) && host.Replies.Count==1 && host.LastPeer==7,"talisman confirmation reaches crafting client exactly once");
 CharaProgressCompleteEvent.Packing=true;
 TalismanCraftPatch.After(craftContext);
} finally { TalismanCraftPatch.Restore(craftContext); }
Check(EClass.pc==originalPC && TalismanCraftPatch.Receiver==null,"craft context restores host player");
var spellUpdate=(CardAmmoDelta)CharaProgressCompleteEvent.Packed.Single();
weapon.ammoData=null; weapon.c_ammo=0;
spellUpdate.Apply(host);
Check(weapon.ammoData==null && weapon.c_ammo==0,"host rejects incoming weapon-state overwrite");
spellUpdate.Apply(new ElinNetClient()); spellUpdate.Apply(new ElinNetClient());
Check(weapon.ammoData?.refVal==1234 && weapon.ammoData.encLV==150 && weapon.c_ammo==12 && weapon.parent==client && LayerInventory.Dirty==weapon,"client receives spell potency and charges idempotently without replacing weapon");
weapon.isDestroyed=true; weapon.c_ammo=0; spellUpdate.Apply(new ElinNetClient());
Check(weapon.c_ammo==0,"late spell update ignores destroyed weapon");
crafter.Source.type="Grind"; TalismanCraftPatch.Before(crafter,crafting,out var unrelated);
Check(unrelated==null && EClass.pc==originalPC,"unrelated crafting retains vanilla context");
crafter.Source.type="Talisman"; crafting.owner=originalPC;
TalismanCraftPatch.Before(crafter,crafting,out var ownContext);
try { Check(PersonalMsgSayPatch.OnRemoteToggleMessage("talisman_pc",weapon,weapon,null,null,ref result),"host's own talisman confirmation remains local"); }
finally { TalismanCraftPatch.Restore(ownContext); }
#endif
