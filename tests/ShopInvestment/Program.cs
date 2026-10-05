using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
int checks = 0;
void Check(bool v, string s) { if (!v) throw new Exception(s); Console.WriteLine("PASS " + s); checks++; }
var host = new ElinNetHost(); var client = new ElinNetClient();
var hostActor = EClass.pc; hostActor.Discount = 100;
var actor = new Chara { uid = 719, Discount = 20 }; var shop = new Chara { uid = 800 };
host.ActiveRemoteCharas[1] = actor;
NetSession.Instance.Connection = host;
Check(Investment.Price(actor, shop) == 180 && EClass.pc == hostActor,
    "quote uses investing client's stats and restores host actor");
CalcMoney.Throw = true;
try { Investment.Price(actor, shop); } catch (InvalidOperationException) { }
CalcMoney.Throw = false;
Check(EClass.pc == hostActor, "price exception also restores host actor");
InvestmentDelta Request() => new() {
    RequestId = Guid.NewGuid(), Shop = shop, ZoneUid = 7, ExpectedLevel = shop.c_invest,
    QuotedPrice = Investment.Price(actor, shop), OriginPeer = 1,
};
var first = Request(); var concurrent = Request();
first.Apply(host); first.Apply(host); concurrent.Apply(host);
Check(actor.Money == 9820 && actor.Charges == 1 && actor.Experience == 0 && OwnerProgressionAwards.Read(actor).Single().Amount == 70 && shop.c_invest == 1 &&
    EClass._zone.influence == 1 && Guild.Merchant.Exp == 6,
    "investment settles payment, upgrade, pending XP, influence and credit once; concurrent quote rejected");
Check(hostActor.Charges == 0 && hostActor.Experience == 0, "host character receives neither charge nor investing XP");
var stalePrice = Request(); stalePrice.QuotedPrice--;
stalePrice.Apply(host);
Check(!host.Replies[^1].Accepted && actor.Charges == 1, "changed price cannot charge");
actor.Money = 0; Request().Apply(host); actor.Money = 10000;
Check(!host.Replies[^1].Accepted && shop.c_invest == 1, "insufficient money changes no shop state");
actor.Distance = 20; Request().Apply(host); actor.Distance = 1;
Check(!host.Replies[^1].HasState && actor.Charges == 1, "distant action rejected without bogus zero state");
shop.trait.CanInvest = false; Request().Apply(host); shop.trait.CanInvest = true;
Check(!host.Replies[^1].Accepted && actor.Charges == 1, "non-investable NPC rejected");
var wrongZone = Request(); wrongZone.ZoneUid = 99; wrongZone.Apply(host);
Check(!host.Replies[^1].Accepted && actor.Charges == 1, "wrong-zone request rejected");
var forged = Request(); forged.OriginPeer = 2; forged.Apply(host);
Check(actor.Charges == 1, "unknown peer cannot invest");

// A separate client replica owns its XP; no original dialogue payment is run.
var localActor = new Chara { uid = 719, Discount = 20 };
EClass.game.player.chara = localActor; NetSession.Instance.Connection = client;
var callbacks = 0;
List<ElinDelta> Sent() { client.Delta.RefreshBuffer(); return client.Delta.FlushOutBuffer(); }
Investment.Submit(shop, 1, 880, _ => callbacks++);
var sent = (InvestmentDelta)Sent().OfType<InvestmentDelta>().Last();
var receipt = new InvestmentResultDelta {
    RequestId = sent.RequestId, Shop = shop, ZoneUid = 7, HasState = true,
    Level = 2, Influence = 2, Award = new() { Id = Guid.NewGuid(), OwnerUid = 719, Skill = 292, Amount = 90 }, Accepted = true,
};
receipt.Apply(client); receipt.Apply(client);
Check(localActor.Experience == 90 && localActor.Charges == 0 && callbacks == 1 && SE.Payments == 1,
    "receipt grants client XP/confirmation once without another payment");
Check(shop.c_invest == 2 && EClass._zone.influence == 2, "receipt updates quote state before callback");
Investment.Submit(shop, 2, 1580, _ => callbacks++);
sent = (InvestmentDelta)Sent().OfType<InvestmentDelta>().Last();
new InvestmentResultDelta { RequestId = sent.RequestId, Shop = shop, ZoneUid = 7 }.Apply(client);
Check(shop.c_invest == 2 && EClass._zone.influence == 2 && localActor.Experience == 90,
    "validation failure cannot wipe shop level or influence");
