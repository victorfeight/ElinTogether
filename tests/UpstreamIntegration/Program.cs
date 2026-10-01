using System.Reflection;
using ElinTogether;
using ElinTogether.Models;
using ElinTogether.Models.AI;
using ElinTogether.Net;
using ElinTogether.Patches;
int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name+": "+string.Join(",",Trace.Events));checks++;Console.WriteLine("PASS "+name);}
var host=new ElinNetHost();var client=new ElinNetClient();var actor=new Chara{uid=1,IsRemotePlayer=true};var item=new Thing{uid=2,parent=actor};
CharaBuildDelta Packet(params ElinDelta[] effects)=>new(){Owner=actor,Held=item,Pos=new(),Dir=0,Altitude=0,BridgeHeight=0,AutoActRequestId=Guid.NewGuid(),DeltaList=[..effects]};
var packet=Packet(new Effect("count"),new Effect("ground"));TaskBuild.Build=t=>t.target=new Thing{uid=3};packet.TargetUid=30;
packet.Apply(client);
Check(Trace.Events.SequenceEqual(new[]{"select","build","rebind","count","ground","ack:True"}),"build then rebind then all side effects before AutoAct resumes");
Trace.Events.Clear();CardCache.ThrowRebind=true;
try{packet.Apply(client);}catch(Exception){}
Check(Trace.Events.Contains("ground")&&Trace.Events.Last()=="ack:False","UID reconciliation failure cannot acknowledge AutoAct success");CardCache.ThrowRebind=false;
Trace.Events.Clear();packet.Held.Value=null;packet.Apply(client);
Check(Trace.Events.SequenceEqual(new[]{"count","ground","ack:False"}),"missing held item still receives results and stops AutoAct");
Trace.Events.Clear();item.parent=null;packet=Packet(new Effect("ground"));packet.Apply(client);
Check(Trace.Events.SequenceEqual(new[]{"ground","ack:False"}),"already-ground item receives results without replaying build");item.parent=actor;
Trace.Events.Clear();TaskBuild.Throw=true;try{Packet(new Effect("ground")).Apply(client);}catch(Exception){}
Check(Trace.Events.SequenceEqual(new[]{"build","ground","ack:False"}),"native exception preserves authoritative results and releases AutoAct");TaskBuild.Throw=false;
Trace.Events.Clear();Packet(new Effect("bad",true),new Effect("ground")).Apply(client);
Check(Trace.Events.Contains("desync")&&Trace.Events[^2]=="ground"&&Trace.Events[^1]=="ack:False","one failed side effect reports desync but remaining results land");
Trace.Events.Clear();TaskBuild.Build=t=>CharaProgressCompleteEvent.Into!.Add(new Effect("captured"));packet=Packet(new Effect("untrusted"));packet.Apply(host);
Check(host.Delta.Sent.Count==1&&packet.DeltaList.Count==1&&((Effect)packet.DeltaList[0]).Name=="captured","host replaces request side effects with its own captured results");
Check(!Trace.Events.Contains("captured")&&!Trace.Events.Contains("hold"),"host does not replay captured results or invoke remote HoldCard");
Trace.Events.Clear();packet.Held.Value=null;packet.Apply(host);
Check(host.Delta.Sent.Last() is AutoActStepDelta{Reply:true,Success:false},"host rejection answers AutoAct instead of leaving it waiting");
TaskBuild.Build=null;
Trace.Events.Clear();TaskBuild.Reject=true;Packet(new Effect("result")).Apply(client);TaskBuild.Reject=false;
Check(Trace.Events.SequenceEqual(new[]{"build","result","ack:False"}),"silent native rejection cannot acknowledge build success");
Trace.Events.Clear();TaskBuild.Reject=true;Packet().Apply(host);TaskBuild.Reject=false;
Check(host.Delta.Sent.Last() is AutoActStepDelta{Reply:true,Success:false},"silent host build rejection returns explicit failure reply");
Trace.Events.Clear();TaskBuild.Rotate=true;Packet().Apply(client);TaskBuild.Rotate=false;
Check(Trace.Events.Last()=="ack:True","native early block rotation remains a successful build");
foreach(var invalid in new[]{"distance","bounds","destroyed","foreign item","foreign actor"}) {
 Trace.Events.Clear();var bad=Packet(new Effect("result"));
 if(invalid=="distance")bad.Pos.Range=2;
 if(invalid=="bounds")bad.Pos.IsInActiveMapBounds=false;
 if(invalid=="destroyed")item.isDestroyed=true;
 if(invalid=="foreign item")item.parent=new Chara();
 if(invalid=="foreign actor")bad.OriginPeer=99;
 bad.Apply(invalid=="foreign actor"?host:client);
 Check(!Trace.Events.Contains("build")&&!Trace.Events.Contains("select")&&!Trace.Events.Contains("hold"),"reject "+invalid+" before native build or held mutation");
 item.isDestroyed=false;item.parent=actor;
}
TaskBuild.Build=null;NetSession.Instance.Connection=client;actor.IsPC=true;AutoActTaskBridge.Active=true;
Check(CharaBuildDelta.Create(new(){owner=actor,held=item}).AutoActRequestId!=Guid.Empty,"client AutoAct build carries a completion identifier");NetSession.Instance.Connection=host;
Check(CharaBuildDelta.Create(new(){owner=actor,held=item}).AutoActRequestId==Guid.Empty,"host builds do not allocate client completion tickets");
var keys=typeof(CharaBuildDelta).GetProperties().Select(p=>p.GetCustomAttribute<MessagePack.KeyAttribute>()?.Key).Where(k=>k!=null).ToArray();
Check(keys.Distinct().Count()==keys.Length&&typeof(CharaBuildDelta).GetProperty("AutoActRequestId")!.GetCustomAttribute<MessagePack.KeyAttribute>()!.Key==7&&typeof(CharaBuildDelta).GetProperty("DeltaList")!.GetCustomAttribute<MessagePack.KeyAttribute>()!.Key==8,"AutoAct and bundled results use distinct stable field numbers");
string output="";
host.ActiveRemoteCharas[2]=actor;Msg.currentColor=new(){r=.7f};
using(MsgRelayContext.RedirectTo(actor)) {
 bool visible=false;MsgRelayContext.ShowRecipientMessage(actor,ref visible);
 Check(visible,"personal recipient can speak while off the host screen");
 visible=false;MsgRelayContext.ShowRecipientMessage(new Chara(),ref visible);
 Check(!visible,"personal relay does not expose unrelated hidden actor messages");
 actor.isDead=true;visible=false;MsgRelayContext.ShowRecipientMessage(actor,ref visible);actor.isDead=false;
 Check(!visible,"personal relay preserves dead actor message suppression");
 Check(!MsgRelayContext.OnSayRaw("craft",ref output)&&host.Messages.Last().Peer==2&&host.Messages.Last().Message.R==.7f,"personal relay preserves recipient and color");
 using(MsgRelayContext.Suppress())Check(!MsgRelayContext.OnSayRaw("toggle",ref output)&&host.Messages.Count==1,"nested replay suppression sends no duplicate");
 using(MsgRelayContext.RedirectTo(0))Check(MsgRelayContext.OnSayRaw("host",ref output),"nested host operation stays local");
 Check(!MsgRelayContext.OnSayRaw("first craft",ref output)&&host.Messages.Count==2,"nested scopes restore previous personal recipient");
 host.Deliver=false;Msg.currentColor=new(){r=.9f};Msg.alwaysVisible=true;
 Check(MsgRelayContext.OnSayRaw("fallback",ref output)&&Msg.currentColor.r==.9f&&Msg.alwaysVisible,"failed delivery preserves local message and presentation");host.Deliver=true;
}
Check(MsgRelayContext.OnSayRaw("ordinary",ref output),"scope exit restores ordinary messaging");
try{using var scope=MsgRelayContext.RedirectTo(2);throw new Exception();}catch(Exception){}
Check(MsgRelayContext.OnSayRaw("after failure",ref output),"exception unwinding restores message scope");
Msg.ignoreAll=true;using(MsgRelayContext.RedirectTo(2))Check(MsgRelayContext.OnSayRaw("ignored",ref output),"vanilla ignoreAll is respected");Msg.ignoreAll=false;
var original=new Thing{uid=40,Num=10};var split=new Thing{uid=-2,Num=1};CardCache.Cards[40]=original;PendingSplit.Record(split,original);actor.held=split;
var eat=AIEatArgs.Create(new AI_Eat(),actor);
Check(eat.Target!.Uid==40&&eat.Target.Num==1,"held split food sends host source UID and split amount");var second=new Thing{uid=-3,Num=1};PendingSplit.Record(second,split);
Check(AIEatArgs.Create(new AI_Eat{target=second},actor).Target!.Uid==40,"chained food splits resolve original host stack");
Check(!client.Accept(new NetSessionRules{UseSharedSpeed=false,UseTurnBasedCombat=true}),"rules are rejected before host version validation");client.Phase(NetHandshakePhase.AwaitingIntegrity);
Check(client.Accept(new NetSessionRules{UseSharedSpeed=false,UseTurnBasedCombat=true,AllowMoongateTheft=true}),"host settings accepted during save handshake");
Check(!client.Accept(new NetSessionRules{UseSharedSpeed=false,UseTurnBasedCombat=true},new ElinTogether.Net.Steam.Peer()),"another peer cannot set joining client's rules");
NetSession.Instance.Connection=client;ElinDelta.IsApplying=true;client.Delta.Sent.Clear();
actor.LV=5;actor.exp=12;actor.feat=3;
CharaLevelEvent.OnSetLevelEnd(actor,(4,0));CharaFeatPointEvent.OnSetFeatEnd(actor,2);
Check(client.Delta.Sent.OfType<CharaLevelDelta>().Count()==1&&client.Delta.Sent.OfType<CharaFeatPointDelta>().Count()==1,"local level and feat awards during host-approved replay are reported");
var remoteActor=new Chara{IsPC=false,IsRemotePlayer=true,LV=5,feat=3};
CharaLevelEvent.OnSetLevelEnd(remoteActor,(4,0));CharaFeatPointEvent.OnSetFeatEnd(remoteActor,2);
CharaLevelEvent.OnSetLevelEnd(actor,(5,12));CharaFeatPointEvent.OnSetFeatEnd(actor,3);
Check(client.Delta.Sent.Count==2,"remote actor updates and unchanged values do not echo progression");
Check(!CardAddExpMirrorGuard.OnAddExp(remoteActor)&&!CardLevelUpMirrorGuard.OnLevelUp(remoteActor),"remote mirror cannot calculate a second XP or level award");
var wake=new CharaSleepDelta{Power=10,Days=1};
foreach(var mode in new[]{PlayerControlMode.Companion,PlayerControlMode.Resuming,PlayerControlMode.Human}) {
 client.ControlMode=mode;EClass.pc=new Chara{conSleep=new()};EClass.player=new();var closed=SleepSynchronizationContext.Closed;
 wake.Apply(client);
 bool human=mode==PlayerControlMode.Human;
 Check(EClass.pc.Sleeps==(human?1:0)&&EClass.player.Dreams==(human?1:0)&&EClass.pc.conSleep.Kills==(human?1:0),"sleep ownership for "+mode+": only human client simulates personal recovery/rewards");
 Check(SleepSynchronizationContext.Closed==closed+1,"wake closes sleep presentation for "+mode);
}
EClass.pc=new Chara{isDead=true,conSleep=new()};EClass.player=new();wake.Apply(client);
Check(EClass.pc.Sleeps==0&&EClass.player.Dreams==0&&EClass.pc.conSleep.Kills==0,"dead client receives no sleep rewards");
EClass.pc.isDead=false;wake.Apply(host);
Check(EClass.pc.Sleeps==0&&EClass.player.Dreams==0,"host does not replay client sleep reward packet");
NetSession.Instance.Connection=host;actor.isDead=false;
var appliance=new Card();var applianceTrait=new Trait{owner=appliance};var beforeMessages=host.Messages.Count;
using(MsgRelayContext.RedirectTo(2)) {
 CardToggleEvent.OnToggle(applianceTrait,out var toggleState);
 try {
  bool visible=false;MsgRelayContext.ShowRecipientMessage(appliance,ref visible);
  Check(visible,"scoped appliance can explain rejection while off the host screen");
  string ignored="";Check(!MsgRelayContext.OnSayRaw("notEnoughElectricity",ref ignored)&&host.Messages.Count==beforeMessages+1,"failed toggle explanation reaches requester exactly once");
  CardToggleEvent.OnToggleEnd(applianceTrait,false,toggleState);
  CardToggleEffectMessageEvent.Before(out var effectScope);
  try {Check(!MsgRelayContext.OnSayRaw("toggle_ele",ref ignored)&&host.Messages.Count==beforeMessages+1,"successful toggle presentation is suppressed only in effect scope");}
  finally {CardToggleEffectMessageEvent.After(effectScope);}
  Check(MsgRelayContext.IsRedirecting,"effect scope restores outer recipient");
 } finally {CardToggleEvent.OnToggleCleanup(toggleState);}
 bool outside=false;MsgRelayContext.ShowRecipientMessage(appliance,ref outside);
 Check(!outside,"appliance visibility ends with its operation");
}
CardToggleEffectMessageEvent.Before(out var localEffect);
try {string ignored="";Check(MsgRelayContext.OnSayRaw("host_toggle",ref ignored),"ordinary host toggle messages stay local");}finally {CardToggleEffectMessageEvent.After(localEffect);}
Trace.Events.Clear();TaskBuild.Build=t=>CharaProgressCompleteEvent.BuildFailed=true;Packet().Apply(host);
Check(host.Delta.Sent.Last() is AutoActStepDelta{Reply:true,Success:false},"caught nested build exception cannot become success acknowledgement");CharaProgressCompleteEvent.BuildFailed=false;TaskBuild.Build=null;
Console.WriteLine($"{checks} checks passed");
class Effect(string name,bool fail=false):ElinDelta {public string Name=name;protected override void OnApply(ElinNetBase n){Trace.Events.Add(Name);if(fail)throw new Exception("side effect failure");}}
