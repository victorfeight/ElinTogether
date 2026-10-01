using System.Reflection;
using System.Runtime.Loader;
using System.Reflection.Emit;
using HarmonyLib;
using ElinTogether.Models;

void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
CombatTimeline Start(params (int uid, double debt)[] players) {
    var t = new CombatTimeline();
    t.Synchronize(players.Select(p => new KeyValuePair<int,double>(p.uid,p.debt)));
    t.Advance(); t.BeginWindow(); return t;
}
bool Near(double a, double b) => Math.Abs(a-b) < 0.00001;
var state = Start((1,0), (719,0));
Check(state.Due.SetEquals(new[]{1,719}), "simultaneously due players both participate");
Check(state.SetReady(1, state.Epoch, true) && !state.AllReady, "one ready player cannot start another due player's action");
state.SetReady(719, state.Epoch, true);
Check(state.AllReady, "due player readiness opens execution");
Check(!state.Complete(1, state.Epoch-1, .5), "stale completion ignored");
Check(!state.Complete(99, state.Epoch, .5), "nonparticipant cannot spend timeline time");
Check(!state.Complete(1, state.Epoch, double.NaN) && !state.Complete(1,state.Epoch,double.PositiveInfinity) &&
      !state.Complete(1,state.Epoch,-1), "invalid costs cannot alter deadlines");
Check(state.Complete(1,state.Epoch,.5) && !state.Complete(1,state.Epoch,2), "completed action charged once");
try { state.Advance(); Check(false,"must reject unfinished advancement"); }
catch (InvalidOperationException) { Check(true,"unfinished due action prevents clock advance"); }
state.Complete(719,state.Epoch,1);
Check(Near(state.Advance(),.5), "world advances to earliest deadline, not slowest action cost");
state.BeginWindow();
Check(state.Due.SetEquals(new[]{1}), "fast player receives the intermediate turn");
Check(!state.SetReady(719,state.Epoch,true), "slower player's input is not a readiness barrier");
Check(!state.SetReady(1,state.Epoch-1,true), "old readiness cannot open next decision window");
state.Complete(1,state.Epoch,.5);
Check(Near(state.Advance(),.5) && Near(state.Now,1), "enemy time advances once per elapsed slice");
state.BeginWindow();
Check(state.Due.SetEquals(new[]{1,719}), "both players due again at shared deadline");

// Preserve speed advantage over many turns, rather than only the first pair.
state = Start((1,0),(719,0));
int fast=0,slow=0;
while (state.Now < 100 - CombatTimeline.Epsilon) {
    foreach (var uid in state.Due.ToArray()) {
        if (uid==1) fast++; else slow++;
        state.Complete(uid,state.Epoch,uid==1 ? .5 : 1);
    }
    state.Advance(); state.BeginWindow();
}
Check(fast==200 && slow==100 && Near(state.Now,100), "200-speed vs 100-speed opportunities stay 2:1 over time");
state.Complete(1,state.Epoch,2); state.Complete(719,state.Epoch,1);
state.Advance(); state.BeginWindow();
Check(state.Due.SetEquals(new[]{719}), "a costly fast-player action postpones its next opportunity");

state = Start((1,0),(719,0));
state.Complete(1,state.Epoch,0); state.Complete(719,state.Epoch,1);
Check(Near(state.Advance(),0), "cancelled unspent action grants no enemy time");
state.BeginWindow();
Check(state.Due.SetEquals(new[]{1}) && !state.AllReady, "cancelled action requests a fresh decision at same timestamp");
state.Synchronize(new[]{new KeyValuePair<int,double>(719,0)});
Check(state.AllDone && !state.Due.Contains(1), "disconnect removes missing player from current barrier");
Check(Near(state.Advance(),1), "survivor's outstanding debt survives disconnect");
state.Synchronize(new[]{new KeyValuePair<int,double>(719,0),new KeyValuePair<int,double>(720,.25)});
state.BeginWindow();
Check(state.Due.SetEquals(new[]{719}) && Near(state.Remaining(720),.25), "join seeds debt without stealing or blocking an existing turn");
state.Complete(719,state.Epoch,1); state.Advance(); state.BeginWindow();
Check(state.Due.SetEquals(new[]{720}), "new player participates when its deadline arrives");
state = Start((1,.25),(719,.75));
Check(Near(state.Now,.25) && state.Due.SetEquals(new[]{1}) && Near(state.Remaining(719),.5),
      "combat entry retains unequal timer debts");
