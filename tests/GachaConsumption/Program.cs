using Mono.Cecil;
using Mono.Cecil.Cil;
int n=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);n++;Console.WriteLine("PASS "+name);}
using var game=ModuleDefinition.ReadModule(args[0]);using var mod=ModuleDefinition.ReadModule(args[1]);
var gacha=game.Types.Single(t=>t.Name=="InvOwnerGacha");
Check(gacha.Methods.Single(m=>m.Name=="get_SingleTarget").Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldc_I4_1),"native gacha accepts a single coin");
var process=gacha.Methods.Single(m=>m.Name=="_OnProcess");
Check(process.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="PlayGacha"&&i.Previous.OpCode==OpCodes.Ldc_I4_1),"native gacha issues exactly one reward per operation");
var handler=mod.Types.Single(t=>t.Name=="InvOwnerOnProcessDelta").Methods.Single(m=>m.Name=="ApplyGacha");
Check(handler.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="GetIdCoin"),"MP validates the machine's coin denomination");
Check(!handler.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="Destroy"),"MP gacha never destroys the resolved source stack directly");
var debit=handler.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.Name=="ModNum");
Check(debit.Previous.OpCode==OpCodes.Ldc_I4_1&&debit.Previous.Previous.OpCode==OpCodes.Ldc_I4_M1,"compiled MP handler debits exactly one coin with native notification");
var count=game.Types.Single(t=>t.Name=="Card").Methods.Single(m=>m.Name=="ModNum");
Check(count.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="Destroy"),"native count handling retains last-coin destruction lifecycle");
var spawn=game.Types.Single(t=>t.Name=="CoreDebug").Methods.Single(m=>m.Name=="Spawn");
Check(spawn.Parameters[0].Name=="id"&&spawn.Parameters[1].Name=="num","recovery command accepts item ID followed by count");
Console.WriteLine($"{n} compiled/native IL checks passed; live multiplayer test still required.");
