using ElinTogether.Models;
using ElinTogether.Models.AI;
using ElinTogether.Net;
using ElinTogether.Patches;
using AutoActMod.Actions;
var host=new ElinNetHost();var client=new ElinNetClient();
var actor=new Chara{uid=719,IsPC=false,pos=new(1,1)};host.ActiveRemoteCharas[1]=actor;
int checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
Card Tool(Trait trait,int charges=5){var c=new Card{uid=123,trait=trait,root=actor,c_charges=charges};trait.owner=c;actor.held=c;return c;}
AutoActStepDelta Request(AutoActStepDelta.Step kind,Point? p=null,Card? target=null)=>new(){RequestId=Guid.NewGuid(),Owner=new(actor),Tool=actor.held,Kind=kind,Pos=p??new(2,1),Target=target,ZoneUid=7};
NetSession.Instance.Connection=host;
var broom=Tool(new TraitBroom());var clean=Request(AutoActStepDelta.Step.Clean);clean.Apply(host);
Check(host.Messages.Last() is (1,{Text:"clean"}),"offscreen clean confirmation routed to requesting peer");
Check(clean.Success&&actor.stamina.value==49&&new Point(2,1).cell.decal==0,"host runs installed custom clean and records stamina cost");
new AutoActStepDelta{RequestId=clean.RequestId,Owner=clean.Owner,Tool=clean.Tool,Kind=clean.Kind,Pos=clean.Pos,ZoneUid=7}.Apply(host);
Check(actor.stamina.value==49,"fresh duplicate packet does not repeat effect");
actor.IsPC=true;actor.stamina.value=50;new Point(2,1).cell.decal=2;clean.Apply(client);
Check(new Point(2,1).cell.decal==0&&actor.stamina.value==49,"reply applies authoritative tile and client cost");actor.IsPC=false;
Tool(new TraitToolWaterCan());var water=Request(AutoActStepDelta.Step.Water);water.Apply(host);
Check(host.Messages.Last() is (1,{Text:"water_farm"}),"offscreen water confirmation routed to requesting peer");
Check(water.Success&&water.Charges==4&&water.Tiles.Count==1&&water.Tiles[0].Watered,"custom water returns authoritative charge and tile changes");
new Point(2,1).cell.isWatered=false;actor.held!.c_charges=5;water.Apply(client);
Check(new Point(2,1).cell.isWatered&&actor.held.c_charges==4,"water reply reconciles local can and terrain");
var refill=Request(AutoActStepDelta.Step.Refill);refill.Apply(host);Check(refill.Success&&actor.held.c_charges==20,"refill checks source and refills host can");
Check(host.Messages.Last() is (1,{Text:"water_draw"}),"refill confirmation routed to requesting peer");
var badSource=Request(AutoActStepDelta.Step.Refill,new(1,1));badSource.Apply(host);Check(!badSource.Success,"refill without water source rejected");
var other=new Chara{uid=9};var wrong=Request(AutoActStepDelta.Step.Refill);wrong.OriginPeer=9;wrong.Apply(host);Check(!wrong.Success,"unknown sender rejected");
actor.held.c_charges=7;wrong.Apply(client);Check(actor.held.c_charges==7,"rejection cannot zero local tool charges");
var forged=Request(AutoActStepDelta.Step.Refill);forged.OriginPeer=9;forged.HasTargetState=true;forged.StaminaChange=100;
forged.Tiles.Add(new(){Pos=new Point(1,1),Watered=true,Decal=0});forged.Apply(host);
Check(!forged.Success&&forged.Tiles.Count==0&&!forged.HasTargetState&&forged.StaminaChange==0,"rejection strips caller-supplied reply state");
var transferred=Request(AutoActStepDelta.Step.Refill);actor.held.root=other;transferred.Apply(host);Check(!transferred.Success,"transferred tool rejected");actor.held.root=actor;
var distant=Request(AutoActStepDelta.Step.Refill,new(8,8));distant.Apply(host);Check(!distant.Success,"distant requests rejected");
var dead=Request(AutoActStepDelta.Step.Refill);actor.isDead=true;dead.Apply(host);actor.isDead=false;Check(!dead.Success,"dead actor rejected");
var stale=Request(AutoActStepDelta.Step.Refill);EClass._zone.uid=8;stale.Apply(host);EClass._zone.uid=7;Check(!stale.Success,"stale zone rejected");
var trapCard=new Card{uid=500,pos=new(2,1)};var trap=new TraitTrap{owner=trapCard};trapCard.trait=trap;
var disarm=Request(AutoActStepDelta.Step.Disarm,target:trapCard);var pc=EClass.game.player.chara;disarm.Apply(host);
Check(Msg.Local.Contains("trap")&&!host.Messages.Any(m=>m.Message.Text=="trap"),"disarm world messages retain normal routing");
Check(disarm.Success&&TraitTrap.Attempts==1&&EClass.game.player.chara==pc,"disarm runs once with requester context and restores host PC");
var npc=new Chara{uid=600,pos=new(2,1)};var chat=Request(AutoActStepDelta.Step.Chat,target:npc);var affinity=Affinity.CC;chat.Apply(host);
Check(chat.Success&&npc.interest==40&&npc._affinity==1&&EClass.game.player.chara==pc&&Affinity.CC==affinity,"chat resolves host affinity and restores global context");
Check(SocialInteractions.Calls==1,"AutoAct conversation delegates to shared social resolver");
var potCard=Tool(new TraitToolWaterPot());var pour=new AutoActPourWater.SubActPourWater{pos=new(2,3),pot=(TraitToolWaterPot)potCard.trait!,targetCount=4};
var rebuilt=TaskPourWaterArgs.Create(pour).CreateSubAct();
Check(rebuilt is AutoActPourWater.SubActPourWater{targetCount:4},"pouring serializer preserves custom type and depth");
Check(TaskPourWaterArgs.Create(new TaskPourWater{pos=new(2,3),pot=pour.pot}).CreateSubAct().GetType()==typeof(TaskPourWater),"vanilla pouring stays vanilla");
NetSession.Instance.Connection=host;Check(!AutoActAllyGuard.Before(actor)&&AutoActAllyGuard.Before(npc),"host ally assignment excludes human client but allows NPC");
NetSession.Instance.Connection=client;Check(!AutoActAllyGuard.Before(npc)&&!AutoActAllyGuard.Before(actor),"client ally assignment leaves NPC authority on host");
NetSession.Instance.Connection=null;Check(AutoActAllyGuard.Before(npc),"single player ally automation unchanged");
// Production coroutine waits for the result before proceeding to another target.
NetSession.Instance.Connection=client;actor.IsPC=true;actor.ai=new();actor.held=broom;
var localClean=new AutoActClean.SubActClean{owner=actor,pos=new(2,1)};
IEnumerable<AIAct.Status> sequence=[];
Check(!AutoActCustomActions.Before(localClean,ref sequence),"custom clean uses host request coroutine");
using(var run=sequence.GetEnumerator()){
 var guard=0;var before=client.Delta.Items.Count;
 while(client.Delta.Items.Count==before&&++guard<20) {if(!run.MoveNext()) throw new Exception("Coroutine ended before request");}
 Check(client.Delta.Items.Count==before+1,"custom step submits once after navigation/delay");
 var pending=(AutoActStepDelta)client.Delta.Items.Last();
 Check(run.MoveNext(),"custom step waits while acknowledgement is pending");
 AutoActCustomActions.Complete(pending.RequestId,true);
 Check(!run.MoveNext(),"custom step completes after successful acknowledgement");
}
Guid buildId=Guid.Empty;bool advanced=false;var build=new TaskBuild{owner=actor};
IEnumerable<AIAct.Status> Build(){yield return AIAct.Status.Running;buildId=AutoActCustomActions.BeginBuild(build);}
using(var run=AutoActCustomActions.WaitForBuild(build,Build()).GetEnumerator()){
 Check(run.MoveNext()&&run.MoveNext(),"build wrapper waits even when vanilla iterator finishes");
 AutoActCustomActions.Complete(buildId,true);advanced=!run.MoveNext();
 Check(advanced,"build wrapper releases after authoritative replay");
}
var dig=(TaskDig)TaskDigArgs.Create(new(){pos=new(3,4),mode=TaskDig.Mode.Ore}).CreateSubAct();
Check(dig.pos.x==3&&dig.mode==TaskDig.Mode.Ore,"dig serializer preserves mode and destination");
Check(((TaskPlow)TaskPlowArgs.Create(new(){pos=new(4,5)}).CreateSubAct()).pos.z==5,"plow serializer preserves destination");
Check(((TaskDrawWater)TaskDrawWaterArgs.Create(new(){pos=new(2,1),pot=pour.pot}).CreateSubAct()).pot==pour.pot,"draw-water serializer binds real pot");
Check(((AI_Read)AIReadArgs.Create(new(){target=npc}).CreateSubAct()).target==npc,"read serializer preserves selected book identity");
Check(((AI_Shear)AIShearArgs.Create(new(){target=npc}).CreateSubAct()).target==npc,"shear serializer preserves target");
Check(((AI_Slaughter)AISlaughterArgs.Create(new(){target=npc}).CreateSubAct()).target==npc,"slaughter serializer preserves target");
Check(((AI_Steal)AIStealArgs.Create(new(){target=npc}).CreateSubAct()).target==npc,"steal serializer preserves target");
Check(((AI_OpenLock)AIOpenLockArgs.Create(new(){target=npc}).CreateSubAct()).target==npc,"unlock serializer preserves target");
Check(AIFuckArgs.Create(new AI_TendAnimal{target=npc}).CreateSubAct() is AI_TendAnimal {target:var t}&&t==npc,"brushing preserves tame subtype");
Check(new AIPracticeDummyArgs().CreateSubAct() is ElinTogether.Elements.DelegateProgress {ActType:var typ}&&typ==typeof(AI_PracticeDummy),"training uses progress proxy to avoid duplicate host attacks");
NetSession.Instance.Connection=host;actor.IsPC=false;Tool(new TraitBroom());new Point(2,1).cell.decal=2;AutoActClean.Throw=true;
var throws=Request(AutoActStepDelta.Step.Clean);throws.Apply(host);AutoActClean.Throw=false;
Check(!throws.Success&&!MsgRelayContext.IsRedirecting,"throwing custom task restores message routing and returns failure");
Console.WriteLine($"{checks} checks passed; simulated game/mod boundary, not Unity acceptance.");
