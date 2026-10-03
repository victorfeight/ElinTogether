using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
void Check(bool v,string n){if(!v)throw new Exception(n);Console.WriteLine("PASS "+n);}
var client=new ElinNetClient();var host=new ElinNetHost();
var caster=new Chara{uid=719,IsPC=true,pos=new(50,50)};
var wand=new Thing{uid=123,parent=caster,c_charges=4};var rod=new TraitRod{owner=wand};wand.trait=rod;
var zap=new ActZap{trait=rod};
NetSession.Instance.Connection=client;Act.CC=caster;Act.TC=caster;Act.TP=new(51,50);
ActZapEvent.Before(zap,out var packet);
Check(packet!=null && packet.ActId==5051 && packet.Wand!.Uid==123,"zap captures wand identity even before lazy act ID initialization");
Act.TP.x=70;Act.TC=null;
Check(packet!.Pos!.X==51,"request retains original aimed tile despite vanilla context mutation");
ActZapEvent.After(true,packet);
CharaActPerformEvent.OnCharaActPerform(zap,true);
Check(client.Delta.Items.Count==1,"dedicated zap event sends one request with no generic duplicate");
packet.OriginPeer=7;caster.IsPC=false;host.ActiveRemoteCharas[7]=caster;NetSession.Instance.Connection=host;
packet.Apply(host);
Check(ActZap.Casts==1&&wand.c_charges==3,"host binds real rod and spends exactly one authoritative charge");
Check(ActZap.LastAim!.x==51&&ActZap.LastAim.z==50,"nearby ground aim is not compensated onto caster tile");
Check(host.Delta.Items.Count==2&&host.Delta.Items[0] is CardGenDelta&&host.Delta.Items[1] is ZoneAddCardDelta,"host publishes summon generation followed by placement through existing hooks");
Check(CardCache.DestroyQueued==0&&EClass._map.charas.Count==1,"authoritative summon is never scheduled for client prediction cleanup");
Check(!ElinDelta.IsApplying,"host simulation scope restores replay state");
packet.Apply(host);
Check(ActZap.Casts==1&&wand.c_charges==3&&EClass._map.charas.Count==1,"duplicate request cannot spend charge or summon twice");
packet.Apply(client);
Check(ActZap.Casts==1,"clients do not replay host zap into extra predicted summons");
CharaActPerformDelta Request(){Act.CC=caster;Act.TP=new(52,50);return CharaActPerformDelta.Create(zap);}
var wrong=Request();wrong.OriginPeer=8;wrong.Apply(host);
Check(ActZap.Casts==1,"unknown peer cannot cast using another player's wand");
var transferred=Request();transferred.OriginPeer=7;wand.parent=new Chara();transferred.Apply(host);
Check(ActZap.Casts==1,"transferred wand request is rejected without effects");
wand.parent=caster;
var stale=Request();stale.OriginPeer=7;EClass._zone.uid=8;stale.Apply(host);EClass._zone.uid=7;
Check(ActZap.Casts==1,"old zone request cannot create summons in the new map");
var outside=Request();outside.OriginPeer=7;wand.isDestroyed=true;outside.Apply(host);wand.isDestroyed=false;
Check(ActZap.Casts==1,"destroyed rod request is rejected");
rod.IdEffect="Silence";var silence=Request();silence.OriginPeer=7;silence.Apply(host);
Check(ActZap.Conditions==1&&wand.c_charges==2,"same bound rod path executes non-summon effect once");
wand.c_charges=0;var empty=Request();empty.OriginPeer=7;empty.Apply(host);
Check(ActZap.Conditions==1&&wand.c_charges==0,"empty wand retains vanilla no-effect behavior");
NetSession.Instance.Connection=null;ActZapEvent.Before(zap,out var standalone);
Check(standalone==null,"single-player zap is not captured");
Console.WriteLine("Boundary tests do not run Unity or replace live multiplayer acceptance.");

