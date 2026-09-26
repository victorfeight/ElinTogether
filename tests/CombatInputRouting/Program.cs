using System.Reflection;
using ElinTogether.Patches;
using ElinTogether.Models;

void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS "+name); }
bool Invoke(string name, params object?[] args) => (bool)typeof(CombatInputBuffer)
    .GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args)!;
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
Check(((DynamicAIAct)ActionModeCombat.Pending!).Execute() && act.performed==1,
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
Check(((DynamicAIAct)ActionModeCombat.Pending!).Execute() && pc.mana==mana-1 && spell.performed==1,
    "ability pays mana and applies effect when dispatched");
CombatInputBuffer.BeginInput(out outer);
Invoke("BeforeAct",act,pc,target,new Point(),false);
CombatInputBuffer.EndInput(outer);
gun.root=new Chara();
Check(!((DynamicAIAct)ActionModeCombat.Pending!).Execute() && act.performed==1,
    "queued shot cannot use a weapon transferred to another player");
gun.root=pc;
CombatInputBuffer.BeginInput(out outer);
Invoke("BeforeAbility",pc,spell,target,new Point(),false,false);
CombatInputBuffer.EndInput(outer);
target.isDestroyed=true; mana=pc.mana;
Check(!((DynamicAIAct)ActionModeCombat.Pending!).Execute() && pc.mana==mana,
    "destroyed queued target does not consume mana");
Check(Invoke("BeforeAct",act,pc,target,new Point(),false),"scripted effects outside input remain immediate");
CombatInputBuffer.BeginInput(out outer);
ActionModeCombat.Activated=false;
Check(Invoke("BeforeAct",act,pc,target,new Point(),false),"turn mode inactive retains vanilla input");
CombatInputBuffer.EndInput(outer);
