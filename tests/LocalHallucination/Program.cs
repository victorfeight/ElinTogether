using ElinTogether.Net;
using ElinTogether.Patches;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
int passed=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
// Execute transpiled vanilla IL without native detours: the game's old
// MonoMod runtime does not support this harness's .NET 11 CLR.
Delegate Compile(string name, Type delegateType, params Type[] args) {
 var method=typeof(ConHallucination).GetMethod(name)!;
 var dynamic=new DynamicMethod(name+"Patched",typeof(void),args,typeof(Program).Module,true);
 var il=dynamic.GetILGenerator();
 using var module=Mono.Cecil.ModuleDefinition.ReadModule(method.Module.FullyQualifiedName);
 var definition=module.Types.Single(t=>t.Name=="ConHallucination").Methods.Single(m=>m.Name==name);
 Type Resolve(Mono.Cecil.TypeReference t)=>typeof(Program).Assembly.GetType(t.FullName)??Type.GetType(t.FullName)!;
 var labels=definition.Body.Instructions.ToDictionary(i=>i,i=>il.DefineLabel());
 var locals=definition.Body.Variables.Select(v=>il.DeclareLocal(Resolve(v.VariableType))).ToArray();
 var opcodes=typeof(OpCodes).GetFields().Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(o=>o.Value);
 var instructions=definition.Body.Instructions.Select(i=> {
  object? operand=i.Operand switch {
   Mono.Cecil.MethodReference m=>Resolve(m.DeclaringType).GetMethod(m.Name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static,null,m.Parameters.Select(p=>Resolve(p.ParameterType)).ToArray(),null),
   Mono.Cecil.FieldReference f=>Resolve(f.DeclaringType).GetField(f.Name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static),
   Mono.Cecil.Cil.Instruction target=>labels[target],
   Mono.Cecil.Cil.VariableDefinition v=>locals[v.Index],
   _=>i.Operand
  };
  var code=new CodeInstruction(opcodes[i.OpCode.Value],operand);code.labels.Add(labels[i]);return code;
 }).ToList();
 foreach(var code in LocalHallucinationPatch.UseLocalViewer(instructions)) {
  foreach(var label in code.labels)il.MarkLabel(label);
  switch(code.operand) {
   case null: il.Emit(code.opcode); break;
   case MethodInfo m: il.Emit(code.opcode,m); break;
   case FieldInfo f: il.Emit(code.opcode,f); break;
   case Label l: il.Emit(code.opcode,l); break;
   case LocalBuilder b: il.Emit(code.opcode,b); break;
   case int n: il.Emit(code.opcode,n); break;
   case sbyte n: il.Emit(code.opcode,n); break;
   default: throw new Exception("Unsupported IL operand: "+code.operand.GetType());
  }
 }
 return dynamic.CreateDelegate(delegateType);
}
var set=(Action<ConHallucination,Chara,bool>)Compile("SetOwner",typeof(Action<ConHallucination,Chara,bool>),typeof(ConHallucination),typeof(Chara),typeof(bool));
var remove=(Action<ConHallucination>)Compile("OnRemoved",typeof(Action<ConHallucination>),typeof(ConHallucination));
NetSession.Instance.HasActiveConnection=true;
var client=new Chara{localPC=true,savedPC=false};
var hostCopy=new Chara{localPC=false,savedPC=true};
ConHallucination local=new ConHallucination(),remote=new ConHallucination();
Player.seedHallucination=0;set(local,client,false);
Check(Player.seedHallucination!=0,"client hallucination enables local visuals without changing saved flag");
Check(!client.savedPC,"client identity flag unchanged");
var seed=Player.seedHallucination;set(remote,hostCopy,false);
Check(Player.seedHallucination==seed,"remote host condition cannot overwrite client visuals");
remove(remote);Check(Player.seedHallucination==seed,"remote cure cannot clear local hallucination");
remove(local);Check(Player.seedHallucination==0,"local cure clears visuals");
set(remote,hostCopy,false);Check(Player.seedHallucination==0,"host hallucination alone cannot affect client view");
var host=new Chara{localPC=true,savedPC=true};set(local,host,false);
Check(Player.seedHallucination!=0,"host local hallucination works");
seed=Player.seedHallucination;set(remote,new Chara(),false);remove(remote);
Check(Player.seedHallucination==seed,"remote client condition does not affect host view");
EClass.game.player.chara=client;client.hallucinating=true;Player.seedHallucination=0;
LocalHallucinationPatch.RefreshAfterSceneInit(Scene.Mode.StartGame);
Check(Player.seedHallucination!=0,"joining while hallucinating restores visuals");
seed=Player.seedHallucination;LocalHallucinationPatch.RefreshAfterSceneInit(Scene.Mode.StartGame);
Check(Player.seedHallucination==seed,"scene refresh preserves active visual seed");
client.hallucinating=false;LocalHallucinationPatch.RefreshAfterSceneInit(Scene.Mode.StartGame);
Check(Player.seedHallucination==0,"scene refresh clears inherited nonlocal visuals");
NetSession.Instance.HasActiveConnection=false;set(local,hostCopy,false);
Check(Player.seedHallucination!=0,"single-player retains vanilla saved-flag semantics");
remove(local);Check(Player.seedHallucination==0,"single-player removal unchanged");
Console.WriteLine($"{passed} checks passed using transpiled vanilla IL; native Unity detours/rendering still require live acceptance.");
