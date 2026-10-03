using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;

int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
var host=new ElinNetHost();var client=new ElinNetClient();
var frey=new Chara{uid=1};var din=new Chara{uid=719};var npc=new Chara{uid=50};
foreach(var c in new[]{frey,din,npc})CardCache.Add(c);
host.ActiveRemoteCharas[1]=din;EClass.player.chara=frey;NetSession.Instance.Connection=host;
var stack=new Thing{uid=10,Num=5};CardCache.Add(stack);din.AddThing(stack);
CharaGiveGiftDelta Request(Thing item,Chara? target=null,Guid? id=null)=>new(){Id=id??Guid.NewGuid(),ZoneUid=1,From=din,To=target??frey,Thing=new(){Uid=item.uid,Num=1}};

// Reproduce the real client split: temporary UID, unchanged authoritative count.
NetSession.Instance.Connection=client;EClass.player.chara=din;
var shadow=new Thing{uid=0x40000001,Num=1};CardCache.Add(shadow);PendingSplit.Record(shadow,stack);
din.GiveGift(frey,shadow);
var request=(CharaGiveGiftDelta)client.Delta.Items.Single();
Check(request.Thing.Uid==stack.uid&&request.Thing.Num==1,"gift maps pending split to real source and one item");
Check(din.GiftCalls==0&&frey.Inventory.Count==0&&stack.Num==5,"client request neither gifts nor consumes real stack");
Check(CardCache.Delayed.Contains(shadow.uid),"temporary UI split scheduled for cleanup");

NetSession.Instance.Connection=host;EClass.player.chara=frey;Affinity.CC=npc;
request.Apply(host);
Check(stack.Num==4&&frey.Inventory.Single().Num==1,"host transfers exactly one from five");
Check(frey._affinity==10,"client-to-host affinity uses giver context rather than self guard");
Check(frey.EquipCalls==0&&frey.ClearCalls==0&&frey.nextUse==null,"human gift does not auto-equip, sell, or schedule consumption");
Check(EClass.pc==frey&&Affinity.CC==npc&&!SocialInteractions.Active&&!ElinDelta.IsApplying,"gift restores global context and replay state");
request.Apply(host);
Check(stack.Num==4&&din.GiftCalls==1,"repeated request ID cannot consume another item");
var authoritative=host.Delta.Items.OfType<SocialStateDelta>().Last();
frey._affinity=-999;NetSession.Instance.Connection=client;authoritative.Apply(client);authoritative.Apply(client);
Check(frey._affinity==10,"absolute affinity result is idempotent without another random roll");
NetSession.Instance.Connection=host;authoritative.Apply(host);Check(frey._affinity==10,"host does not accept client social result packets");

var last=new Thing{uid=11};CardCache.Add(last);din.AddThing(last);Request(last).Apply(host);
Check(last.parent==frey&&last.Num==1,"last item transfers its existing identity");
var before=stack.Num;frey.Full=true;Request(stack).Apply(host);frey.Full=false;
Check(stack.Num==before,"full recipient rejects before splitting");
frey.Heavy=true;Request(stack).Apply(host);frey.Heavy=false;Check(stack.Num==before,"overweight recipient rejects before splitting");
stack.Important=true;Request(stack).Apply(host);stack.Important=false;Check(stack.Num==before,"important item restriction retained");
var foreign=new Thing{uid=12,Num=3};CardCache.Add(foreign);frey.AddThing(foreign);Request(foreign).Apply(host);
Check(foreign.Num==3&&foreign.parent==frey,"cannot gift another player's item");
host.Accept=false;Request(stack).Apply(host);host.Accept=true;Check(stack.Num==before,"control handoff blocks stale gift input");
din.Distance=4;Request(stack).Apply(host);din.Distance=1;Check(stack.Num==before,"distant gift rejected");
var stale=Request(stack);EClass._zone.uid=2;stale.Apply(host);EClass._zone.uid=1;Check(stack.Num==before,"old-zone gift rejected");
new CharaGiveGiftDelta{Id=Guid.NewGuid(),ZoneUid=1,From=frey,To=npc,Thing=stack}.Apply(host);
Check(stack.Num==before,"spoofed giver rejected");
new CharaGiveGiftDelta{Id=Guid.NewGuid(),ZoneUid=1,From=din,To=frey,Thing=new(){Uid=stack.uid,Num=3}}.Apply(host);
Check(stack.Num==before,"request cannot accidentally give the entire stack");

Request(stack,npc).Apply(host);
Check(npc.Inventory.Single().Num==1&&npc.EquipCalls==1&&npc.ClearCalls==1&&npc.nextUse!=null,"NPC gift keeps native follow-up behavior");
din.GiftEffect=(c,t)=>{};var count=din.Inventory.Sum(t=>t.Num);Request(stack).Apply(host);din.GiftEffect=null;
Check(din.Inventory.Sum(t=>t.Num)==count,"native early-return restores detached live remainder");
din.GiftEffect=(c,t)=>throw new InvalidOperationException("native gift failed");
try{Request(stack).Apply(host);throw new Exception("expected failure");}catch(InvalidOperationException){}
din.GiftEffect=null;
Check(EClass.pc==frey&&Affinity.CC==npc&&!SocialInteractions.Active&&!ElinDelta.IsApplying,"exception restores context and replay depth");
Check(din.Inventory.Sum(t=>t.Num)==count,"exception does not strand split item");

