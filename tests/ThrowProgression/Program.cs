using ElinTogether.Models;
using ElinTogether.Net;
using Mono.Cecil;

int checks = 0;
void Check(bool ok, string why) { if (!ok) throw new Exception(why); Console.WriteLine("PASS " + why); checks++; }
using var module = ModuleDefinition.ReadModule(args[0]);
MethodDefinition Method(string type, string name, int count = -1) => module.Types.Single(t => t.Name == type).Methods.Single(m =>
    m.Name == name && (count < 0 || m.Parameters.Count == count) &&
    (type != "ActThrow" || m.Parameters[2].ParameterType.Name == "Card") &&
    (type != "Card" || m.Parameters[0].ParameterType.Name == "Int32"));
bool Calls(MethodDefinition method, string type, string name) => method.Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == type && m.Name == name);
var nativeThrow = Method("ActThrow", "Throw", 5);
Check(Calls(nativeThrow, "Card", "ModExp"), "installed special throws use native raw XP awards");
Check(Calls(nativeThrow, "AttackProcess", "Perform"), "installed ordinary throws use combat outcome calculation");
Check(!nativeThrow.Body.Instructions.Any(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Stsfld &&
    i.Operand is FieldReference f && f.DeclaringType.Name == "Act" && f.Name == "CC"),
    "installed static throw leaves global actor context to its caller");
Check(nativeThrow.Body.Instructions.Count(i => i.Operand is MethodReference m && m.DeclaringType.Name == "Chara" && m.Name == "SetAI") == 1 &&
    nativeThrow.Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == "AI_PracticeDummy" && m.Name == ".ctor"),
    "installed throw has one native training-start task assignment");
if (args.Length > 1) {
    using var mod = ModuleDefinition.ReadModule(args[1]);
    var setAi = mod.Types.Single(t => t.Name == "CharaTaskRemoteEvent").Methods.Single(m => m.Name == "OnSetAI");
    var callsInOrder = setAi.Body.Instructions.Where(i => i.Operand is MethodReference).Select(i => (MethodReference)i.Operand).ToList();
    Check(callsInOrder[0].DeclaringType.Name == "ThrowTraining" && callsInOrder[0].Name == "AllowTask" &&
        callsInOrder.Any(m => m.Name == "PublishTask"), "built SetAI patch guards replay before any task publication");
}
Check(Calls(Method("Card", "ModExp", 2), "ElementContainer", "ModExp"), "card XP reaches the intercepted container boundary");
var nativeXp = Method("ElementContainer", "ModExp");
Check(nativeXp.Parameters[1].ParameterType.FullName == "System.Single", "native XP input is float, not truncated integer");
Check(Calls(nativeXp, "ElementContainer", "ModExp") && Calls(nativeXp, "ElementContainer", "OnLevelUp"), "owner processing retains native parent XP and level-up callbacks");
var attack = module.Types.Single(t => t.Name == "AttackProcess");
IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
Check(Types(attack).SelectMany(t => t.Methods).Any(m => m.HasBody && m.Name.Contains("ModExpAtk") && Calls(m, "Card", "ModExp")), "installed combat award reaches shared capture boundary");
var harvest = module.Types.Single(t => t.Name == "GrowSystem").Methods.Single(m => m.Name == "PopHarvest" && m.Parameters[1].ParameterType.Name == "Thing");
Check(Calls(harvest, "Card", "ModExpParty") && Calls(Method("Party", "ModExp"), "Card", "ModExp"), "native crop award distributes XP through each party member");
var craft = Types(module.Types.Single(t => t.Name == "AI_UseCrafter")).SelectMany(t => t.Methods)
    .Single(m => m.HasBody && Calls(m, "ElementContainer", "ModExp"));
var craftCall = craft.Body.Instructions.Single(i => i.Operand is MethodReference m && m.Name == "ModExp");
// The chain=false argument follows the XP expression on the evaluation stack.
Check(craftCall.Previous.Previous.OpCode.Code == Mono.Cecil.Cil.Code.Conv_R4 &&
    craftCall.Previous.Previous.Previous.OpCode.Code == Mono.Cecil.Cil.Code.Div,
    "installed crafting divides integers before conversion to float XP");
