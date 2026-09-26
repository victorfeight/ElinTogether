using System.Reflection;
using System.Runtime.Loader;
using System.Reflection.Emit;
using HarmonyLib;
using ElinTogether.Models;

void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
var state = new CombatRoundState();
Check(state.Advance(1), "first round starts");
state.SetReady(719, 1, true);
Check(state.Ready.Contains(719), "matching readiness accepted");
state.SetReady(719, 0, false);
Check(state.Ready.Contains(719), "old cancellation cannot revoke current readiness");
Check(!state.Complete(719, 0) && state.Done.Count == 0, "stale completion cannot end current turn");
Check(state.Complete(719, 1) && !state.Complete(719, 1), "completion accepted once, avoiding duplicate cost");
Check(state.Advance(2) && state.Ready.Count == 0 && state.Done.Count == 0, "next round requires fresh readiness and completion");
Check(!state.Advance(1) && state.Id == 2, "old phase cannot roll back round");
state.SetReady(719, 3, true);
Check(state.Ready.Count == 0, "future readiness cannot jump rounds");
state.Reset();
Check(state.Id == 0 && state.Ready.Count == 0 && state.Done.Count == 0, "new session clears round state");

// Inspect and transform the REAL installed vanilla IL, without launching Unity.
var game = @"C:\Program Files (x86)\Steam\steamapps\common\Elin";
var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var build = Path.Combine(repo, "build/shared-rounds");
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
var updater = vanilla.GetType("GameUpdater+CharaUpdater", true)!;
var method = updater.GetMethod("FixedUpdate")!;
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
        default: throw new NotSupportedException(op.OperandType.ToString());
    }
    original.Add(new CodeInstruction(op, operand));
}
var patch = mod.GetType("ElinTogether.Patches.ActionModeCombat", true)!;
var transpile = patch.GetMethod("OnCharaUpdaterAccumulate", BindingFlags.NonPublic|BindingFlags.Static)!;
var transformed = ((IEnumerable<CodeInstruction>)transpile.Invoke(null, new object[]{original})!).ToList();
var timer = vanilla.GetType("Card", true)!.GetField("roundTimer")!;
var cost = vanilla.GetType("Chara", true)!.GetField("actTime")!;
Check(transformed.Count(i => i.opcode == OpCodes.Stfld && Equals(i.operand,timer)) ==
      original.Count(i => i.opcode == OpCodes.Stfld && Equals(i.operand,timer))-1,
      "timer accumulation patched, real cost subtraction preserved");
var loop = transformed.FindIndex(i => i.opcode == OpCodes.Ldfld && Equals(i.operand,cost) &&
    transformed.IndexOf(i)+1 < transformed.Count &&
    (transformed[transformed.IndexOf(i)+1].opcode == OpCodes.Bgt || transformed[transformed.IndexOf(i)+1].opcode == OpCodes.Bgt_S));
Check(loop >= 2 && transformed[loop-2].opcode == OpCodes.Call,
      "round gate replaces loop-condition read, not subtraction read");
Console.WriteLine("Actual vanilla IL transformation validated; Unity runtime/two-player tests still required.");
