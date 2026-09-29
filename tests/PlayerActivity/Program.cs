using ElinTogether.Helper;
using ElinTogether.Elements;
using ElinTogether.Net;
using ElinTogether.Patches;
int passed=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
bool Pauses(bool vanilla=true){PauseGame.OnGetShouldPauseGame(ref vanilla);return vanilla;}
var host=new ElinNetHost();NetSession.Instance.Connection=host;
var local=new Chara{ai=new AutoAct()};var remote=new Chara{ai=new GoalRemote()};
host.ActiveRemoteCharas[1]=remote;host.States[1]=new();
void Report()=>host.States[1].LastAct=PlayerActivity.ReportAct(local);
Report();Check(!Pauses(),"reported controller wakes idle host before a child arrives");
var root=(GoalRemote)remote.ai;root.child=new TaskHarvest();Check(!Pauses(),"accepted child keeps host active");
root.child=null;Report();Check(!Pauses(),"gap after completion keeps time moving to next target");
local.ai.running=false;Report();Check(Pauses(),"finished controller reports idle without another game tick");
local.ai=new NoGoal();Report();Check(Pauses(),"manual cancellation permits idle pause");
root.child=new TaskHarvest();Check(!Pauses(),"in-flight running task survives an earlier idle report");
root.child.running=false;Check(Pauses(),"completed child does not hold time open");root.child=null;
local.ai=new AutoAct();Report();remote.isDead=true;Check(Pauses(),"dead remote cannot keep time moving");remote.isDead=false;
remote.IsInActiveMap=false;Check(Pauses(),"remote in another map excluded");remote.IsInActiveMap=true;
host.States[1].LastAct=999;Check(Pauses(),"unknown action ID excluded");
host.States[1].LastAct=3;Check(Pauses(),"non-AI action type excluded");
local.ai=new UnknownAct();Report();Check(Pauses(),"unmapped action retains idle fallback");
local.ai=new AutoAct();local.isDead=true;Check(PlayerActivity.ReportAct(local)==0,"dead local reports idle");local.isDead=false;Report();
ActionModeCombat.Phase=ActionModeCombat.CombatPhase.Deciding;Check(Pauses(false),"combat decision wait overrides activity and vanilla clock");
ActionModeCombat.Phase=ActionModeCombat.CombatPhase.Executing;Check(!Pauses(),"combat dispatch can advance");
ActionModeCombat.Phase=ActionModeCombat.CombatPhase.Advancing;Check(!Pauses(),"combat settlement can advance");
ActionModeCombat.Phase=ActionModeCombat.CombatPhase.Inactive;
host.ActiveRemoteCharas.Clear();Check(Pauses(),"disconnect ignores retained peer report");
Check(!Pauses(false),"disabled auto-pause remains disabled");
NetSession.Instance.Connection=null;Check(Pauses(),"single-player idle pause retained");
NetSession.Instance.Connection=new object();EClass.pc.party.members.Add(remote);root.child=new TaskHarvest();
Check(!Pauses(),"client still considers running remote task");root.child=null;
Check(Pauses(),"client idle task behavior retained");
// Reproduce the lifecycle gap: a completed root reports idle but still fails
// vanilla's HasNoGoal input gate. Cleanup cannot depend on a character tick.
NetSession.Instance.Connection=host;host.ActiveRemoteCharas[1]=remote;
local.IsPC=true;local.ai=new AutoAct{running=false};Report();
Check(Pauses()&&!local.HasNoGoal,"finished controller can leave idle host with blocked local input");
PlayerActivity.ClearFinishedLocalGoal(local);
Check(local.HasNoGoal&&local.Resets==1&&PlayerActivity.ReportAct(local)==0,"frame cleanup restores input without changing idle report");
PlayerActivity.ClearFinishedLocalGoal(local);Check(local.Resets==1,"idle cleanup is idempotent");
local.ai=new AutoAct();var active=local.ai;PlayerActivity.ClearFinishedLocalGoal(local);
Check(local.ai==active&&local.Resets==1,"running controller waiting on host is preserved");
foreach(var phase in new[]{ActionModeCombat.CombatPhase.Inactive,ActionModeCombat.CombatPhase.Deciding,ActionModeCombat.CombatPhase.Executing,ActionModeCombat.CombatPhase.Advancing}) {
 ActionModeCombat.Phase=phase;local.ai=new AutoAct{running=false};
 PlayerActivity.ClearFinishedLocalGoal(local);Check(local.HasNoGoal,$"finished local goal released in {phase}");
}
local.isDead=true;local.ai=new AutoAct{running=false};var deadGoal=local.ai;
PlayerActivity.ClearFinishedLocalGoal(local);Check(local.ai==deadGoal,"dead player's goal left to death lifecycle");
remote.ai=new TaskHarvest{running=false};var remoteGoal=remote.ai;
PlayerActivity.ClearFinishedLocalGoal(remote);Check(remote.ai==remoteGoal,"remote character's goal is never cleared by local cleanup");
// Scene combines UI pause and action-mode pause with OR. Exercise the production
// UI patch while a remote goal/combat phase wants time to continue.
bool UiPauses(bool vanilla=true){PauseGame.GetIsPauseGame(new UI(),ref vanilla);return vanilla;}
foreach(var connection in new object[]{host,new object()}) {
 NetSession.Instance.Connection=connection;
 foreach(var phase in new[]{ActionModeCombat.CombatPhase.Inactive,ActionModeCombat.CombatPhase.Executing,ActionModeCombat.CombatPhase.Advancing}) {
  ActionModeCombat.Phase=phase;Game.isPaused=false;
  Check(!UiPauses(),$"ordinary menu does not pause MP in {phase}");
  Game.isPaused=true;
  Check(UiPauses(false),$"mandatory pause wins even with non-pausing top layer in {phase}");
  Game.isPaused=false;
  Check(!UiPauses(),$"closing mandatory pause restores MP menu policy in {phase}");
 }
}
// Model only the vanilla event/UI boundary: an expired quest queues a callback
// every event round; GameUpdater.Update stops event rounds on scene.paused.
// This is not a Unity test of actual layer destruction or zone activation.
NetSession.Instance.Connection=host;ActionModeCombat.Phase=ActionModeCombat.CombatPhase.Inactive;
host.ActiveRemoteCharas[1]=remote;remote.ai=new GoalRemote{child=new TaskHarvest()};
Game.isPaused=false;int pendingReturns=0;
for(int frame=0;frame<5;frame++) {
 bool scenePaused=UiPauses(false)||Pauses();
 if(!scenePaused){pendingReturns++;Game.isPaused=true;}
}
Check(pendingReturns==1,"expired quest cannot accumulate return callbacks while remote remains busy");
Game.isPaused=false;Check(!UiPauses(false)&&!Pauses(),"world can resume after mandatory pause closes");
NetSession.Instance.Connection=null;
foreach(bool hardPause in new[]{false,true}) {
 Game.isPaused=hardPause;
 Check(UiPauses(true)&&!UiPauses(false),"disconnected UI preserves vanilla result");
}
Game.isPaused=false;
Console.WriteLine($"{passed} checks passed; simulated game/network boundary.");