Investment.Submit(shop, 2, 1580, _ => callbacks++);
sent = (InvestmentDelta)Sent().OfType<InvestmentDelta>().Last(); Investment.Reset();
receipt.RequestId = sent.RequestId; receipt.Apply(client);
Check(localActor.Experience == 90, "session reset drops stale receipt");

var drama = new DramaCustomSequence(); int nativePayments = 0;
var method = new DramaEventMethod { action = () => {
    GameLang.refDrama1 = "1580";
    drama.manager.lastTalk = new() { choices = [
        new() { text = "yes", onJump = () => nativePayments++ }, new() { text = "no" },
        new() { text = "quickInvest", onJump = () => nativePayments++ },
    ] };
} };
drama.events.Add(new() { step = "_investShop" }); drama.events.Add(method);
LayerDrama.Instance = new() { drama = drama.manager };
InvestmentDialoguePatch.After(drama, shop); method.action();
var quick = drama.manager.lastTalk.choices[2].onJump!;
Sent(); quick(); quick();
var quickRequests = Sent().OfType<InvestmentDelta>().ToList();
Check(nativePayments == 0 && quickRequests.Count == 1,
    "native quick-invest callback replaced and double-click queues only one request");
sent = quickRequests.Single();
receipt.RequestId = sent.RequestId; receipt.Level = 3; receipt.Apply(client);
Check(drama.LastJump == "_investShop", "accepted quick-invest resumes native quote after result");
method.action(); drama.manager.lastTalk.choices[0].onJump!();
sent = (InvestmentDelta)Sent().OfType<InvestmentDelta>().Last();
drama.TempGoto("anotherTopic"); receipt.RequestId = sent.RequestId; receipt.Apply(client);
Check(drama.LastJump == "anotherTopic", "late receipt cannot hijack a different dialogue topic");
// Exercise the production output queue and pending ledger, including save/rejoin.
Investment.Reset(); client.Delta.ClearOut();
var award = OwnerProgressionAwards.Read(actor).Single();
var oldElement = new ElementChangeDelta { Owner = localActor, Element = 292, Value = [1, 0, 100, 0] };
client.Delta.AddRemoteImmediate(oldElement);
client.Delta.DeferRemote(oldElement);
client.Delta.AddRemote(oldElement);
client.Delta.AddRemote(new CharaLevelDelta { Owner = localActor });
client.Delta.AddRemote(new CharaFeatPointDelta { Owner = localActor });
localActor.elements.GetOrCreateElement(20);
OwnerProgressionAwards.Receive([award]);
OwnerProgressionAwards.Receive([award]);
localActor.elements.Remove(20);
Check(localActor.ModExpCalls == 3, "repeated pending award offers calculate XP only once");
client.Delta.AddRemoteImmediate(new InvestmentDelta());
var batch = client.Delta.FlushOutBuffer();
Check(batch.Count == 2 && batch[0] is OwnerProgressionCommitDelta && batch[1] is InvestmentDelta,
    "commit precedes quick-invest and absorbs ordinary progression packets");
client.Delta.RefreshBuffer();
Check(client.Delta.FlushOutBuffer().Count == 0, "deferred and unrefreshed progression cannot overwrite committed state");
var commit = (OwnerProgressionCommitDelta)batch[0];
Check(commit.Elements.Single(e => e.Element == 20).Value.All(v => v == 0), "removed elements have explicit zero state");
Check(OwnerProgressionAwards.Read(actor).Count == 1 && actor.ModExpCalls == 0,
    "host persists pending award without calculating XP");
var savedPending = actor.GetStr("emp_pending_progression");
Investment.Reset();
actor.SetStr("emp_pending_progression", savedPending);
Check(OwnerProgressionAwards.Read(actor).Single().Id == award.Id, "saved award survives transient session reset and JSON reload");
// Simulate rejoining from host's old character, before the original commit arrived.
localActor = new Chara { uid = 719 };
EClass.game.player.chara = localActor;
OwnerProgressionAwards.Receive(OwnerProgressionAwards.Read(actor).ToArray());
var recovered = (OwnerProgressionCommitDelta)Sent().Single();
Check(localActor.ModExpCalls == 1 && localActor.elements.dict[292].vExp == 70,
    "disconnect before commit reapplies once to restored host baseline");
