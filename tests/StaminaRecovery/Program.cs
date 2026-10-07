using ElinTogether.Models;
using ElinTogether.Patches;
using ElinTogether.Net;
using ElinTogether.Elements;
using HarmonyLib;
using Mono.Cecil;
using System.Reflection.Emit;
int checks=0;void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
using var game=ModuleDefinition.ReadModule(args[0]);
var chara=game.Types.Single(t=>t.Name=="Chara");
var tick=chara.Methods.Single(m=>m.Name=="TickConditions");
var staminaCalls=tick.Body.Instructions.Where(i=>i.Operand is MethodReference m&&m.Name=="Mod"&&m.DeclaringType.Name=="Stats"&&i.Previous?.Previous?.Previous?.Previous?.Previous?.Operand is MethodReference {Name:"get_stamina"}).ToArray();
Check(staminaCalls.Length==1,"installed TickConditions has exactly one NPC stamina Mod call");
var site=staminaCalls.Single();
Check(site.Previous.OpCode==Mono.Cecil.Cil.OpCodes.Add&&site.Previous.Previous.OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_1&&site.Previous.Previous.Previous.Operand is MethodReference {Name:"rnd"}&&site.Previous.Previous.Previous.Previous.OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_5,"target call is native rnd(5)+1 NPC regeneration");
var localRecovery=tick.Body.Instructions.Single(i=>i.Operand is MethodReference {Name:"RecoverStamina"});
Check(localRecovery.Offset<site.Offset,"human recovery is a separate earlier call");
var nativeMod=AccessTools.Method(typeof(Stats),nameof(Stats.Mod));
var code=new CodeInstruction(OpCodes.Callvirt,nativeMod);
var label=new DynamicMethod("labels",typeof(void),Type.EmptyTypes).GetILGenerator().DefineLabel();code.labels.Add(label);
var routed=PersonalStaminaRecoveryPatch.Regen([
 new(OpCodes.Call,AccessTools.PropertyGetter(typeof(Chara),nameof(Chara.stamina))),
 new(OpCodes.Ldc_I4_5),new(OpCodes.Call,AccessTools.Method(typeof(EClass),nameof(EClass.rnd))),
 new(OpCodes.Ldc_I4_1),new(OpCodes.Add),code]).Last();
Check(routed.opcode==OpCodes.Call&&routed.operand.Equals(AccessTools.Method(typeof(PersonalStaminaRecoveryPatch),nameof(PersonalStaminaRecoveryPatch.RecoverNpc)))&&routed.labels.Contains(label),"patch preserves branch labels and routes only native regen");
try{PersonalStaminaRecoveryPatch.Regen([]).ToArray();throw new Exception("accepted changed IL");}catch(InvalidOperationException){checks++;}
var ai=new Chara();BaseStats.CC=ai;ai.stamina.value=98;PersonalStaminaRecovery.Store(ai,10);
NetSession.Instance.Connection=new ElinNetHost();PersonalStaminaRecoveryPatch.RecoverNpc(ai.stamina,6);
Check(ai.stamina.value==100&&ai.stamina.Calls==1&&PersonalStaminaRecovery.Saved(ai)==8,"NPC regen runs once and consumes only actual gain at stamina cap");
ai.stamina.value=50;PersonalStaminaRecoveryPatch.RecoverNpc(ai.stamina,3);
Check(PersonalStaminaRecovery.Saved(ai)==5,"further NPC regen spends remaining allowance");
ai.ai=new GoalRemote();PersonalStaminaRecoveryPatch.RecoverNpc(ai.stamina,3);
Check(PersonalStaminaRecovery.Saved(ai)==5,"remote human tick mirror never spends saved allowance");
ai.ai=null;NetSession.Instance.Connection=new ElinNetClient();PersonalStaminaRecoveryPatch.RecoverNpc(ai.stamina,3);
Check(PersonalStaminaRecovery.Saved(ai)==5,"client replay never independently accounts AI recovery");
NetSession.Instance.Connection=null;PersonalStaminaRecoveryPatch.RecoverNpc(ai.stamina,9);
Check(PersonalStaminaRecovery.Saved(ai)==0,"solo companion recovery exhausts credit without going negative");
PersonalStaminaRecovery.Store(ai,7);ai.stamina.Mod(5);
Check(PersonalStaminaRecovery.Saved(ai)==7,"ordinary food/direct stamina recovery is not rerouted");
PersonalStaminaRecoveryPatch.Slept(ai);
Check(PersonalStaminaRecovery.Saved(ai)==0,"companion sleep clears remaining pre-break credit");
PersonalStaminaRecovery.Store(ai,7);ai.isDead=true;PersonalStaminaRecoveryPatch.Reviving(ai);ai.isDead=false;
Check(PersonalStaminaRecovery.Saved(ai)==0,"death and revival cannot resurrect credit");
EClass.player.staminaRecovery=12;PersonalStaminaRecoveryPatch.Slept(EClass.pc);
Check(EClass.player.staminaRecovery==12,"human sleep recovery remains native");
Check(PersonalStaminaRecovery.Current(EClass.pc)==12,"human snapshots use live vanilla allowance");
PlayerControl.WatchingOwnCharacter=true;PersonalStaminaRecovery.Store(EClass.pc,3);
Check(PersonalStaminaRecovery.Current(EClass.pc)==3,"spectator reads host-owned copy");
PersonalStaminaRecovery.Mirror(EClass.pc,2);
Check(EClass.player.staminaRecovery==2,"spectator widget receives authoritative allowance");
var packetHost=new ElinNetHost();packetHost.ActiveRemoteCharas[1]=ai;
var packet=new CharaStaminaDelta{Chara=ai,Stamina=44,StaminaRecovery=13};packet.Apply(packetHost);
Check(ai.stamina.value==44&&PersonalStaminaRecovery.Saved(ai)==13,"existing stamina packet carries latest recoverable amount to host");
packetHost.Human=false;new CharaStaminaDelta{Chara=ai,Stamina=99,StaminaRecovery=77}.Apply(packetHost);
Check(ai.stamina.value==44&&PersonalStaminaRecovery.Saved(ai)==13,"late human packet cannot overwrite AI credit");
packetHost.Human=true;new CharaStaminaDelta{Chara=ai,Stamina=99,StaminaRecovery=77,OriginPeer=2}.Apply(packetHost);
Check(ai.stamina.value==44&&PersonalStaminaRecovery.Saved(ai)==13,"wrong peer cannot overwrite another character's stamina");
Console.WriteLine($"{checks} checks passed; native IL checked, runtime boundaries simulated.");