var local=new Thing{uid=13};CardCache.Add(local);frey.AddThing(local);frey.GiveGift(din,local);
Check(local.parent==din&&din._affinity==10&&din.EquipCalls==0,"host-to-client gift uses same result scope and human protection");
host.Companions.Add(din);local=new Thing{uid=14};CardCache.Add(local);frey.AddThing(local);frey.GiveGift(din,local);
Check(din.EquipCalls==1&&din.nextUse==local,"Take a Break companion retains native gift behavior");host.Companions.Clear();

NetSession.Instance.Connection=client;EClass.player.chara=din;var talks=frey.TalkCalls;var interest=frey.interest;
frey.affinity.OnTalkRumor();var chat=(SocialTalkDelta)client.Delta.Items.Last();
Check(frey.TalkCalls==talks&&frey.interest==interest,"ordinary client talk submits without a second local roll");
NetSession.Instance.Connection=host;EClass.player.chara=frey;Affinity.CC=npc;var affinity=frey._affinity;
chat.Apply(host);chat.Apply(host);
Check(frey._affinity==affinity+1&&frey.interest==interest-10,"ordinary talk resolves once for actual speaker");
Check(EClass.pc==frey&&Affinity.CC==npc,"conversation restores host and static affinity context");
var chatResult=host.Delta.Items.OfType<SocialStateDelta>().Last();
Check(chatResult.Elements.Single().Owner.Uid==din.uid&&chatResult.Elements.Single().Element==291,"conversation result contains speaker's negotiation award only");
EClass.player.chara=din;din.elements.Get(291).vExp=0;chatResult.Apply(client);
Check(din.elements.Get(291).vExp==20,"authoritative negotiation XP reaches the owning client");EClass.player.chara=frey;
interest=frey.interest;SocialInteractions.Talk(din,frey);
Check(frey.interest==interest-10,"AutoAct shared resolver executes the same native conversation");
frey.interest=0;talks=frey.TalkCalls;SocialInteractions.Talk(din,frey);Check(frey.TalkCalls==talks,"exhausted interest cannot grant extra conversation affinity");
frey.SetStr(72,"apple");frey.SetStr(73,"food");var state=SocialStateDelta.Capture(frey);frey.SetStr(72,null);frey.SetStr(73,null);state.Apply(client);
Check(frey.GetStr(72)=="apple"&&frey.GetStr(73)=="food","gift favourite discovery uses correct vanilla fields");

using(SocialInteractions.Begin(din,frey,gift:true)) din.SetAI(new AI_Massage{target=frey});
var ticket=host.Delta.Items.OfType<SocialStateDelta>().Last();
Check(ticket.Tasks is [{ArmPillow:false}]&&din.TaskStarts==0,"ticket captures requested client task before GoalRemote replaces it");
NetSession.Instance.Connection=client;EClass.player.chara=din;ticket.Apply(client);ticket.Apply(client);
Check(din.TaskStarts==1&&din.ai is AI_Massage{target:var massageTarget}&&massageTarget==frey,"ticket starts native action on owner exactly once");
NetSession.Instance.Connection=host;EClass.player.chara=frey;
using(SocialInteractions.Begin(frey,din,gift:true)) din.SetAI(new AI_ArmPillow{target=frey});
var pillow=host.Delta.Items.OfType<SocialStateDelta>().Last();
NetSession.Instance.Connection=client;EClass.player.chara=din;pillow.Apply(client);
Check(din.TaskStarts==2&&din.ai is AI_ArmPillow,"pillow targets recipient's client with correct native action");
var blocked=SocialStateDelta.Capture(frey,tasks:ticket.Tasks);PlayerControl.LocalInputBlocked=true;blocked.Apply(client);PlayerControl.LocalInputBlocked=false;
Check(din.TaskStarts==2,"late ticket result cannot take control during a break handoff");
var oldZone=SocialStateDelta.Capture(frey,tasks:ticket.Tasks);EClass._zone.uid=2;oldZone.Apply(client);EClass._zone.uid=1;
Check(din.TaskStarts==2,"old-zone ticket does not start an action in the new map");
var massageArgs=ElinTogether.Models.AI.AIMassageArgs.Create(new AI_Massage{target=frey});
Check(massageArgs.CreateSubAct() is AI_Massage{target:var restored}&&restored==frey,"massage task serializer retains target for normal MP task execution");
EClass.player.chara=frey;

NetSession.Instance.Connection=null;local=new Thing{uid=15};frey.AddThing(local);frey.GiveGift(npc,local);
Check(local.parent==npc&&!SocialInteractions.Active,"single-player gift remains vanilla");
Console.WriteLine($"{checks} social checks passed");
