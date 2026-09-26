using System.Reflection;
using ElinTogether.Patches;
using ElinTogether.Models;

void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS "+name); }
bool Invoke(string name, params object?[] args) => (bool)typeof(CombatInputBuffer)
    .GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args)!;
bool ExecutePending() {
    if (ActionModeCombat.Pending is QueuedCombatAct queued && !queued.CanStart()) return false;
    using var run = ActionModeCombat.Pending!.Run().GetEnumerator();
    return run.MoveNext() && run.Current == AIAct.Status.Success;
}
var probeCalls=0;
using (var run = new QueuedCombatAct("probe",()=>{probeCalls++; return true;}).Run().GetEnumerator()) {
 Check(run.MoveNext() && run.Current==AIAct.Status.Success && probeCalls==1,
    "instant queued action runs on first tick without DynamicAIAct wait");
 Check(!run.MoveNext() && probeCalls==1,"queued action completion does not repeat effect");
}
var pc=EClass.pc;
var gun=new Thing{root=pc}; pc.ranged=gun;
var target=new Card(); var act=new ActRanged();
CombatInputBuffer.BeginInput(out var outer);
Check(!Invoke("BeforeAct",act,pc,target,new Point(),false) && act.performed==0,
    "Fire shortcut is buffered before damage or ammunition effects");
Check(!Invoke("BeforeEndTurn"),"unconditional EndTurn cannot overwrite buffered input");
CombatInputBuffer.BeginInput(out var inner);
CombatInputBuffer.EndInput(inner);
Check(!Invoke("BeforeEndTurn"),"nested input scopes retain deferred action");
CombatInputBuffer.EndInput(outer);
Check(Invoke("BeforeEndTurn"),"input finalizer clears deferred flag");
ActionModeCombat.IsDispatching=true;
Check(ExecutePending() && act.performed==1,
    "scheduled Fire performs its effect once");
CombatInputBuffer.BeginInput(out outer);
Check(Invoke("BeforeAct",act,pc,target,new Point(),false),"scheduled execution is not recursively buffered");
ActionModeCombat.IsDispatching=false;
ElinDelta.IsApplying=true;
Check(Invoke("BeforeAct",act,pc,target,new Point(),false),"network replay is not captured as user input");
ElinDelta.IsApplying=false;
var spell=new Act(); var mana=pc.mana;
Check(!Invoke("BeforeAbility",pc,spell,target,new Point(),false,false) && pc.mana==mana && spell.performed==0,
    "ability shortcut is buffered before mana payment");
CombatInputBuffer.EndInput(outer);
Check(ExecutePending() && pc.mana==mana-1 && spell.performed==1,
    "ability pays mana and applies effect when dispatched");
CombatInputBuffer.BeginInput(out outer);
Invoke("BeforeAct",act,pc,target,new Point(),false);
CombatInputBuffer.EndInput(outer);
gun.root=new Chara();
Check(!ExecutePending() && act.performed==1,
    "queued shot cannot use a weapon transferred to another player");
gun.root=pc;
CombatInputBuffer.BeginInput(out outer);
Invoke("BeforeAbility",pc,spell,target,new Point(),false,false);
CombatInputBuffer.EndInput(outer);
target.isDestroyed=true; mana=pc.mana;
Check(!ExecutePending() && pc.mana==mana,
    "destroyed queued target does not consume mana");
Check(Invoke("BeforeAct",act,pc,target,new Point(),false),"scripted effects outside input remain immediate");
CombatInputBuffer.BeginInput(out outer);
ActionModeCombat.Activated=false;
Check(Invoke("BeforeAct",act,pc,target,new Point(),false),"turn mode inactive retains vanilla input");
CombatInputBuffer.EndInput(outer);

ActionModeCombat.Activated=true;
target.isDestroyed=false;
var victim = new Chara();
var queuedShot = CombatInputBuffer.CreateAct(act, pc, victim, new Point());
Check(queuedShot.CanStart(), "living target passes preflight");
victim.isDead=true;
Check(!queuedShot.CanStart() && act.performed==1, "partner kill invalidates queued shot before vanilla tick");
act.valid=false;
Check(!CombatInputBuffer.CreateAct(act,pc,target,new Point()).CanStart(), "vanilla range/LOS validation rejects stale input before tick");
act.valid=true;
CombatInputBuffer.BeginInput(out outer);
Invoke("BeforeAbility",pc,spell,target,new Point(),false,false);
CombatInputBuffer.EndInput(outer);
pc.cooldown=true;
Check(!ExecutePending() && pc.mana==mana, "cooldown blocks queued ability before spending mana or entering tick");
pc.cooldown=false;
using (var run = new QueuedCombatAct("failed",()=>false).Run().GetEnumerator()) {
 Check(run.MoveNext() && run.Current==AIAct.Status.Fail,"execution failure is not reported as AI success");
}
