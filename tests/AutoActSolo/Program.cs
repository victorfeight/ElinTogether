using AutoActMod.Actions;
using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
using ElinTogether.Elements;
int passed=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
var client=new ElinNetClient();NetSession.Instance.Connection=client;
Check(!AutoActAllyGuard.Before(new Chara()),"watching clients cannot run their own Ally Expansion assignments");
var owner=new Chara{IsPC=true};
var controller=new AutoActHarvestMine{owner=owner}; owner.ai=controller;
TaskHarvest Harvest(){var t=new TaskHarvest{owner=owner,parent=controller};controller.child=t;return t;}
AIProgress Progress(AIAct t){var p=new AIProgress{owner=owner,parent=t};t.child=p;return p;}
var task=Harvest();var progress=Progress(task);
CharaTaskProgressEvents.OnProgressBegin(progress);
Check(client.Delta.Items.Count==2&&client.Delta.Items[0] is CharaTaskDelta&&client.Delta.Items[1] is CharaProgressBeginDelta,"child request precedes progress request");
Check(progress.progress==HeldProgress.Held,"client waits for authoritative completion");
var request=(CharaTaskDelta)client.Delta.Items[0];
var reconstructed=(TaskHarvest)request.TaskArgs!.CreateSubAct();
Check(reconstructed.pos.X==2&&reconstructed.mode==task.mode&&reconstructed.target==task.target,"existing harvest serializer preserves target and mode");
client.Delta.Items.Clear();task.pos=new(8,9);progress=Progress(task);CharaTaskProgressEvents.OnProgressBegin(progress);
Check(((TaskHarvestArgs)((CharaTaskDelta)client.Delta.Items[0]).TaskArgs!).Pos.X==8,"same task instance republishes new target");
var mine=new TaskMine{parent=controller,owner=owner,pos=new(11,12)};controller.child=mine;client.Delta.Items.Clear();CharaTaskProgressEvents.OnProgressBegin(Progress(mine));
Check(((CharaTaskDelta)client.Delta.Items[0]).TaskArgs!.CreateSubAct() is TaskMine {pos.X:11},"harvest-to-mine transition uses existing mine serializer");
owner.IsPC=false;Check(AutoActTaskBridge.FindController(owner,mine)==null,"NPC/remote characters excluded");owner.IsPC=true;
var publishingHost=new ElinNetHost();NetSession.Instance.Connection=publishingHost;
Check(AutoActTaskBridge.FindController(owner,mine)==null,"host excluded from client controller lifecycle");
foreach(var isPc in new[]{true,false}) {
 owner.IsPC=isPc;
 foreach(var action in new AIAct[]{Harvest(),mine}) {
  controller.child=action;publishingHost.Delta.Items.Clear();
  CharaTaskProgressEvents.OnProgressBegin(Progress(action));
  Check(publishingHost.Delta.Items.Count==2 && publishingHost.Delta.Items[0] is CharaTaskDelta && publishingHost.Delta.Items[1] is CharaProgressBeginDelta,
   $"host {(isPc?"human":"NPC")} {action.GetType().Name} publishes task before progress");
  var sent=(CharaTaskDelta)publishingHost.Delta.Items[0];
  Check(sent.TaskArgs!.CreateSubAct().GetType()==action.GetType(),"host child uses existing concrete task serializer");
  // Emulate the receiver's reconstructed task. The real completion delta must
  // invoke the terrain callback, not just its attached item updates.
  var receiver=new Chara{IsPC=false};var remote=new GoalRemote{owner=receiver};receiver.ai=remote;
  var replayTask=sent.TaskArgs.CreateSubAct();replayTask.owner=receiver;remote.child=replayTask;
  bool terrainChanged=false,itemChanged=false;
  replayTask.child=new AIProgress{owner=receiver,parent=replayTask,Complete=()=>terrainChanged=true};
  new CharaProgressCompleteDelta{Owner=receiver,CompletedActId=action is TaskHarvest?1:2,
   DeltaList=[new Callback(()=>{Check(terrainChanged,"terrain completion precedes attached item results");itemChanged=true;})]}.Apply(client);
  Check(terrainChanged&&itemChanged,"host/ally completion replays terrain and attached results");
 }
}
publishingHost.ActiveRemoteCharas[1]=owner;publishingHost.Delta.Items.Clear();
AutoActTaskBridge.PublishChild(owner,mine);
Check(publishingHost.Delta.Items.Count==0,"host cannot publish a remote human's stale Auto Act controller");
Check(!AutoActAllyGuard.Before(owner),"Ally Expansion cannot assign work to a connected human");
publishingHost.Companions.Add(owner);
Check(AutoActAllyGuard.Before(owner),"host Ally Expansion may assign work during an explicit break");
AutoActTaskBridge.PublishChild(owner,mine);
Check(publishingHost.Delta.Items.Count==1 && publishingHost.Delta.Items[0] is CharaTaskDelta,
 "connected AI companion publishes Auto Act steps through normal host terrain synchronization");
