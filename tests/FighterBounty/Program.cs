using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using HarmonyLib;
using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
int checks=0;
void Check(bool condition,string name) { if(!condition) throw new Exception(name); checks++; Console.WriteLine("PASS "+name); }
var host=new ElinNetHost(); NetSession.Instance.Connection=host;
var human=new Chara {uid=1}; EClass.pc=human;
var remote=new Chara {uid=2,master=human}; host.ActiveRemoteCharas.Add(2,remote);
Check(FighterBountyPatch.Recipient(human)==human,"host kill pays host");
Check(FighterBountyPatch.Recipient(remote)==remote,"remote kill pays remote before party master");
var summon=new Chara {uid=3,master=remote};
Check(FighterBountyPatch.Recipient(summon)==remote,"remote summon pays remote");
Check(FighterBountyPatch.Recipient(new Chara {uid=4,resolvedMaster=summon})==remote,"nested summon resolves saved master");
Check(FighterBountyPatch.Recipient(new Chara {uid=5})==human,"unowned shared ally retains vanilla recipient");
var cycle=new Chara {uid=6}; cycle.master=cycle;
Check(ActorOwnership.PlayerFor(cycle)==null,"ownership cycle terminates");
PersonalMsgSayPatch.BountyReceiver=human;
FighterBountyPatch.Say("bounty",summon,"200",null,null,remote);
Check(Msg.Receiver==remote && PersonalMsgSayPatch.BountyReceiver==human,"message recipient scoped and restored");
Msg.Throw=true; try { FighterBountyPatch.Say("bounty",summon,"200",null,null,remote); } catch(Exception) {}
Check(PersonalMsgSayPatch.BountyReceiver==human,"message scope restores on exception");
NetSession.Instance.Connection=new ElinNetClient(); bool eligible=true;
Check(FighterBountyReplayPatch.Before(ref eligible) && eligible,"client tooltip eligibility stays native");
FighterBountyReplayPatch.ReplayingClientDamage=true;
Check(!FighterBountyReplayPatch.Before(ref eligible) && !eligible,"client damage replay cannot pay bounty");
NetSession.Instance.Connection=host; eligible=true;
Check(FighterBountyReplayPatch.Before(ref eligible) && eligible,"host damage pays normally");
FighterBountyReplayPatch.ReplayingClientDamage=false;
NetSession.Instance.Connection=null;
Check(FighterBountyPatch.Recipient(human)==human,"solo recipient unchanged");
var mismatch=new List<CodeInstruction> {new(OpCodes.Ret)};
Check(FighterBountyPatch.Transpile(mismatch).SequenceEqual(mismatch),"unrecognized IL preserved");
// Inspect and transform the REAL installed vanilla IL, without launching Unity.
var game = @"C:\Program Files (x86)\Steam\steamapps\common\Elin";
var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var build = Path.Combine(repo, "build/guild-bounty");
var roots = new[] { build, Path.Combine(game,"Elin_Data/Managed"), Path.Combine(game,"BepInEx/core"),
    Path.Combine(game,"Package/Mod_ElinTogether"), Path.Combine(game,"Package/_ModdingKit"), Path.Combine(game,"Package/Mod_ElinModdingKit") };
AssemblyLoadContext.Default.Resolving += (context, name) => {
    foreach (var dir in roots) {
        var file = Path.Combine(dir, name.Name + ".dll");
        if (File.Exists(file)) return context.LoadFromAssemblyPath(file);
    }
    return null;
};
var vanilla = Assembly.LoadFrom(Path.Combine(game, "Elin_Data/Managed/Elin.dll"));
var mod = Assembly.LoadFrom(Path.Combine(build, "ElinTogether.dll"));
var method = vanilla.GetType("Card", true)!.GetMethods().Single(m => m.Name == "DamageHP" && m.GetParameters().Length == 9 && m.GetParameters()[0].ParameterType == typeof(long));
// Unity's bundled MonoMod reader is not compatible with this .NET console host.
// Decode vanilla IL directly; invoke the real production matcher over those instructions.
var bytes = method.GetMethodBody()!.GetILAsByteArray()!;
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static)
    .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
    .ToDictionary(op => unchecked((ushort)op.Value));
var original = new List<CodeInstruction>();
for (int at = 0; at < bytes.Length;) {
    ushort key = bytes[at++];
    if (key == 0xfe) key = (ushort)(0xfe00 | bytes[at++]);
    var op = opcodes[key];
    object? operand = null;
    int Int32() { var n = BitConverter.ToInt32(bytes, at); at += 4; return n; }
    switch (op.OperandType) {
        case OperandType.InlineNone: break;
        case OperandType.InlineField: operand = method.Module.ResolveField(Int32()); break;
        case OperandType.InlineMethod: operand = method.Module.ResolveMethod(Int32()); break;
        case OperandType.InlineType: operand = method.Module.ResolveType(Int32()); break;
        case OperandType.InlineString: operand = method.Module.ResolveString(Int32()); break;
        case OperandType.ShortInlineBrTarget: operand = (sbyte)bytes[at++]; break;
        case OperandType.InlineBrTarget: case OperandType.InlineI: operand = Int32(); break;
        case OperandType.ShortInlineI: case OperandType.ShortInlineVar: operand = bytes[at++]; break;
        case OperandType.InlineVar: operand = BitConverter.ToUInt16(bytes, at); at += 2; break;
        case OperandType.ShortInlineR: operand = BitConverter.ToSingle(bytes, at); at += 4; break;
        case OperandType.InlineI8: operand = BitConverter.ToInt64(bytes,at); at+=8; break;
        case OperandType.InlineR: operand = BitConverter.ToDouble(bytes,at); at+=8; break;
        case OperandType.InlineTok: operand = method.Module.ResolveMember(Int32()); break;
        case OperandType.InlineSig: operand = method.Module.ResolveSignature(Int32()); break;
        case OperandType.InlineSwitch: var count=Int32(); var targets=new int[count]; for(int j=0;j<count;j++) targets[j]=Int32(); operand=targets; break;
        default: throw new NotSupportedException(op.OperandType.ToString());
    }
    original.Add(new CodeInstruction(op, operand));
}
var patch = mod.GetType("ElinTogether.Patches.FighterBountyPatch",true)!;
var transformed=((IEnumerable<CodeInstruction>)patch.GetMethod("Transpile",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{original})!).ToList();
Check(transformed.Count(i=>i.operand is MethodInfo m && m.DeclaringType==patch && m.Name=="Recipient")==1,"actual installed vanilla payout recipient replaced once");
Check(transformed.Count(i=>i.operand is MethodInfo m && m.DeclaringType==patch && m.Name=="Say")==1,"actual installed vanilla bounty message replaced once");
int Calls(List<CodeInstruction> list,string name)=>list.Count(i=>i.operand is MethodInfo m && m.Name==name);
Check(Calls(transformed,"get_pc")==Calls(original,"get_pc")-1,"all other pc references preserved");
Check(Calls(transformed,"ModCurrency")==Calls(original,"ModCurrency"),"native currency mutations preserved");
Check(Calls(transformed,"rndHalf")==Calls(original,"rndHalf"),"native bounty random rolls preserved");
Console.WriteLine($"{checks} checks passed. Live two-player validation still required.");
