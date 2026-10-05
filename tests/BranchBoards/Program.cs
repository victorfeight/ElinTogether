using ElinTogether.Models;
using ElinTogether.Net;
using Mono.Cecil;
using Mono.Cecil.Cil;

int checks = 0;
void Check(bool ok, string why) { if (!ok) throw new Exception(why); Console.WriteLine("PASS " + why); checks++; }
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(args.Single()));
using var module = ModuleDefinition.ReadModule(args.Single(), new ReaderParameters { AssemblyResolver = resolver });
TypeDefinition Type(string name) => module.Types.Single(t => t.Name == name);
Check(Type("EClass").CustomAttributes.Any(a => a.AttributeType.Name == "JsonObjectAttribute" &&
    a.ConstructorArguments.Count == 1 && Convert.ToInt32(a.ConstructorArguments[0].Value) == 1),
    "installed EClass declares inherited opt-in JSON serialization");
foreach (var (manager, owner) in new[] { ("ResearchManager", "branch"), ("PolicyManager", "owner") })
    Check(Type(manager).BaseType.Name == "EClass" && !Type(manager).Fields.Single(f => f.Name == owner).HasCustomAttributes,
        $"{manager} inherits native saved-field serialization without its owner back-reference");
bool Calls(MethodDefinition m, string name) => m.HasBody && m.Body.Instructions.Any(i => i.Operand is MethodReference r && r.Name == name);
foreach (var (name, call) in new[] { ("LayerHome", "ModBase"), ("ItemResearch", "CompletePlan") }) {
    var callbacks = Type(name).NestedTypes.SelectMany(t => t.Methods).Where(m => Calls(m, call)).ToArray();
    Check(callbacks.Length == 1, $"installed {name} has exactly one complete purchase callback");
    Check(Calls(callbacks.Single(), name == "LayerHome" ? "ModCurrency" : "Mod"), $"{name} payment lies inside intercepted callback");
    var capture = name == "LayerHome" ? "Element" : "ResearchPlan";
    IEnumerable<string> Captures(TypeDefinition t, HashSet<string> seen) {
        if (!seen.Add(t.FullName)) yield break;
        foreach (var f in t.Fields) {
            if (f.FieldType.Name == capture || f.FieldType.Name == name) yield return f.FieldType.Name;
            else if (f.FieldType is TypeDefinition nested && nested.DeclaringType == Type(name))
                foreach (var v in Captures(nested, seen)) yield return v;
        }
    }
    Check(Captures(callbacks[0].DeclaringType, []).Count(n => n == capture) == 1, $"{name} callback has a unique {capture} capture path");
}
Check(Type("LayerPolicy").NestedTypes.SelectMany(t => t.Methods).Count(m => m.HasBody && m.Body.Instructions.Any(i => i.OpCode == OpCodes.Stfld && i.Operand is FieldReference f && f.DeclaringType.Name == "Policy" && f.Name == "active")) == 1,
    "installed policy callback can be intercepted before toggling or refreshing effects");
foreach (var name in new[] { "chara_hired", "chara_hired_ticket" }) {
    var m = Type("DramaOutcome").Methods.Single(m => m.Name == name);
    Check(Calls(m, "Recruit") && Calls(m, name.EndsWith("ticket") ? "ModNum" : "ModCurrency"), $"native {name} pairs payment with resident recruitment");
}
Check(Calls(Type("LayerQuestBoard").Methods.Single(m => m.Name == "OnInit"), "UpdateReqruits"), "opening hiring board reaches candidate-generation gate");

