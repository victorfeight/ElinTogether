using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using HarmonyLib;
using ElinTogether.Models;
using ElinTogether.Net;
int checks=0;
void Check(bool yes,string name) {if(!yes) throw new Exception(name); checks++; Console.WriteLine("PASS "+name);}
var host=new ElinNetHost(); var h=new Chara {uid=1}; var c=new Chara {uid=2,master=h};
EClass.pc=h; EClass.player.karma=30; NetSession.Instance.Player=h; NetSession.Instance.Connection=host; host.ActiveRemoteCharas[7]=c;
var quest=new Quest(); EClass.game.quests.list.Add(quest);
PersonalKarma.Initialize(c); Check(PersonalKarma.Get(c)==30,"legacy initialization uses previous join baseline");
EClass.player.karma=9; PersonalKarma.Initialize(c); Check(PersonalKarma.Get(c)==30,"reinitialization never overwrites saved personal balance");
var fresh=new Chara{uid=3}; PersonalKarma.Initialize(fresh,30); Check(PersonalKarma.Get(fresh)==30,"new character uses vanilla initial karma");
using(PersonalKarma.For(c)) Check(!PersonalKarma.BeforeChange(-1),"remote native event intercepted");
Check(PersonalKarma.Get(c)==29 && EClass.player.karma==9,"remote crime changes only remote saved karma");
Check(quest.Negative==-1 && host.Sent.Count==1 && host.Sent[0].Item1==7,"one quest event and notification to owner");
using(PersonalKarma.For(new Chara {uid=4,master=c})) PersonalKarma.BeforeChange(-1);
Check(PersonalKarma.Get(c)==28,"summon crime attributes to owner before party leader");
using(PersonalKarma.For(new Chara {uid=5})) Check(!PersonalKarma.BeforeChange(-1),"unowned NPC cannot charge host");
Check(EClass.player.karma==9 && PersonalKarma.Get(c)==28,"NPC scope leaves human balances alone");
using(PersonalKarma.For(c)) {
 using(PersonalKarma.For(h)) Check(PersonalKarma.BeforeChange(-1),"nested host event retains native implementation");
 PersonalKarma.BeforeChange(-1);
}
Check(PersonalKarma.Get(c)==27,"nested scope restores outer actor");
try {using var actorScope=PersonalKarma.For(c); throw new Exception();} catch(Exception) {}
Check(PersonalKarma.BeforeChange(-1),"exception restores unscoped native host event");
int refreshes=EClass._zone.Refreshes;
PersonalKarma.Change(c,-1);
Check(EClass._zone.Refreshes==refreshes,"same-sign change does not reset unrelated guard combat");
EClass.pc=c;
using(PersonalKarma.For(h)) Check(PersonalKarma.BeforeChange(-1),"host actor identity survives temporary pc swap");
EClass.pc=h;
PersonalKarma.Change(c,-1000); Check(PersonalKarma.Get(c)==-100,"personal lower bound clamped");
int previous=quest.Negative; PersonalKarma.Change(c,-1); Check(PersonalKarma.Get(c)==-100 && quest.Negative==previous-1,"crime at floor still advances signed trial progress");
Check(c.pos.Witnesses==1,"crossing into criminal status witnesses actual actor once");
Check(PersonalKarma.IsCriminal(c),"negative remote actor criminal"); c.Disguised=true; Check(!PersonalKarma.IsCriminal(c),"remote actor disguise respected"); c.Disguised=false;
PersonalKarma.Change(c,1000); Check(PersonalKarma.Get(c)==100 && quest.Negative==previous-1,"positive karma clamps without undoing thieves progress");
var saved=System.Text.Json.JsonSerializer.Serialize(c.Values); var restored=new Chara {uid=2,Values=System.Text.Json.JsonSerializer.Deserialize<Dictionary<int,int>>(saved)!};
Check(restored.GetInt(PersonalKarma.ValueKey)==100 && restored.GetInt(PersonalKarma.RevisionKey)==c.GetInt(PersonalKarma.RevisionKey),"saved character data preserves balance and revision");
var client=new ElinNetClient(); NetSession.Instance.Connection=client; NetSession.Instance.Player=restored; EClass.pc=restored; EClass.player.karma=9; PersonalKarma.Join(restored);
Check(EClass.player.karma==100,"join restores own balance over host Player data");
Check(!PersonalKarma.BeforeChange(-1) && EClass.player.karma==100,"client replay cannot create another penalty");
var next=restored.GetInt(PersonalKarma.RevisionKey)+1;
var state=new PersonalKarmaState {ActorUid=2,Value=-1,Revision=next};
PersonalKarma.Receive(state); int lines=Msg.Lines.Count;
PersonalKarma.Receive(state,-101,100); Check(EClass.player.karma==-1 && Msg.Lines.Count==lines+2,"snapshot before notification still announces personal transition once");
PersonalKarma.Receive(state,-101,100); Check(Msg.Lines.Count==lines+2,"duplicate event notification ignored");
PersonalKarma.Receive(new() {ActorUid=2,Value=90,Revision=next-1}); Check(EClass.player.karma==-1,"stale snapshot cannot roll back personal state");
PersonalKarma.Receive(new() {ActorUid=1,Value=99,Revision=next+1}); Check(EClass.player.karma==-1,"other player state cannot overwrite own karma");
NetSession.Instance.Connection=host; EClass.pc=h; NetSession.Instance.Player=h;
new PersonalKarmaDelta{State=new(){ActorUid=2,Value=99,Revision=999},Amount=100}.Apply(host);
Check(PersonalKarma.Get(c)==100,"host rejects client-originated authoritative state packet");
// Client-only UI results are sequenced separately from host simulation.
ElinDelta.IsApplying=true;
using(PersonalKarma.For(c,true)) PersonalKarma.BeforeChange(-1);
Check(PersonalKarma.Get(c)==100,"host mirror of local hold does not charge another copy");
ElinDelta.IsApplying=false;
PersonalKarma.BeginSession(c); var token=c.GetStr(PersonalKarma.SessionKey);
NetSession.Instance.Connection=client; EClass.pc=c; NetSession.Instance.Player=c; PersonalKarma.Join(c);
client.Delta.Sent.Clear();
using(PersonalKarma.For(c)) PersonalKarma.BeforeChange(-1);
Check(client.Delta.Sent.Count==0,"host-executed client prediction emits no owner report");
PersonalKarma.BeforeChange(-1);
Check(client.Delta.Sent.Count==1 && client.Delta.Sent[0] is LocalKarmaOutcomeDelta,"local inventory/dialogue event reports to host");
ElinDelta.IsApplying=true; PersonalKarma.BeforeChange(-1);
Check(client.Delta.Sent.Count==1,"unscoped network replay cannot report karma again");
ThingRequest.IsReplayingIntent=true; PersonalKarma.BeforeChange(-1);
Check(client.Delta.Sent.Count==2,"local inventory callback after ThingRequest remains an intent");
ThingRequest.IsReplayingIntent=false; ElinDelta.IsApplying=false;
using(PersonalKarma.For(c,true)) PersonalKarma.BeforeChange(3);
Check(client.Delta.Sent.Count==3,"client-only dynamic tool callback reports native outcome");
NetSession.Instance.Connection=host; EClass.pc=h; NetSession.Instance.Player=h;
var first=(LocalKarmaOutcomeDelta)client.Delta.Sent[0]; first.OriginPeer=7; first.Apply(host);
Check(PersonalKarma.Get(c)==99,"host applies UI event to authenticated sender only");
first.Apply(host); Check(PersonalKarma.Get(c)==99,"duplicate UI event cannot charge twice");
var third=(LocalKarmaOutcomeDelta)client.Delta.Sent[2]; third.OriginPeer=7; third.Apply(host);
Check(PersonalKarma.Get(c)==99 && host.Delta.Deferred.Count==1,"out-of-order UI event waits for preceding event");
var second=(LocalKarmaOutcomeDelta)client.Delta.Sent[1]; second.OriginPeer=7; second.Apply(host); third.Apply(host);
Check(PersonalKarma.Get(c)==100 && c.GetInt(PersonalKarma.SequenceKey)==3,"ordered UI outcomes preserve each signed event and clamp once per event");
NetSession.Instance.Connection=client; EClass.pc=c; NetSession.Instance.Player=c;
client.Delta.Sent.Clear();
PersonalKarma.Receive(new() {ActorUid=c.uid,Value=100,Revision=c.GetInt(PersonalKarma.RevisionKey),AcknowledgedSequence=1});
Check(client.Delta.Sent.Cast<LocalKarmaOutcomeDelta>().Select(e=>e.Sequence).SequenceEqual(new[]{2,3}),"unacknowledged local outcomes are retried after a cleared network buffer");
client.Delta.Sent.Clear();
PersonalKarma.Receive(new() {ActorUid=c.uid,Value=100,Revision=c.GetInt(PersonalKarma.RevisionKey),AcknowledgedSequence=3});
Check(client.Delta.Sent.Count==0,"host acknowledgement stops local outcome retries");
NetSession.Instance.Connection=host; EClass.pc=h; NetSession.Instance.Player=h;
PersonalKarma.BeginSession(c); first.Apply(host);
Check(PersonalKarma.Get(c)==100 && c.GetInt(PersonalKarma.SequenceKey)==0,"reconnect token rejects stale prior-session request");
new LocalKarmaOutcomeDelta {Session=c.GetStr(PersonalKarma.SessionKey),Sequence=1,Amount=-1,OriginPeer=123}.Apply(host);
Check(PersonalKarma.Get(c)==100,"unknown sender cannot modify personal karma");
// Inspect and transform the REAL installed vanilla IL, without launching Unity.
var game = @"C:\Program Files (x86)\Steam\steamapps\common\Elin";
var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var build = Path.Combine(repo, "build/personal-karma");
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