Check(RemoteCraft.Experience(1, 1) == 12 && RemoteCraft.Experience(2, 3) == 25 && RemoteCraft.Experience(5, 25) == 90,
    "remote crafting preserves native truncation for fractional and exact cases");

var host = new ElinNetHost(); var client = new ElinNetClient();
Chara Actor(int uid, bool remote = false) {
    var c = new Chara { uid = uid, IsRemotePlayer = remote };
    foreach (var id in new[] { 108, 132, 134, 10, 207 }) c.elements.dict[id] = new();
    return c;
}
var remote = Actor(719, true); var localHost = Actor(1); localHost.IsPC = true;
host.ActiveRemoteCharas[7] = remote;
var item = new Thing { uid = 90 }; var target = new Card { uid = 80 };
ActThrowDelta Request(Card owner, int peer = 7) => new() { Owner = owner, Point = new(), Target = target, Thing = item, Method = ThrowMethod.Default, OriginPeer = peer };
NetSession.Instance.Connection = host;
ActThrow.Native = c => ((Chara)c).elements.ModExp(108, 1);
ActThrow.Throw(localHost, new(), target, item, ThrowMethod.Default);
Check(localHost.elements.Native.SequenceEqual(new[] { (108, 1f) }) && OwnerProgressionAwards.Items.Count == 0, "host's own throws keep vanilla XP unchanged");
host.Delta.Items.Clear();
Request(remote).Apply(host);
Check(remote.elements.Native.Count == 0 && OwnerProgressionAwards.Items.Single().Amount == 1, "remote hit records actual raw award without host-side skill mutation");
Check(host.Delta.Items.Single() is ActThrowDelta, "existing throw result still reaches client");
var count = OwnerProgressionAwards.Items.Count;
ActThrow.Native = _ => { }; Request(remote).Apply(host);
Check(OwnerProgressionAwards.Items.Count == count, "miss or empty-ground throw with no native XP creates no compensation");
foreach (var raw in new[] { 50f, 100f, 2.75f }) {
    ActThrow.Native = c => ((Chara)c).elements.ModExp(108, raw);
    Request(remote).Apply(host);
    Check(OwnerProgressionAwards.Items.Last().Amount == raw, $"native award {raw} is preserved exactly");
}
ActThrow.Native = c => {
    ((Chara)c).elements.ModExp(132, 2);
    ((Chara)c).elements.ModExp(134, 3);
    localHost.elements.ModExp(10, 4);
};
Request(remote).Apply(host);
Check(OwnerProgressionAwards.Items.TakeLast(2).Select(a => a.Skill).SequenceEqual(new[] { 132, 134 }), "throw's tactics and critical-related skills route with Throwing");
Check(localHost.elements.Native.Last() == (10, 4f), "host defender or unrelated character XP is never intercepted");
var localClient = Actor(719); localClient.IsPC = true;
NetSession.Instance.Connection = client; count = OwnerProgressionAwards.Items.Count;
var calls = ActThrow.Calls;
ActThrow.Throw(localClient, new(), target, item, ThrowMethod.Default);
Check(ActThrow.Calls == calls && localClient.elements.Native.Count == 0 && client.Delta.Items.Count == 1, "client request sends throw but awards no speculative XP");
ActThrow.Native = c => ((Chara)c).elements.ModExp(108, 50);
Request(localClient).Apply(client); Request(localClient).Apply(client);
Check(localClient.elements.Native.Count == 0 && OwnerProgressionAwards.Items.Count == count, "repeated client throw replay awards no XP");
localClient.elements.ModExp(108, 2.75f);
Check(localClient.elements.Native.Single() == (108, 2.75f), "award delivery outside throw replay uses native owner progression");
NetSession.Instance.Connection = host; calls = ActThrow.Calls;
Request(localHost).Apply(host); Request(remote, 99).Apply(host);
Check(ActThrow.Calls == calls, "client cannot run a throw as host or another actor");
item.isDestroyed = true; Request(remote).Apply(host); item.isDestroyed = false;
Check(ActThrow.Calls == calls && TaskCache.Cancels == 1, "missing throw item causes cancellation without XP");
ActThrow.Native = _ => throw new InvalidOperationException("native failure");
try { Request(remote).Apply(host); } catch (InvalidOperationException) { }
remote.elements.ModExp(108, 3);
Check(remote.elements.Native.Last() == (108, 3f), "exception restores scope rather than intercepting later actions");
using (HostSkillProgression.Begin(host, remote)) {
    using (HostSkillProgression.Begin(client, localClient)) localClient.elements.ModExp(108, 50);
    remote.elements.ModExp(108, 6);
}
Check(OwnerProgressionAwards.Items.Last().Amount == 6, "nested replay scopes restore outer actor");
count = OwnerProgressionAwards.Items.Count;
using (HostSkillProgression.Begin(host, remote)) {
    remote.elements.ModExp(999, 10);
    remote.elements.ModExp(108, 0);
    remote.isDead = true; remote.elements.ModExp(108, 10); remote.isDead = false;
}
Check(OwnerProgressionAwards.Items.Count == count, "unlearned skill, zero XP and dead actor do not enqueue awards");
NetSession.Instance.Connection = null;
ActThrow.Native = c => ((Chara)c).elements.ModExp(108, 9);
ActThrow.Throw(localHost, new(), target, item, ThrowMethod.Default);
Check(localHost.elements.Native.Last() == (108, 9f), "solo throw progression stays native");
var npc = Actor(88);
var party = new Party { members = [localHost, remote, npc] };
localHost.party = remote.party = npc.party = party;
NetSession.Instance.Connection = host;
count = OwnerProgressionAwards.Items.Count;
remote.ModExpParty(207, 15);
Check(localHost.elements.Native.Last() == (207, 15f) && npc.elements.Native.Last() == (207, 15f), "client harvest retains native host and NPC party award");
Check(OwnerProgressionAwards.Items.Count == count + 1 && OwnerProgressionAwards.Items.Last() is { Skill: 207, Amount: 15 } award && award.Actor == remote,
    "client harvester receives its share through owner progression");