var host = new ElinNetHost(); var client = new ElinNetClient();
var hostActor = EClass.pc; var actor = new Chara { uid = 719 }; host.ActiveRemoteCharas[1] = actor;
NetSession.Instance.Connection = host;
var branch = EClass.Branch;
branch.elements.SetBase(2000, 1);
BranchBoardCommand Request(BranchBoardOperation op) => new() { Id = Guid.NewGuid(), OriginPeer = 1, ZoneUid = 10, Operation = op };
var upgrade = Request(BranchBoardOperation.Upgrade); upgrade.Target = 2000; upgrade.Expected = 1; upgrade.Price = 2;
upgrade.Apply(host); upgrade.Apply(host);
Check(actor.Money == 98 && actor.Payments == 1 && branch.elements.Base(2000) == 2, "upgrade charges requesting client and changes shared level once");
Check(hostActor.Payments == 0 && EClass.pc == hostActor, "host funds and actor context stay intact");
var stale = Request(BranchBoardOperation.Upgrade); stale.Target = 2000; stale.Expected = 1; stale.Price = 2; stale.Apply(host);
Check(actor.Payments == 1, "concurrent stale quote cannot charge");
var poor = Request(BranchBoardOperation.Upgrade); poor.Target = 2000; poor.Expected = 2; poor.Price = 3;
actor.Money = 0; poor.Apply(host); actor.Money = 98;
Check(branch.elements.Base(2000) == 2 && actor.Payments == 1, "insufficient money leaves upgrade unchanged");
var wrongZone = Request(BranchBoardOperation.Upgrade); wrongZone.ZoneUid = 9; wrongZone.Apply(host);
Check(actor.Payments == 1, "stale-zone action cannot spend");
var plan = new ResearchPlan(); branch.researches.plans.Add(plan);
var research = Request(BranchBoardOperation.Research); research.Plan = plan.id; research.Expected = 1; research.Price = 20;
research.Apply(host); research.Apply(host);
Check(branch.resources.knowledge.value == 80 && branch.researches.Completed == 1 && branch.researches.plans.Count == 0, "knowledge cost and research reward happen together once");
research.Id = Guid.NewGuid(); research.Apply(host);
Check(branch.resources.knowledge.value == 80, "stale completed research cannot charge again");
branch.researches.plans.Add(plan); branch.resources.knowledge.value = 10;
research.Id = Guid.NewGuid(); research.Apply(host);
Check(branch.researches.Completed == 1 && branch.resources.knowledge.value == 10, "insufficient knowledge prevents completion and deduction");
var policy = new Policy(); branch.policies.list.Add(policy);
var toggle = Request(BranchBoardOperation.Policy); toggle.Target = policy.id; toggle.Active = true;
toggle.Apply(host); toggle.Id = Guid.NewGuid(); toggle.Apply(host);
Check(policy.active && branch.policies.CurrentAP() == 2, "policy request sets desired state without double-click toggling back");
var second = new Policy { id = 5 }; branch.policies.list.Add(second);
toggle.Id = Guid.NewGuid(); toggle.Target = 5; toggle.Apply(host);
Check(!second.active, "host enforces administration capacity");
toggle.Id = Guid.NewGuid(); toggle.Target = 4; toggle.Active = false; toggle.Apply(host);
Check(!policy.active, "policy can be disabled without spending or AP restriction");
var candidate = new Chara { uid = 80 }; branch.listRecruit.Add(new() { chara = candidate });
var hire = Request(BranchBoardOperation.Hire); hire.Target = 80; hire.Price = 10;
hire.Apply(host); hire.Apply(host);
Check(actor.Money == 88 && candidate.HomeMember && branch.Hires == 1 && branch.listRecruit.Count == 0, "paid hire consumes client money and authoritative candidate exactly once");
hire.Id = Guid.NewGuid(); hire.Apply(host);
Check(actor.Money == 88, "second request for already-hired resident cannot charge");
var candidate2 = new Chara { uid = 81 }; branch.listRecruit.Add(new() { chara = candidate2 });
hire = Request(BranchBoardOperation.HireTicket); hire.Target = 81; hire.Price = 10;
hire.Apply(host);
Check(!candidate2.HomeMember, "missing ticket cannot hire");
actor.things.Ticket = new(); hire.Id = Guid.NewGuid(); hire.Apply(host);
Check(candidate2.HomeMember && actor.things.Ticket.Num == 0 && actor.Money == 88, "ticket hire consumes one ticket without charging money");
var forged = new Chara { uid = 99, Protected = true }; branch.listRecruit.Add(new() { chara = forged });
hire.Id = Guid.NewGuid(); hire.Target = 99; hire.Operation = BranchBoardOperation.Hire; hire.Apply(host);
Check(!forged.HomeMember && actor.Money == 88, "player characters cannot be hired through board requests");
var snapshot = BranchBoardState.Capture();
EClass.Branch = new(); NetSession.Instance.Connection = client;
snapshot.Apply(client); snapshot.Apply(client);
Check(EClass.Branch.elements.Base(2000) == 2 && EClass.Branch.resources.knowledge.value == 10,
    "client receives final upgrade and resource state without replaying purchases");
Check(EClass.Branch.researches.Completed == 1 && EClass.Branch.listRecruit.Single().chara == forged,
    "research and candidate results retain existing character identity");
Check(actor.Money == 88 && hostActor.Money == 100 && client.Delta.Items.Count == 0, "result replay emits no payments, requests or duplicated costs");
EClass.Branch = branch; NetSession.Instance.Connection = host;
var beforeLevel = branch.elements.Base(2000); snapshot.Elements[2000] = 999; snapshot.Apply(host);
Check(branch.elements.Base(2000) == beforeLevel, "host ignores client-supplied state snapshots");
var exception = Request(BranchBoardOperation.Upgrade); exception.Target = 2000; exception.Expected = 2; exception.Price = 3;
branch.ThrowOnPrice = true;
try { exception.Apply(host); } catch (InvalidOperationException) { }
branch.ThrowOnPrice = false;
Check(EClass.pc == hostActor && !BranchBoardManagement.Resolving && actor.Money == 88, "exception restores actor and transaction scope before later actions");
var pending = host.Delta.Items.Count;
ElinTogether.Patches.ZoneActivateEvent.IsHappening = true;
BranchBoardManagement.Publish(branch);
Check(host.Delta.Items.Count == pending, "zone activation does not publish partial settlement state");
ElinTogether.Patches.ZoneActivateEvent.IsHappening = false;
BranchBoardManagement.Publish(branch);
Check(host.Delta.Items.Count == pending + 1, "ordinary host settlement changes publish after activation");
Console.WriteLine($"{checks} settlement checks passed; native callback IL verified, gameplay/transport boundaries simulated.");