EClass.game.player.chara = hostActor; NetSession.Instance.Connection = host;
RemoteCard.Resolved[719] = actor;
recovered.OriginPeer = 2; recovered.Apply(host);
Check(OwnerProgressionAwards.Read(actor).Count == 1, "other peer cannot acknowledge owner's award");
var invalid = new OwnerProgressionCommitDelta {
    Owner = actor, Awards = recovered.Awards, Level = 2, Exp = 4, Feat = 1, OriginPeer = 1,
    Elements = [new() { Owner = actor, Element = 99999, Value = [1, 0, 100, 0] }],
};
invalid.Apply(host);
Check(actor.LV == 1 && OwnerProgressionAwards.Read(actor).Count == 1,
    "unknown element rejects whole commit before changing saved progression");
invalid.Elements = recovered.Elements; invalid.Awards = [award.Id, award.Id]; invalid.Apply(host);
Check(actor.LV == 1 && OwnerProgressionAwards.Read(actor).Count == 1,
    "duplicate award IDs reject whole commit");
var laterAward = OwnerProgressionAwards.Record(actor, Guid.NewGuid(), 292, 80);
recovered.OriginPeer = 1; recovered.Apply(host);
Check(actor.ModExpCalls == 0 && actor.elements.dict[292].vExp == 70 && actor.elements.dict[10].vExp == 3 &&
    actor.LV == 2 && actor.exp == 4 && actor.feat == 1 && OwnerProgressionAwards.Read(actor).Single().Id == laterAward.Id,
    "host saves exact skill, parent, level and feat outcome without rerolling XP");
actor.elements.dict[292].vExp = 999;
recovered.Apply(host);
Check(actor.elements.dict[292].vExp == 999, "duplicate acknowledgement cannot roll back newer progression");
OwnerProgressionAwards.Save(actor, []); // independent later award is outside this reconnect scenario
Investment.Reset(); EClass.game.player.chara = localActor; NetSession.Instance.Connection = client;
OwnerProgressionAwards.Receive(OwnerProgressionAwards.Read(actor).ToArray());
Check(localActor.ModExpCalls == 1, "disconnect after commit has no remaining award to pay");
var deadAward = new OwnerProgressionAward { Id = Guid.NewGuid(), OwnerUid = 719, Skill = 292, Amount = 50 };
localActor.isDead = true; OwnerProgressionAwards.Receive([deadAward]);
localActor.isDead = false; OwnerProgressionAwards.Receive([deadAward]); OwnerProgressionAwards.Receive([deadAward]);
Check(localActor.ModExpCalls == 2, "dead recipient defers award; repeated offers after revival grant once");
// Town investment shares validation/acknowledgement, but uses distinct vanilla effects.
Investment.Reset(); client.Delta.ClearOut();
EClass.game.player.chara = hostActor; NetSession.Instance.Connection = host;
actor.Money = 10000;
EClass._zone.development = 10; EClass._zone.investment = 100; EClass._zone.influence = 0;
var priorShopLevel = shop.c_invest; var priorContribution = Guild.Merchant.Exp;
var priorCharges = actor.Charges;
InvestmentDelta TownRequest() => new() {
    RequestId = Guid.NewGuid(), Shop = shop, ZoneUid = 7, Kind = InvestmentKind.Town,
    ExpectedLevel = EClass._zone.development, QuotedPrice = Investment.Price(actor, shop, InvestmentKind.Town), OriginPeer = 1,
};
EClass._zone.AllowInvest = false; TownRequest().Apply(host); Request().Apply(host);
Check(!host.Replies[^1].Accepted && actor.Charges == priorCharges, "zone investment prohibition applies to both shop and town requests");
EClass._zone.AllowInvest = true;
var town = TownRequest(); var otherTown = TownRequest();
Check(town.QuotedPrice == 980 && EClass.pc == hostActor, "town quote uses client discount and restores host actor");
town.Apply(host); town.Apply(host); otherTown.Apply(host);
Check(actor.Money == 9020 && actor.Charges == priorCharges + 1 && EClass.RandomCalls == 1 &&
    EClass._zone.development == 17 && EClass._zone.investment == 1080 && EClass._zone.influence == 2,
    "town payment and development roll commit once; concurrent old quote cannot spend");
Check(shop.c_invest == priorShopLevel && Guild.Merchant.Exp == priorContribution &&
    OwnerProgressionAwards.Read(actor).Single().Amount == 134,
    "town grants XP from resulting development without shop upgrade or Merchant credit");