publishingHost.Companions.Remove(owner);publishingHost.Delta.Items.Clear();
Check(!AutoActAllyGuard.Before(owner),"taking control immediately revokes Ally Expansion assignment");
AutoActTaskBridge.PublishChild(owner,mine);
Check(publishingHost.Delta.Items.Count==0,"taking control back revokes host Auto Act publishing immediately");
publishingHost.ActiveRemoteCharas.Clear();owner.IsPC=true;
NetSession.Instance.Connection=null;Check(AutoActTaskBridge.FindController(owner,mine)==null,"single player untouched");AutoActTaskBridge.PublishChild(owner,mine);NetSession.Instance.Connection=client;
owner.IsPC=false;client.Delta.Items.Clear();AutoActTaskBridge.PublishChild(owner,mine);
Check(client.Delta.Items.Count==0,"client cannot publish NPC automation");owner.IsPC=true;
var stale=new TaskMine{parent=new AutoActHarvestMine()};Check(AutoActTaskBridge.FindController(owner,stale)==null,"detached old controller excluded");
var other=new AutoActOther();owner.ai=other;mine.parent=other;Check(AutoActTaskBridge.FindController(owner,mine)==other,"other Auto Act controllers share lifecycle support");owner.ai=controller;mine.parent=controller;
client.Delta.Items.Clear();ElinDelta.IsApplying=true;AutoActTaskBridge.PublishChild(owner,mine);ElinDelta.IsApplying=false;Check(client.Delta.Items.Count==0,"replay does not submit a fresh task");
// Completion must land results without ticking or discarding either level of local automation.
task=Harvest();progress=Progress(task);bool landed=false;
new CharaProgressCompleteDelta{Owner=owner,CompletedActId=1,DeltaList=[new Callback(()=>{landed=true;Check(task.Ticks==0&&controller.Ticks==0,"results apply before any task/controller tick");})]}.Apply(client);
Check(landed&&progress.status==AIAct.Status.Success&&owner.ai==controller&&owner.Resets==0&&task.Ticks==0,"completion preserves controller and leaves continuation to normal scheduler");
// Existing non-Auto-Act completion still finishes and clears its root task.
var regular=new TaskHarvest{owner=owner};owner.ai=regular;Progress(regular);
new CharaProgressCompleteDelta{Owner=owner,CompletedActId=1,DeltaList=[]}.Apply(client);
Check(regular.Ticks==1&&owner.Resets==1,"ordinary task completion unchanged");
owner.ai=controller;controller.owner=owner;task=Harvest();Progress(task);
new CharaTaskCancelDelta{Owner=owner,ActId=1}.Apply(client);
Check(task.Cancels==1&&controller.Cancels==1&&owner.ai is NoGoal,"host rejection cancels both task and controller without retry");
Check(client.Delta.Items.Last() is CharaTaskDelta{TaskArgs:null},"stop requests host task cleanup through existing NoTask path");
var replacement=new AutoActHarvestMine();owner.ai=replacement;var resets=owner.Resets;AutoActTaskBridge.Stop(owner,controller);
Check(owner.ai==replacement&&owner.Resets==resets,"old controller stop cannot clear a replacement");
// The existing cancellation delta relays a client stop and cancels its host task.
var host=new ElinNetHost();var hostOwner=new Chara{IsPC=false};var hostRoot=new GoalRemote{owner=hostOwner};hostOwner.ai=hostRoot;
var hostTask=new TaskMine{owner=hostOwner,parent=hostRoot};hostRoot.child=hostTask;Progress(hostTask);
new CharaTaskCancelDelta{Owner=hostOwner,ActId=2}.Apply(host);
Check(hostTask.Cancels==1&&host.Delta.Items.Single() is CharaTaskCancelDelta,"client stop cancels host child and relays acknowledgement");
Check(hostOwner.ai==hostRoot&&hostOwner.Resets==0,"cancellation preserves host remote controller");
// A delayed completion still applies authoritative world changes after a manual stop.
owner.ai=new NoGoal();landed=false;
new CharaProgressCompleteDelta{Owner=owner,CompletedActId=1,DeltaList=[new Callback(()=>landed=true)]}.Apply(client);
Check(landed&&owner.ai is NoGoal,"late completion lands results without restarting automation");
NetSession.Instance.Connection=host;
foreach(var human in new[]{false,true}) {
 var builder=new Chara{IsPC=human};var build=new TaskBuild{owner=builder};host.Delta.Items.Clear();
 CharaProgressCompleteEvent.OnProgressComplete(build);CharaProgressCompleteEvent.Pack(new Callback(()=>{}));CharaProgressCompleteEvent.OnProgressCompleteEnd(build);
 Check(host.Delta.Items.Single() is CharaBuildDelta {Results.Count:1},"host "+(human?"player":"AI companion")+" publishes terrain build and captured results together");
}
var remoteBuild=new TaskBuild{owner=new Chara{IsRemotePlayer=true}};host.Delta.Items.Clear();
var collected=new List<ElinDelta>();
using(CharaProgressCompleteEvent.CollectBuildSideEffects(collected)) {
 CharaProgressCompleteEvent.OnProgressComplete(remoteBuild);CharaProgressCompleteEvent.Pack(new Callback(()=>{}));CharaProgressCompleteEvent.OnProgressCompleteEnd(remoteBuild);
}
Check(collected.Count==1&&host.Delta.Items.Count==0,"requested build collects results without publishing a duplicate build");
NetSession.Instance.Connection=client;
// Failure isolation: native replay and each result can fail independently.
owner.IsPC=true;controller=new AutoActHarvestMine{owner=owner};owner.ai=controller;task=Harvest();progress=Progress(task);
progress.Complete=()=>throw new InvalidOperationException("native replay failed");
int results=0;var failures=client.Desyncs;
new CharaProgressCompleteDelta{Owner=owner,CompletedActId=1,DeltaList=[new Callback(()=>{
 Check(CharaProgressCompleteDelta.IsReplaying,"replay guard stays active after native failure");results++;
})]}.Apply(client);
Check(results==1&&client.Desyncs==failures+1&&owner.ai is NoGoal&&controller.Cancels==1,"native failure lands results then stops Auto Act without retry");
Check(!CharaProgressCompleteDelta.IsReplaying,"native failure restores replay context");
owner.ai=controller=new AutoActHarvestMine{owner=owner};task=Harvest();Progress(task);
results=0;failures=client.Desyncs;
new CharaProgressCompleteDelta{Owner=owner,CompletedActId=1,DeltaList=[new Callback(()=>throw new Exception("bad result")),new Callback(()=>results++)]}.Apply(client);
Check(results==1&&client.Desyncs==failures+1&&owner.ai is NoGoal,"failed result cannot skip later results and stops current automation");
var later=new AutoActHarvestMine{owner=owner};owner.ai=later;results=0;
new CharaProgressCompleteDelta{Owner=owner,CompletedActId=1,DeltaList=[new Callback(()=>throw new Exception("late bad result")),new Callback(()=>results++)]}.Apply(client);
Check(results==1&&owner.ai==later&&later.Cancels==0,"late failed result drains bundle without canceling replacement automation");
results=0;
new CharaProgressCompleteDelta{Owner=owner,CompletedActId=999,DeltaList=[new Callback(()=>results++)]}.Apply(client);
Check(results==1&&owner.ai==later,"unknown task mapping still applies authoritative results");
new CharaProgressCompleteDelta{Owner=owner,CompletedActId=1,DeltaList=[new Callback(()=>{
 var outer=CharaProgressCompleteDelta.Current;
 new CharaProgressCompleteDelta{Owner=owner,CompletedActId=1,DeltaList=[]}.Apply(client);
 Check(CharaProgressCompleteDelta.Current==outer,"nested completion restores outer replay context");
})]}.Apply(client);
Check(!CharaProgressCompleteDelta.IsReplaying,"completion releases outer replay context");
regular=new TaskHarvest{owner=owner};owner.ai=regular;Progress(regular);
new CharaProgressCompleteDelta{Owner=owner,CompletedActId=1,DeltaList=[new Callback(()=>Check(regular.Ticks==0,"ordinary task also waits for results before continuation"))]}.Apply(client);
results=0;var absent=new RemoteCard(owner){Missing=true};
new CharaProgressCompleteDelta{Owner=absent,CompletedActId=1,DeltaList=[new Callback(()=>results++)]}.Apply(client);
Check(results==1&&!CharaProgressCompleteDelta.IsReplaying,"missing owner still drains authoritative results and restores scope");
Console.WriteLine($"{passed} checks passed. Fake Unity/network boundary; live two-peer acceptance remains required.");
class Callback(Action action):ElinDelta{protected override void OnApply(ElinNetBase net)=>action();}