var epoch = state.Epoch;
state.Reset(false); state.Synchronize(new[]{new KeyValuePair<int,double>(1,0)}); state.BeginWindow();
Check(state.Epoch>epoch && !state.Complete(1,epoch,.3), "combat re-entry rejects previous encounter acknowledgements");
state.Reset();
Check(state.Now==0 && state.Epoch==0 && state.Deadlines.Count==0, "session reset clears timeline");

// Ordered grants preserve every due player's opportunity, rotating ties fairly.
state = Start((1,0),(719,0));
Check(state.NextActor==1,"first tie grants one player, not simultaneous local execution");
state.Complete(1,state.Epoch,1);
Check(state.NextActor==719 && !state.AllDone,"second due player follows first completion at same time");
state.Complete(719,state.Epoch,1);
Check(state.NextActor==-1 && state.AllDone,"completed window grants nobody twice");
state.Advance(); state.BeginWindow();
Check(state.NextActor==719,"next tie rotates priority away from host");
state.Complete(719,state.Epoch,1);
state.Complete(1,state.Epoch,0);
Check(Near(state.Advance(),0),"invalid second attack grants no additional enemy time");
state.BeginWindow();
Check(state.NextActor==1 && state.Due.Count==1,"invalidated player can choose again without partner spending another action");
state.Synchronize(new[]{new KeyValuePair<int,double>(719,0)});
Check(state.NextActor==-1 && state.AllDone,"disconnect releases a dispatched player's barrier");
state=Start((719,0),(1,0),(500,0));
Check(state.NextActor==1,"tie order is independent of participant enumeration");
state.Complete(1,state.Epoch,1); state.Complete(500,state.Epoch,1); state.Complete(719,state.Epoch,1);
state.Advance(); state.BeginWindow();
Check(state.NextActor==500,"tie priority rotates across more than two players");
state.Complete(500,state.Epoch,1);
Check(state.NextActor==719,"rotated order continues before wrapping");
state.Complete(719,state.Epoch,1);
Check(state.NextActor==1,"rotated order wraps without losing a turn");

// Exercise the real mod queue: deferred deaths must precede the next grant.
var deltas = new ElinTogether.Net.ElinDeltaManager();
deltas.AddRemote(new ProbeDelta("attack"));
deltas.DeferRemote(new ProbeDelta("death"));
deltas.RefreshBuffer();
Check(deltas.HasDeferredOut,"deferred death holds the next dispatch barrier");
var firstBatch=deltas.FlushOutBuffer().Cast<ProbeDelta>().Select(d=>d.Label).ToArray();
Check(firstBatch.SequenceEqual(new[]{"attack"}) && !deltas.HasDeferredOut,
    "first flush promotes deferred death without discarding it");
deltas.AddRemote(new ProbeDelta("next grant")); deltas.RefreshBuffer();
Check(deltas.FlushOutBuffer().Cast<ProbeDelta>().Select(d=>d.Label).SequenceEqual(new[]{"death","next grant"}),
    "authoritative death precedes the next player's execution grant on the wire");

