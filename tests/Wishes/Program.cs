using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;

int checks = 0;
void Check(bool condition, string name) {
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS " + name); checks++;
}
var host = new ElinNetHost();
var client = new ElinNetClient();
var hostPC = EClass.pc;
var alice = new Chara { uid = 719, pos = new() { x = 52, z = 49 } };
var bob = new Chara { uid = 720, pos = new() { x = 55, z = 50 } };
host.ActiveRemoteCharas[1] = alice;
host.ActiveRemoteCharas[2] = bob;
NetSession.Instance.Connection = host;
Guid Grant(Chara actor, int power = 319, BlessedState state = BlessedState.Normal) {
    Check(!WishEffectPatch.Before(EffectId.Wish, power, state, actor, null), "remote effect replaces vanilla dialog");
    return ((WishPromptDelta)host.Sent.Last().Delta).RequestId;
}
void Reply(Guid id, int peer = 1, string? text = "big fridge") =>
    new WishReplyDelta { RequestId = id, OriginPeer = peer, Text = text }.Apply(host);

NetSession.Instance.Connection = client;
Check(!WishEffectPatch.Before(EffectId.Wish, 319, BlessedState.Normal, alice, null) && Dialog.Callbacks.Count == 0,
    "client prediction creates neither dialog nor reward");
Check(WishEffectPatch.Before(EffectId.Summon, 319, BlessedState.Normal, alice, null), "other effects unchanged");
NetSession.Instance.Connection = null;
Check(WishEffectPatch.Before(EffectId.Wish, 319, BlessedState.Normal, hostPC, null), "solo wish unchanged");
NetSession.Instance.Connection = host;
Check(WishEffectPatch.Before(EffectId.Wish, 319, BlessedState.Normal, hostPC, null), "host wish unchanged");
var first = Grant(alice);
Check(host.Sent.Last().Peer == 1, "prompt goes only to wishing peer");
var prompt = (WishPromptDelta)host.Sent.Last().Delta;
NetSession.Instance.Connection = client;
prompt.Apply(client); prompt.Apply(client);
Check(Dialog.Callbacks.Count == 1, "duplicate prompt opens one dialog");
Dialog.Callbacks.Last()(false, "big fridge"); Dialog.Callbacks.Last()(false, "big fridge");
Check(client.Delta.Items.Count == 1, "repeated dialog callback sends one reply");
NetSession.Instance.Connection = host;
Reply(first, peer: 2);
Check(ActEffect.Results.Count == 0, "different peer cannot consume a grant");
Reply(first);
var result = ActEffect.Results.Single();
Check(result.Recipient == 719 && result.Power == 319 && result.X == 52 && result.Z == 49, "vanilla wish executes for correct recipient, power and position");
Check(EClass.pc == hostPC && WishInteraction.Receiver == null, "host player and message context restored");
Reply(first);
Reply(Guid.NewGuid());
Check(ActEffect.Results.Count == 1, "duplicate and unsolicited replies create no reward");
var cancel = Grant(alice); Reply(cancel, text: null); Reply(cancel);
Check(ActEffect.Results.Count == 1, "cancel consumes grant without reward or retry");
var a = Grant(alice, 200, BlessedState.Blessed);
var b = Grant(bob, 77, BlessedState.Cursed);
Reply(b, 2, "money"); Reply(a);
Check(ActEffect.Results[^2].Recipient == 720 && ActEffect.Results[^2].State == BlessedState.Cursed &&
      ActEffect.Results[^2].Power == 77 && ActEffect.Results[^1].Power == 200 && ActEffect.Results[^1].State == BlessedState.Blessed,
    "simultaneous wishes preserve separate host-computed power and blessing state");
int count = ActEffect.Results.Count;
var stale = Grant(alice); WishInteraction.ReleasePeer(1); Reply(stale);
Check(ActEffect.Results.Count == count, "disconnect invalidates pending wish");
stale = Grant(alice); EClass._zone.uid++; Reply(stale); EClass._zone.uid--;
Check(ActEffect.Results.Count == count && host.Sent.Last().Delta is MsgSayDelta, "zone change rejects stale wish with a client explanation");
stale = Grant(alice); alice.isDead = true; Reply(stale); alice.isDead = false;
Check(ActEffect.Results.Count == count, "dead recipient cannot complete wish");
var failure = Grant(alice); ActEffect.Throw = true;
try { Reply(failure); throw new Exception("Expected vanilla exception"); } catch (InvalidOperationException) { }
ActEffect.Throw = false; Reply(failure);
Check(EClass.pc == hostPC && WishInteraction.Receiver == null && ActEffect.Results.Count == count, "exception restores host context and cannot duplicate reward on retry");
host.Deliver = false; var undelivered = Grant(alice); host.Deliver = true; Reply(undelivered);
Check(ActEffect.Results.Count == count, "failed prompt delivery leaves no usable grant");
var reset = Grant(alice); WishInteraction.Clear(); Reply(reset);
Check(ActEffect.Results.Count == count, "session reset invalidates pending wishes");
NetSession.Instance.Connection = client;
new WishPromptDelta { RequestId = Guid.NewGuid() }.Apply(client);
NetSession.Instance.Connection = new ElinNetClient();
var sent = client.Delta.Items.Count;
Dialog.Callbacks.Last()(false, "money");
Check(client.Delta.Items.Count == sent, "old dialog cannot send into a disconnected session");
Console.WriteLine($"{checks} checks passed");