var summonRef=new ActRef{refThing=wand};caster.IsPC=true;NetSession.Instance.Connection=client;
Check(!ClientWandSummonEvent.Before(EffectId.Summon,caster,summonRef),"local wand summon waits for authoritative creatures");
Check(ClientWandSummonEvent.Before(EffectId.Silence,caster,summonRef),"non-summon wand effect unchanged");
Check(ClientWandSummonEvent.Before(EffectId.Summon,caster,default),"ordinary summoning spell unchanged");
NetSession.Instance.Connection=host;
Check(ClientWandSummonEvent.Before(EffectId.Summon,caster,summonRef),"host summon still executes");
NetSession.Instance.Connection=null;
Check(ClientWandSummonEvent.Before(EffectId.Summon,caster,summonRef),"single player summon unchanged");
NetSession.Instance.Connection=client;caster.IsPC=false;
Check(ClientWandSummonEvent.Before(EffectId.Summon,caster,summonRef),"other caster excluded from local wand suppression");

NetSession.Instance.Connection=host;Act.CC=caster;Act.TC=caster;
var countBefore=host.Delta.Items.Count;
CharaActPerformEvent.OnCharaActPerform(new ActMeleeCounter(),true);
CharaActPerformEvent.OnCharaActPerform(new ActMeleeParry(),true);
Check(host.Delta.Items.Count==countBefore,"native nested retaliations never become invalid standalone action packets");
var invalid=CharaActPerformDelta.Create(new Act());
invalid.Apply(client);
Check(true,"zero-ID legacy/unknown action never reaches ACT.Create fallback");

foreach(var kind in new[]{"knowledge","item","strife"}) {
 host.Delta.Items.Clear();NetSession.Instance.Connection=host;
 var statue=new Thing{uid=8000};var shrine=new TraitShrine{owner=statue,Shrine=new(){id=kind}};statue.trait=shrine;
 var use=new CardOnUseDelta{Card=statue,RootCard=statue,User=caster};
 use.Apply(host);
 Check(shrine.Uses==1&&!shrine.SawReplay&&!statue.isOn,kind+": host executes fresh shrine simulation once");
 Check(host.Delta.Items.Count==3&&ReferenceEquals(host.Delta.Items[0],use)&&host.Delta.Items[1] is CardGenDelta&&host.Delta.Items[2] is ZoneAddCardDelta,
  kind+": activation then creation then ground placement use existing hooks");
 var generated=(CardGenDelta)host.Delta.Items[1];var placed=(ZoneAddCardDelta)host.Delta.Items[2];
 Check(generated.Card.uid==placed.Card.Uid&&placed.Pos.X==13&&placed.Pos.Z==14,kind+": same reward UID lands on shrine tile");
 use.Apply(host);
 Check(shrine.Uses==1&&host.Delta.Items.Count==3,kind+": exhausted shrine rejects repeated request before relay");
 NetSession.Instance.Connection=client;client.Delta.Items.Clear();var cleanupBefore=CardCache.DestroyQueued;
 use.Apply(client);
 Check(shrine.SawReplay&&client.Delta.Items.Count==0&&CardCache.DestroyQueued==cleanupBefore+1,
  kind+": client presentation retains replay guards and discards temporary reward");
 Check(!ElinDelta.IsApplying,kind+": replay depth restored");
}
NetSession.Instance.Connection=host;host.Delta.Items.Clear();
var brokenCard=new Thing{uid=9000};var brokenShrine=new TraitShrine{owner=brokenCard,Fail=true};brokenCard.trait=brokenShrine;
try {new CardOnUseDelta{Card=brokenCard,RootCard=brokenCard,User=caster}.Apply(host);throw new Exception("expected failure");}
catch(InvalidOperationException){}
Check(!ElinDelta.IsApplying,"shrine exception cannot leak simulation scope");
var otherCard=new Thing{uid=9001};var otherShrine=new TraitShrine{owner=otherCard,Shrine=new(){id="armor"}};otherCard.trait=otherShrine;
new CardOnUseDelta{Card=otherCard,RootCard=otherCard,User=caster}.Apply(host);
Check(otherShrine.SawReplay,"owner-local shrine retains existing replay context");
