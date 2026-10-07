using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
using ElinTogether.Elements;
using HarmonyLib;
using Mono.Cecil;
using System.Reflection.Emit;
int checks=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;Console.WriteLine("PASS "+why);}
using var native=ModuleDefinition.ReadModule(args[0]);
var body=native.Types.Single(t=>t.Name=="CharaBody");
var eligibility=body.Methods.Single(m=>m.Name=="IsEquippable");
Check(eligibility.Body.Instructions.Count(i=>i.Operand is MethodReference {Name:"get_pc"}&&i.Next?.Operand is MethodReference {Name:"get_feat"})==1,"native eligibility has one isolated PC feat-point lookup");
var equip=body.Methods.Single(m=>m.Name=="Equip");
var unequip=body.Methods.Single(m=>m.Name=="Unequip"&&m.Parameters.Count==2&&m.Parameters[0].ParameterType.Name=="BodySlot");
bool Calls(MethodDefinition m,string type,string name)=>m.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.DeclaringType.Name==type&&mr.Name==name);
Check(Calls(equip,"DNA","Apply")&&Calls(unequip,"DNA","Apply")&&Calls(equip,"Card","set_feat")&&Calls(unequip,"Card","set_feat"),"native equipment owns DNA mutation and feat spend/refund");
Check(Calls(native.Types.Single(t=>t.Name=="DramaOutcome").Methods.Single(m=>m.Name=="reward_sorin"),"CoreDebug","_AddBodyPart"),"native Sorin reward adds a body part directly");
var sleep=native.Types.Single(t=>t.Name=="Chara").Methods.Single(m=>m.Name=="OnSleep"&&m.Parameters.Count==3);
Check(Calls(sleep,"BaseCard","SetBool")&&sleep.Body.Instructions.Any(i=>i.Operand is int n&&n==135),"native sleep explicitly clears lock flag 135");
var pcCall=new CodeInstruction(OpCodes.Call,AccessTools.PropertyGetter(typeof(EClass),nameof(EClass.pc)));
var label=new DynamicMethod("label",typeof(void),Type.EmptyTypes).GetILGenerator().DefineLabel();pcCall.labels.Add(label);
var routed=RelicEquipmentPatch.Eligibility([pcCall,new(OpCodes.Callvirt,AccessTools.PropertyGetter(typeof(Card),nameof(Card.feat)))]).ToArray();
Check(routed.Length==3&&routed[0].opcode==OpCodes.Ldarg_0&&routed[0].labels.Contains(label)&&routed[1].operand.Equals(AccessTools.Method(typeof(RelicEquipmentPatch),nameof(RelicEquipmentPatch.BudgetOwner))),"affordability patch changes only budget owner and preserves labels");
try{RelicEquipmentPatch.Eligibility([]).ToArray();throw new Exception("accepted wrong IL");}catch(InvalidOperationException){checks++;}
void Cache(params Card[] cards){CardCache.Cards.Clear();foreach(var c in cards)CardCache.Cards[c.uid]=c;}
var client=new ElinNetClient();var host=new ElinNetHost();
var local=new Chara{uid=10};EClass.pc=local;local.body.AddBodyPart(46);local.feat=20;
var item=new Thing{uid=50,parent=local,c_DNA=new()};Cache(local,item);NetSession.Instance.Connection=client;
Check(local.body.Equip(item),"client native relic equip succeeds");
Check(local.feat==15&&local.Power==3&&item.GetBool(135),"native client spends once and applies DNA/lock");
Check(client.Delta.Items.Count==1&&client.Delta.Items[0] is CharaEquipDelta{Relic:not null},"one bundled result replaces competing feat update");
var equipped=(CharaEquipDelta)client.Delta.Items[0];
Check(equipped.Relic!.BeforeFeat==20&&equipped.Relic.AfterFeat==15,"equipment result captures original and final point totals");
var hostPC=new Chara{uid=1};EClass.pc=hostPC;hostPC.feat=0;
var mirror=new Chara{uid=10,ai=new GoalRemote()};mirror.feat=20;
var hostItem=new Thing{uid=50,parent=mirror,c_DNA=new()};Cache(hostPC,mirror,hostItem);host.ActiveRemoteCharas[1]=mirror;NetSession.Instance.Connection=host;
Check(RelicEquipmentPatch.BudgetOwner(mirror.body)==mirror,"host validates client owner rather than zero-point host PC");
Check(!mirror.body.Equip(hostItem)&&mirror.body.Equips==0,"remote AI_Equip cannot execute a second relic mutation");
equipped.Apply(host);
Check(mirror.feat==15&&mirror.Power==3&&mirror.body.GetSlot(46,false)?.thing==hostItem,"host creates missing permanent slot and applies equip once");
Check(hostPC.feat==0&&host.Delta.Items.Count==1&&hostItem.GetBool(135),"host's personal points untouched and successful result relayed");
equipped.Apply(host);
Check(mirror.feat==15&&mirror.Power==3&&hostItem.c_DNA!.Applies==1&&host.Delta.Items.Count==1,"duplicate result cannot spend points or apply DNA twice");
// Sleep local character, then relay only its exact lock state.
EClass.pc=local;NetSession.Instance.Connection=client;Cache(local,item);client.Delta.Items.Clear();local.OnSleep(1,1,false);
var unlock=client.Delta.Items.OfType<RelicStateDelta>().Single();
Check(!item.GetBool(135),"native human sleep unlocks own relic");
EClass.pc=hostPC;NetSession.Instance.Connection=host;Cache(hostPC,mirror,hostItem);unlock.Apply(host);
Check(!hostItem.GetBool(135)&&mirror.feat==15&&mirror.Power==3,"sleep packet clears host lock without reapplying equipment effects");
// Native unequip refunds once; no standalone point delta precedes it.
EClass.pc=local;NetSession.Instance.Connection=client;Cache(local,item);client.Delta.Items.Clear();local.body.Unequip(local.body.GetSlot(46,false)!);
var removed=client.Delta.Items.OfType<CharaEquipDelta>().Single();
Check(client.Delta.Items.Count==1&&local.feat==20&&local.Power==0,"unequip bundles refund and reverses DNA once on client");
EClass.pc=hostPC;NetSession.Instance.Connection=host;Cache(hostPC,mirror,hostItem);removed.Apply(host);removed.Apply(host);
Check(mirror.feat==20&&mirror.Power==0&&hostItem.c_DNA!.Removes==1,"host refund/reverse DNA are exactly once");
// Delayed old equip packet after unequip cannot re-equip or alter points.
equipped.Apply(host);Check(hostItem.c_equippedSlot==0&&mirror.feat==20,"completed old operation cannot resurrect a removed relic");
// A new operation has a new identity and can legitimately equip again.
EClass.pc=local;NetSession.Instance.Connection=client;Cache(local,item);client.Delta.Items.Clear();local.ai=new AI_Equip();local.body.Equip(item);
var aiEquipped=client.Delta.Items.OfType<CharaEquipDelta>().Single();
Check(aiEquipped.Relic!=null&&aiEquipped.Relic.Id!=equipped.Relic.Id,"local AI_Equip publishes one distinct completed relic operation");
EClass.pc=hostPC;NetSession.Instance.Connection=host;Cache(hostPC,mirror,hostItem);aiEquipped.Apply(host);
Check(mirror.feat==15&&mirror.Power==3&&hostItem.c_DNA!.Applies==2,"equip-from-ground task result uses same replay path");
// Break-controlled saved humans retain the human relic sleep-unlock rule.
mirror.Strings[SoloPlayerProfile.SaveKey]="saved";mirror.ai=new AIAct();host.Delta.Items.Clear();mirror.OnSleep(1,1,false);
Check(!hostItem.GetBool(135)&&host.Delta.Items.OfType<RelicStateDelta>().Count()==1,"host AI companion sleep publishes unlocked relic");
var breakUnlock=host.Delta.Items.OfType<RelicStateDelta>().Single();
EClass.pc=local;NetSession.Instance.Connection=client;Cache(local,item);item.SetBool(135,true);PlayerControl.WatchingOwnCharacter=true;breakUnlock.Apply(client);
Check(!item.GetBool(135),"spectator receives companion sleep unlock");
PlayerControl.WatchingOwnCharacter=false;item.SetBool(135,true);breakUnlock.Apply(client);
Check(item.GetBool(135),"late companion state cannot overwrite resumed local human");
// Story reward is additive/idempotent and independent from shared recipes.
var fresh=new Chara{uid=11};var reward=new RelicStateDelta{Owner=fresh,HasSlot=true,Locks=[]};EClass.pc=hostPC;NetSession.Instance.Connection=host;Cache(hostPC,fresh);host.ActiveRemoteCharas[1]=fresh;reward.Apply(host);reward.Apply(host);
Check(fresh.body.slots.Count(s=>s.elementId==46)==1,"repeated Sorin result creates only one permanent relic slot");
var stranger=new Chara{uid=12};Cache(hostPC,fresh,stranger);new RelicStateDelta{Owner=stranger,HasSlot=true,Locks=[]}.Apply(host);
Check(stranger.body.slots.Count==0,"peer cannot unlock another player's relic slot");
EClass.pc=stranger;bool condition=true;
Check(!RelicEquipmentPatch.SorinCondition("hasFlag,sorin_relic",ref condition)&&!condition,"shared story flag cannot hide another character's unclaimed reward");
Check(!RelicEquipmentPatch.SorinCondition("!hasFlag,sorin_relic",ref condition)&&condition,"unclaimed character gets native Sorin reward choice");
RelicEquipment.EnsureSlot(stranger);
Check(!RelicEquipmentPatch.SorinCondition("hasFlag,sorin_relic",ref condition)&&condition,"claimed character gets native explanation choice");
Check(RelicEquipmentPatch.SorinCondition("hasFlag,other_story",ref condition),"other story conditions remain vanilla");
EClass.pc=hostPC;
// Native replacement produces a nested refund then an outer equip result.
EClass.pc=local;NetSession.Instance.Connection=client;Cache(local,item);client.Delta.Items.Clear();item.SetBool(135,false);
var replacement=new Thing{uid=51,parent=local,c_DNA=new(){cost=7,power=8}};CardCache.Cards[51]=replacement;
local.body.Equip(replacement);var swap=client.Delta.Items.OfType<CharaEquipDelta>().ToArray();
Check(swap.Length==2&&swap.All(d=>d.Relic!=null)&&local.feat==13&&local.Power==8,"native swap publishes nested refund and final replacement without loose progression updates");
EClass.pc=hostPC;NetSession.Instance.Connection=host;hostItem.SetBool(135,false);mirror.ai=new GoalRemote();
var hostReplacement=new Thing{uid=51,parent=mirror,c_DNA=new(){cost=7,power=8}};Cache(hostPC,mirror,hostItem,hostReplacement);host.ActiveRemoteCharas[1]=mirror;
foreach(var d in swap)d.Apply(host);
foreach(var d in swap)d.Apply(host);
Check(mirror.feat==13&&mirror.Power==8&&hostReplacement.c_DNA!.Applies==1&&hostItem.c_equippedSlot==0,"swap replay and duplicates preserve one refund and one new DNA application");
// Exceptions cannot leak the progression-suppression/replay scopes.
Cache(hostPC,mirror,hostItem);host.ActiveRemoteCharas[1]=mirror;mirror.body.ThrowEquip=true;
var failure=new RelicEquipResult{Id=Guid.NewGuid(),BeforeFeat=30,AfterFeat=25,HasSlot=true};
try{RelicEquipment.Replay(mirror,hostItem,failure,()=>mirror.body.Equip(hostItem));throw new Exception("missing failure");}catch(InvalidOperationException){}
Check(!RelicEquipment.Changing(mirror)&&mirror.feat==13,"failed native replay restores points and mutation scope");mirror.body.ThrowEquip=false;
// Ordinary gear and ordinary progression retain existing channels.
EClass.pc=local;NetSession.Instance.Connection=client;Cache(local,item);client.Delta.Items.Clear();local.feat=31;
Check(client.Delta.Items.Single() is CharaFeatPointDelta,"unrelated point gains still publish normally");
client.Delta.Items.Clear();local.body.AddBodyPart(31);var normal=new Thing{uid=80,parent=local,category=new(){slot=31}};CardCache.Cards[80]=normal;local.ai=new AIAct();local.body.Equip(normal);
Check(client.Delta.Items.Single() is CharaEquipDelta{Relic:null},"ordinary equipment keeps its existing protocol");
NetSession.Instance.Connection=null;
Check(RelicEquipmentPatch.BudgetOwner(mirror.body)==EClass.pc,"solo affordability keeps native controller semantics");
if(args.Length>1){
 using var built=ModuleDefinition.ReadModule(args[1]);
 var hook=built.Types.Single(t=>t.Name=="ElementChangedEvent").Methods.Single(m=>m.Name=="OnSyncElementChange");
 var guard=hook.Body.Instructions.Single(i=>i.Operand is MethodReference {Name:"Changing"});
 var defer=hook.Body.Instructions.Single(i=>i.Operand is MethodReference {Name:"Deferred"});
 Check(guard.Offset<defer.Offset,"compiled DNA element guard runs before deferred absolute updates are queued");
}
Console.WriteLine($"{checks} checks passed; native metadata verified, game boundaries simulated.");