List<CodeInstruction> ReadIL(MethodInfo method) {
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

return original;
}
var eligibility=mod.GetType("ElinTogether.Patches.KarmaEligibilityPatch",true)!;
var targetMethods=(IEnumerable<MethodBase>)eligibility.GetMethod("TargetMethods",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null)!;
foreach(var target in targetMethods.Cast<MethodInfo>()) {
 var before=ReadIL(target);
 var after=((IEnumerable<CodeInstruction>)eligibility.GetMethod("Transpile",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{before.Select(i=>new CodeInstruction(i)).ToList()})!).ToList();
 var repeated=((IEnumerable<CodeInstruction>)eligibility.GetMethod("Transpile",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{after.Select(i=>new CodeInstruction(i)).ToList()})!).ToList();
 Check(after.Zip(repeated).All(pair=>pair.First.opcode==pair.Second.opcode && Equals(pair.First.operand,pair.Second.operand)),"eligibility transform is idempotent: "+target.Name);
 int routes=after.Count(i=>i.operand is MethodInfo m && m.DeclaringType==eligibility && m.Name=="IsPlayer");
 Check(routes==1,"actual IL changes one karma eligibility gate: "+target.DeclaringType!.Name+"."+target.Name);
 Check(before.Count(i=>i.operand is MethodInfo m && m.Name=="ModKarma")==after.Count(i=>i.operand is MethodInfo m && m.Name=="ModKarma"),"native karma events retained: "+target.Name);
}
var scope=mod.GetType("ElinTogether.Patches.KarmaActorScopePatch",true)!;
var scopes=((IEnumerable<MethodBase>)scope.GetMethod("TargetMethods",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null)!).ToArray();
foreach(var type in new[]{"TaskMine","TaskDig","Progress_Custom"})
 Check(scopes.Any(m=>m.DeclaringType!.Name==type && m.Name=="OnProgressComplete"),"actor scope covers completion: "+type);