var input=new ElinTogether.Net.ElinDeltaManager();
var inputHost=new ElinTogether.Net.ElinNetHost();inputHost.ActiveRemoteCharas[2]=new();inputHost.ActiveRemoteCharas[3]=new();
var applied=new List<int>();
input.AddLocal(new CallbackDelta(()=>applied.Add(2)){OriginPeer=2});
input.DeferLocal(new CallbackDelta(()=>applied.Add(2)){OriginPeer=2});
input.AddLocal(new CallbackDelta(()=>applied.Add(3)){OriginPeer=3});
input.DiscardIncomingPeer(2);input.ProcessLocalBatch(inputHost);input.ProcessLocalBatch(inputHost);
Check(applied.SequenceEqual(new[]{3}),"disconnect discards immediate and deferred input only for departed peer");
applied.Clear();
input.AddLocal(new CallbackDelta(()=>inputHost.ActiveRemoteCharas.Remove(2)));
input.AddLocal(new CallbackDelta(()=>applied.Add(2)){OriginPeer=2});
input.AddLocal(new CallbackDelta(()=>applied.Add(3)){OriginPeer=3});
input.ProcessLocalBatch(inputHost);
Check(applied.SequenceEqual(new[]{3}),"ownership revoked during batch blocks remaining departed-player input");

inputHost.ActiveRemoteCharas[2]=new();
inputHost.Watching.Add(2);applied.Clear();
input.AddLocal(new CallbackDelta(()=>applied.Add(2)){OriginPeer=2});
input.DeferLocal(new CallbackDelta(()=>applied.Add(2)){OriginPeer=2});
input.AddLocal(new CallbackDelta(()=>applied.Add(3)){OriginPeer=3});
input.ProcessLocalBatch(inputHost);input.ProcessLocalBatch(inputHost);
Check(applied.SequenceEqual(new[]{3}),"watching peer remains connected but cannot replay input, including deferred input");
inputHost.Watching.Remove(2);applied.Clear();
input.AddLocal(new CallbackDelta(()=>applied.Add(2)){OriginPeer=2});input.ProcessLocalBatch(inputHost);
Check(applied.SequenceEqual(new[]{2}),"human handoff restores input without reconnecting peer");

// Inspect and transform the REAL installed vanilla IL, without launching Unity.
var game = @"C:\Program Files (x86)\Steam\steamapps\common\Elin";
var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var build = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(repo, "build/shared-rounds");
var roots = new[] { @"C:\Program Files (x86)\Steam\steamapps\workshop\content\2135150\3396025367",
    @"C:\Program Files (x86)\Steam\steamapps\workshop\content\2135150\3370686923", build, Path.Combine(game,"Elin_Data/Managed"), Path.Combine(game,"BepInEx/core"),
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

var controlPatch = mod.GetType("ElinTogether.Patches.PlayerControlInputPatch", true)!;
var targets = ((IEnumerable<MethodBase>)controlPatch.GetMethod("TargetMethods", BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!).ToArray();
Check(targets.Length > 4 && targets.All(m => m is not null && !m.IsAbstract), "control input hooks resolve concrete installed Elin methods");
Check(targets.Any(m=>m.DeclaringType!.Name=="InvOwnerHotbar" && m.Name=="OnClick"), "input guard covers overridden hotbar input as well as base inventory");

Assembly.LoadFrom(Path.Combine(roots[0], "AutoActAllyExpansion.dll"));
var allyGuard = mod.GetType("ElinTogether.Patches.AutoActAllyGuard",true)!;
var assignments = ((IEnumerable<MethodBase>)allyGuard.GetMethod("TargetMethods",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!).ToArray();
Check(assignments.Any(m=>m.Name=="TrySetAutoActHarvestMine") && assignments.Any(m=>m.Name=="TrySetAutoActBuild"),
 "Ally Expansion assignment guard resolves installed harvest and construction entry points");

var cardType=vanilla.GetType("Card",true)!;
Check(cardType.GetProperty("ShouldShowMsg")?.GetGetMethod() is {IsStatic:false},
 "personal message visibility hook resolves installed Card getter");
var buildType=vanilla.GetType("TaskBuild",true)!;
Check(buildType.GetField("lastPos")?.FieldType==vanilla.GetType("Point"),
 "build completion outcome uses installed per-task lastPos field");