shop.trait.CanInvestTown = false; TownRequest().Apply(host); shop.trait.CanInvestTown = true;
Check(!host.Replies[^1].Accepted, "town investment requires a town representative");
Guild.Current = Guild.Merchant; TownRequest().Apply(host); Guild.Current = null;
Check(!host.Replies[^1].Accepted, "guild location cannot expose ordinary town investment");
var unknown = TownRequest(); unknown.Kind = (InvestmentKind)99; unknown.Apply(host);
Check(!host.Replies[^1].Accepted && EClass.RandomCalls == 1, "invalid investment kind cannot spend or roll development");
var townStale = TownRequest(); townStale.QuotedPrice++; townStale.Apply(host);
Check(!host.Replies[^1].Accepted, "stale town price rejected independently of development");
actor.Money = 0; TownRequest().Apply(host);
Check(!host.Replies[^1].Accepted && EClass.RandomCalls == 1, "insufficient town funds leave development untouched");
NetSession.Instance.Connection = client; EClass.game.player.chara = localActor;
var townDrama = new DramaCustomSequence();
var townMethod = new DramaEventMethod { action = () => {
    GameLang.refDrama1 = "1330";
    townDrama.manager.lastTalk = new() { choices = [new() { text = "yes" }, new() { text = "no" }, new() { text = "quickInvest" }] };
} };
townDrama.events.Add(new() { step = "_investZone" }); townDrama.events.Add(townMethod);
townDrama.events.Add(new() { step = "_investShop" });
var secondMethod = new DramaEventMethod { action = townMethod.action }; townDrama.events.Add(secondMethod);
LayerDrama.Instance = new() { drama = townDrama.manager };
InvestmentDialoguePatch.After(townDrama, shop); townMethod.action(); townDrama.manager.lastTalk.choices[2].onJump!();
var townSent = Sent().OfType<InvestmentDelta>().Single();
Check(townSent.Kind == InvestmentKind.Town && townSent.ExpectedLevel == 17,
    "town menu submits town development rather than NPC investment level");
new InvestmentResultDelta { RequestId = townSent.RequestId, Shop = shop, ZoneUid = 7, Kind = InvestmentKind.Town,
    Accepted = true, HasState = true, Level = priorShopLevel, Development = 24, TownInvestment = 2410, Influence = 4 }.Apply(client);
Check(townDrama.LastJump == "_investZone" && EClass._zone.development == 24 && EClass._zone.investment == 2410,
    "town receipt applies authoritative state before reopening town quote");
secondMethod.action(); townDrama.manager.lastTalk.choices[0].onJump!();
Check(Sent().OfType<InvestmentDelta>().Single().Kind == InvestmentKind.Shop,
    "one native dialogue patches both town and shop investment steps");
// Throw awards use the same persisted acknowledgment channel, including fractional
// native inputs. Existing integer JSON awards must remain readable without migration.
OwnerProgressionAwards.Reset(); client.Delta.ClearOut();
NetSession.Instance.Connection = host;
OwnerProgressionAwards.Save(actor, []);
var fractional = OwnerProgressionAwards.Record(actor, Guid.NewGuid(), 292, 2.75f);
Check(OwnerProgressionAwards.Read(actor).Single().Amount == 2.75f,
    "fractional native XP survives pending-award JSON persistence");
actor.SetStr("emp_pending_progression", "[{\"Id\":\"" + Guid.NewGuid() + "\",\"OwnerUid\":719,\"Skill\":292,\"Amount\":50}]");
Check(OwnerProgressionAwards.Read(actor).Single().Amount == 50f,
    "legacy integer pending awards load without save migration");
OwnerProgressionAwards.Save(actor, [fractional]);
NetSession.Instance.Connection = client;
var previousExperience = localActor.Experience;
OwnerProgressionAwards.Receive([fractional]); OwnerProgressionAwards.Receive([fractional]);
Check(localActor.Experience == previousExperience + 2.75f,
    "fractional award enters native owner XP once without truncation");
foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    OwnerProgressionAwards.Receive([new() { Id = Guid.NewGuid(), OwnerUid = 719, Skill = 292, Amount = value }]);
Check(localActor.Experience == previousExperience + 2.75f, "nonfinite awards cannot corrupt owner progression");
Console.WriteLine($"{checks} checks passed");