Check(scopes.All(m=>m!=null),"all scope targets resolve against installed game");
Check(scopes.Any(m=>m.DeclaringType!.Name=="ActRestrain" && m.Name=="Perform" && m.GetParameters().Length==0),"direct parameterless act execution has an actor scope");
var guard=mod.GetType("ElinTogether.Patches.PersonalGuardTargetPatch",true)!;
var hostility=vanilla.GetType("Chara",true)!.GetMethod("IsHostile",new[]{vanilla.GetType("Chara",true)!})!;
var guardIL=ReadIL(hostility);
var patchedGuard=((IEnumerable<CodeInstruction>)guard.GetMethod("Transpile",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{guardIL})!).ToList();
Check(patchedGuard.Count(i=>i.operand is MethodInfo m && m.DeclaringType==guard)==1,"guard IL uses actual target's criminal check exactly once");
Check(patchedGuard.Count==guardIL.Count+1,"guard rewrite preserves all other hostility logic");
var batPatch=mod.GetType("ElinTogether.Patches.KarmaBatEligibilityPatch",true)!;
var batMethod=vanilla.GetType("ConTransmuteBat",true)!.GetMethod("CheckSeen")!;
var batIL=ReadIL(batMethod);
var batAfter=((IEnumerable<CodeInstruction>)batPatch.GetMethod("Transpile",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{batIL})!).ToList();
Check(batAfter.Count(i=>i.operand is MethodInfo m && m.DeclaringType==batPatch)==1,"bat exposure recognizes actual remote transformer");
Check(batAfter.Count(i=>i.operand is MethodInfo m && m.Name=="TryWitnessCrime")==batIL.Count(i=>i.operand is MethodInfo m && m.Name=="TryWitnessCrime"),"bat patch preserves single native witness roll");
Console.WriteLine($"{checks} personal karma checks passed; live multiplayer testing still required.");