localHost.ModExpParty(207, 20);
Check(localHost.elements.Native.Last() == (207, 20f) && npc.elements.Native.Last() == (207, 20f) && OwnerProgressionAwards.Items.Last().Amount == 20,
    "host harvest awards the same party including remote member");
NetSession.Instance.Connection = client;
var hostCalls = localHost.elements.Native.Count; count = OwnerProgressionAwards.Items.Count;
localHost.ModExpParty(207, 20); remote.ModExpParty(207, 15);
Check(localHost.elements.Native.Count == hostCalls && OwnerProgressionAwards.Items.Count == count,
    "client party-award replay cannot distribute another copy");
NetSession.Instance.Connection = host; remote.party = null;
remote.ModExpParty(207, 12);
Check(OwnerProgressionAwards.Items.Last().Amount == 12 && localHost.elements.Native.Count == hostCalls,
    "actor without a party receives only its own native award");
remote.IsRemotePlayer = false;
remote.ModExpParty(207, 11);
Check(remote.elements.Native.Last() == (207, 11f), "host-controlled break-mode ally uses native progression");
NetSession.Instance.Connection = null;
localHost.ModExpParty(207, 17);
Check(localHost.elements.Native.Last() == (207, 17f) && npc.elements.Native.Last() == (207, 17f), "solo party distribution stays native");
// Reproduce a stale global actor and a throw arriving after cancellation. The
// boundary calls the production guard; the built patch order is checked above.
NetSession.Instance.Connection = client;
item.Returning = true; target.trait = new TraitTrainingDummy();
ActThrow.Native = _ => { };
localClient.ai = new AIAct();
ActThrow.Throw(localClient, new(), target, item, ThrowMethod.Default);
Check(localClient.ai is AI_PracticeDummy p && p.target == target && p.throwItem == item,
    "first deliberate client throw starts training without waiting for result replay");
