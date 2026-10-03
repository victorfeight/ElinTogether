using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
using Mono.Cecil;
int checks=0;
void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;Console.WriteLine("PASS "+why);}
var host=new ElinNetHost();var client=new ElinNetClient();
var h=new Chara{uid=1,id="host"};var c=new Chara{uid=719,id="client"};var victim=new Chara{uid=90,id="creature"};
EClass._map.charas.AddRange([h,c,victim]);host.ActiveRemoteCharas[2]=c;
Act Spell(int id,string alias){var a=new Act{id=id,source=new(){alias=alias}};c.elements.dict[id]=new(){act=a};return a;}
var milk=Spell(222223802,"TpForceMilking");var eggs=Spell(222223801,"TpForceOvulation");var brain=Spell(TpSpellBridge.Brainwash,"TpBrainwash");
HarmonyLib.AccessTools.Available=false;Check(!TpSpellBridge.IsManaged(milk),"absent optional mod leaves vanilla actions alone");HarmonyLib.AccessTools.Available=true;
Check(TpSpellBridge.IsManaged(milk)&&!TpSpellBridge.IsManaged(new(){id=222223802,source=new(){alias="OtherMod"}}),"exact spell identity requires installed mod and matching alias");
void Client(){NetSession.Instance.Connection=client;EClass.player.chara=c;}
void Host(){NetSession.Instance.Connection=host;EClass.player.chara=h;}
TpSpellRequestDelta Request(Act act){Client();var count=client.Delta.Items.Count;Check(act.Perform(c,victim,new Point(4,5)),"client casting succeeds at native cost boundary");var request=(TpSpellRequestDelta)client.Delta.Items[count];request.OriginPeer=2;return request;}
int products=0;bool sawIdentity=false,sawSimulation=false;
Act.Native=a=>{sawIdentity=Act.CC==c&&c.IsPC&&!h.IsPC&&MsgRelayContext.Owner==c;sawSimulation=ElinDelta.Depth==0;products++;TpSpellBridge.Current!.ObserveGeneration(new(){uid=products,id=a==milk?"_milk":"egg_fertilized",c_idRefCard="loytel"});};
var req=Request(milk);Check(products==0&&Act.Executions==0,"client never generates predicted milk");
Host();req.Apply(host);Check(products==1&&sawIdentity&&sawSimulation,"host runs original effect once in caster context with outcome hooks enabled");
Check(EClass.pc==h&&TpSpellBridge.Current==null&&MsgRelayContext.Owner==null&&TpSpellBridge.RequestId==Guid.Empty,"caster context and correlation restored after execution");
req.Apply(host);Check(products==1,"duplicate request cannot generate a second milk");
Client();req.Apply(client);Check(products==1,"request received by a client cannot execute effects");
var eggReq=Request(eggs);Host();eggReq.Apply(host);Check(products==2,"ovulation uses same authoritative route without rerolling on client");
var output=(TpSpellResultDelta)host.Delta.Items.Last();Client();output.Apply(client);Check(products==2,"result application never invokes Tp spell again");
Host();var spoof=new TpSpellRequestDelta{Id=Guid.NewGuid(),OriginPeer=2,Owner=h,ActId=milk.id,ZoneUid=7,Pos=new(){X=1,Z=1}};spoof.Apply(host);Check(products==2,"peer cannot impersonate host");
var oldZone=new TpSpellRequestDelta{Id=Guid.NewGuid(),OriginPeer=2,Owner=c,ActId=milk.id,ZoneUid=8,Pos=new(){X=1,Z=1}};oldZone.Apply(host);Check(products==2,"stale-zone request rejected");
var badPoint=new TpSpellRequestDelta{Id=Guid.NewGuid(),OriginPeer=2,Owner=c,ActId=milk.id,ZoneUid=7,Pos=new(){X=-1,Z=1}};badPoint.Apply(host);Check(products==2,"out of bounds spell rejected");
var breakReq=Request(milk);Host();host.Accept=false;breakReq.Apply(host);host.Accept=true;Check(products==2,"spectator or revoked input cannot cast");
var absent=Spell(222223803,"TpForceSqueeze");var unknown=Request(absent);c.elements.dict.Remove(absent.id);Host();unknown.Apply(host);Check(products==2,"unknown spell on host cannot be requested");
var dead=Request(milk);Host();c.isDead=true;dead.Apply(host);c.isDead=false;Check(products==2,"dead caster cannot execute");
Client();var queued=client.Delta.Items.Count;PlayerControl.LocalInputBlocked=true;Check(!milk.Perform(c,null,new Point(1,1)),"blocked local input does not claim a cast");PlayerControl.LocalInputBlocked=false;ElinDelta.IsRemoteStateLanding=true;milk.Perform(c,null,new Point(1,1));ElinDelta.IsRemoteStateLanding=false;Check(client.Delta.Items.Count==queued,"remote replay does not queue another cast");
Act.Native=a=>victim.c_uidMaster=Act.CC.uid;
var brainReq=Request(brain);Host();brainReq.Apply(host);var brainResult=(TpSpellResultDelta)host.Delta.Items.Last();Check(brainResult.Characters.Count==1&&victim.c_uidMaster==c.uid,"Brainwash result captures native minion owner");
victim.c_uidMaster=0;Client();brainResult.Apply(client);Check(victim.c_uidMaster==c.uid,"client receives authoritative minion conversion");
var tp=Spell(TpSpellBridge.Teleport,"TpTeleportG");Act.Native=a=>{Check(TpSpellBridge.AllowsHostMovement,"host can move a normally locked remote player during teleport");Act.CC.pos.Set(Act.TP);};
var tpReq=Request(tp);var old=c.pos.Copy();Host();tpReq.Apply(host);var tpResult=(TpSpellResultDelta)host.Delta.Items.Last();Check(tpResult.Moves.Count==1&&!TpSpellBridge.AllowsHostMovement,"teleport captures actual position and releases movement override");
c.pos.Set(old);Client();tpResult.Apply(client);Check(c.pos.Equals(new Point(4,5))&&EClass.player.haltMove&&c.ai.Cancels>0,"resolved teleport applies to casting client's own character");
c.pos.Set(old);Host();tpResult.Apply(host);Check(c.pos.Equals(old),"client cannot inject a host result");
Client();EClass._zone.uid=99;tpResult.Apply(client);Check(c.pos.Equals(old),"old-map movement result ignored");EClass._zone.uid=7;
var rain=Spell(222223202,"TpBringRain");Act.Native=a=>WeatherStateSnapshot.Current=3;var rainReq=Request(rain);Host();rainReq.Apply(host);var rainResult=(TpSpellResultDelta)host.Delta.Items.Last();WeatherStateSnapshot.Current=0;Client();rainResult.Apply(client);Check(WeatherStateSnapshot.Current==3,"weather uses authoritative existing snapshot");
var sense=Spell(TpSpellBridge.Sense,"TpSenceObject");EClass._map.cells.AddRange([new(){x=2,z=3,isSeen=true,HasObj=true},new(){x=1,z=1,isSeen=true}]);Act.Native=a=>{};var senseReq=Request(sense);Host();senseReq.Apply(host);var reveal=host.Delta.Items.OfType<MapRevealDelta>().Last();Check(reveal.Cells.SequenceEqual(new[]{32}),"sense shares targeted cells even when host already knew them");
var dig=Spell(222223002,"TpDrilling");Act.Native=a=>{Check(PathTerrainSync.Map==EClass._map,"mining uses existing scoped terrain capture");throw new InvalidOperationException("native failure");};var digReq=Request(dig);Host();var priorCC=Act.CC;var priorPoint=Act.TP.Copy();try{digReq.Apply(host);}catch(InvalidOperationException){}
Check(EClass.pc==h&&TpSpellBridge.Current==null&&PathTerrainSync.Map==null&&MsgRelayContext.Owner==null&&Act.CC==priorCC&&Act.TP.Equals(priorPoint)&&ElinDelta.Depth==0,"native exception restores actor, terrain, act globals and simulation depth");
var ret=Spell(TpSpellBridge.Return,"TpReturnG");Act.Native=a=>EClass.player.returnInfo=new();var retReq=Request(ret);Host();retReq.Apply(host);Check(EClass.player.returnInfo!=null&&EClass.pc==h,"Return leaves shared travel state on host while restoring local player");
Act.Native=a=>{};Host();h.elements.dict[milk.id]=new(){act=milk};var hostCalls=Act.Executions;milk.Perform(h,null,new Point(1,1));Check(Act.Executions==hostCalls+1&&EClass.pc==h,"host local cast executes once through same outcome capture");
NetSession.Instance.Connection=null;var soloCalls=Act.Executions;milk.Perform(h,null,new Point(1,1));Check(Act.Executions==soloCalls+1&&TpSpellBridge.Current==null,"solo behavior remains native");
// Verify the installed mod still has the interface and exact identities we adapt.
using var module=ModuleDefinition.ReadModule(args[0]);var mod=module.Types.Single(t=>t.FullName=="TpMagicAppendix.MagicAppendix");
foreach(var name in new[]{"Brainwash","ForceOvulation","ForceMilking","ForceSqueeze","Cultivate","Demolition","Drilling","PullingRug","Gathering","CollectAll","SenceObject","ChangeWeather","ReturnG","TeleportG","EternalForceBlizzard"}){
 var method=mod.Methods.Single(m=>m.Name==name);Check(method.IsStatic&&method.Parameters[0].ParameterType.Name=="Act"&&method.Parameters[1].ParameterType.FullName=="System.Int32"&&method.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.Name=="get_IsPC"),"installed Tp method and local-player guard: "+name);
}
var rows=module.Types.Single(t=>t.Name=="Source_MagicAppendix").Methods.Where(m=>m.Name.StartsWith("Tp")&&m.HasBody).ToArray();
foreach(var method in rows){var instructions=method.Body.Instructions;var alias=(string)instructions.First(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldstr&&((string)i.Operand).StartsWith("Tp")).Operand;var id=(int)instructions.First(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldc_I4&&i.Operand is int n&&n>=222223000).Operand;Check(TpSpellBridge.IsManaged(new(){id=id,source=new(){alias=alias}}),"installed spell ID/alias covered: "+alias);}
Check(rows.Length==17,"all 17 installed Tp spell identities covered");
Console.WriteLine($"{checks} Tp checks passed; native spell effects/Unity are simulated, installed mod IL verified.");