var training = localClient.ai;
ActThrow.Throw(localClient, new(), target, item, ThrowMethod.Default);
Check(ReferenceEquals(training, localClient.ai), "subsequent client training throws retain the same loop");
localClient.ai = new AIAct(); var nextAction = localClient.ai;
Act.CC = localClient; Act.TC = localHost; Act.TP.Set(new Point { x=17, z=19 });
ActThrow.Native = c => {
    Check(Act.CC == c, "received throw establishes its actual actor");
    // Model the native SetAI branch without its eligibility checks: even an
    // eligible result must not create a new loop or overwrite an unrelated AI.
    localClient.SetAI(new AI_PracticeDummy());
};
Request(localClient).Apply(client);
Check(ReferenceEquals(nextAction, localClient.ai), "own late result cannot restart cancelled training or replace next action");
Request(localHost).Apply(client);
Check(ReferenceEquals(nextAction, localClient.ai), "another player's result cannot start client training");
Check(Act.CC == localClient && Act.TC == localHost && Act.TP.x == 17 && Act.TP.z == 19,
    "throw result restores actor, target and point context");
NetSession.Instance.Connection = host;
localHost.ai = new AIAct(); var hostNext = localHost.ai; Act.CC = localHost;
ActThrow.Native = c => {
    Check(Act.CC == remote, "host resolves client throw with remote actor rather than host");
    localHost.SetAI(new AI_PracticeDummy());
};
Request(remote).Apply(host);
Check(ReferenceEquals(hostNext, localHost.ai) && Act.CC == localHost,
    "client's continued throwing cannot restart host training");
ActThrow.Native = _ => throw new InvalidOperationException("throw replay failed");
try { Request(remote).Apply(host); } catch (InvalidOperationException) { }
Check(Act.CC == localHost && ThrowTraining.AllowTask(new AI_PracticeDummy()),
    "failed replay restores actor and releases training guard");
var depth = 0;
ActThrow.Native = c => {
    Check(ThrowTraining.AllowTask(new AIAct()), "throw replay does not block unrelated task types");
    if (depth++ == 0) {
        ThrowTraining.Replay(localClient, new Point(), target, item, ThrowMethod.Default);
        Check(Act.CC == remote && !ThrowTraining.AllowTask(new AI_PracticeDummy()),
            "nested throw restores outer actor and keeps outer training guard");
    }
};
Request(remote).Apply(host);
Check(Act.CC == localHost && ThrowTraining.AllowTask(new AI_PracticeDummy()),
    "nested throw releases context and guard after outer completion");
NetSession.Instance.Connection = client;
foreach (var reason in new[] { "ordinary item", "ordinary target", "exhausted", "indestructible" }) {
    item.Returning = reason != "ordinary item";
    target.trait = reason == "ordinary target" ? new Trait() : new TraitTrainingDummy();
    localClient.stamina.value = reason == "exhausted" ? 0 : 10;
    item.trait.CanBeDestroyed = reason != "indestructible";
    localClient.ai = new AIAct(); var before = localClient.ai;
    ThrowTraining.StartClient(localClient, target, item);
    Check(ReferenceEquals(before, localClient.ai), $"vanilla training eligibility rejects {reason}");
}
item.Returning = true; item.trait.CanBeDestroyed = true; localClient.stamina.value = 10;
target.trait = new Trait(); target.IsRestrainedResident = true;
ThrowTraining.StartClient(localClient, target, item);
Check(localClient.ai is AI_PracticeDummy, "restrained-resident training remains eligible");
NetSession.Instance.Connection = null;
localHost.SetAI(new AI_PracticeDummy());
Check(localHost.ai is AI_PracticeDummy, "native solo training assignment remains allowed");
Console.WriteLine($"{checks} throw progression/training checks passed; installed native IL checked, game/network boundaries simulated.");
