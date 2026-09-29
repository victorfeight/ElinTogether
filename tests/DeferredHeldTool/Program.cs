using ElinTogether.Elements;
using ElinTogether.Models;
using ElinTogether.Net;
void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
var net=new ElinNetBase{IsHost=true};
var owner=new Chara{uid=719};var root=new GoalRemote();root.SetOwner(owner);owner.ai=root;
var pick=new Thing{uid=1,parent=owner};var axe=new Thing{uid=2,parent=owner};var sickle=new Thing{uid=3,parent=owner};owner.held=pick;
CharaSwitchHeldDelta Switch(Card? item)=>new(){Owner=owner,HeldMainHand=item is null?null:(RemoteCard)item,HeldOffHand=item is null?null:(RemoteCard)item};
root.child=new AIAct();Switch(axe).Apply(net);
Check(owner.held==pick && root.PendingHeld!=null,"running task retains original tool and queues switch");
var next=new BaseTaskHarvest();root.InsertAction(next);
Check(owner.held==axe && next.Captured==axe && root.PendingHeld==null,"next harvest snapshots deferred axe instead of stale pick");
Switch(pick).Apply(net);Switch(sickle).Apply(net);root.HaltChildAct();
Check(owner.held==sickle,"latest switch wins while task runs");
root.child=new AIAct();Switch(null).Apply(net);root.HaltChildAct();
Check(owner.held==null,"empty-hand switch also survives running task");
root.child=new AIAct();Switch(pick).Apply(net);root.child.status=AIAct.Status.Success;Switch(axe).Apply(net);root.HaltChildAct();
Check(owner.held==axe && root.PendingHeld==null,"new idle switch supersedes queued switch");
root.child=new AIAct();Switch(sickle).Apply(net);var foreign=new Chara();sickle.parent=foreign;root.HaltChildAct();
Check(owner.held==axe,"deferred switch cannot take an item that changed owner");
owner.IsPC=true;Switch(pick).Apply(net);
Check(owner.held==axe,"remote replication does not overwrite local player tool");
owner.IsPC=false;root.child=new AIAct();Switch(pick).Apply(net);var before=net.Delta.Items.Count;root.HaltChildAct();
Check(net.Delta.Items.Count==before,"applying pending switch does not rebroadcast packet");
root.child=new AIAct();Switch(axe).Apply(net);root.child.status=AIAct.Status.Success;
var run=root.Run().GetEnumerator();run.MoveNext();run.MoveNext();
Check(owner.held==axe && root.PendingHeld==null,"normal goal cleanup applies queued switch without a new task");

CharaTaskDelta Task(Card? tool,TaskArgsBase? args=null)=>new(){Owner=owner,TaskArgs=args??new HarvestArgs(),Tool=tool is null?null:(RemoteCard)tool};
root.HaltChildAct();owner.held=axe;Task(pick).Apply(net);
Check(owner.held==pick && ((BaseTaskHarvest)root.child!).Captured==pick,"task arriving before visual tool update captures requested pick");
Switch(axe).Apply(net);Task(pick).Apply(net);
Check(owner.held==pick && ((BaseTaskHarvest)root.child!).Captured==pick,"task tool supersedes deferred stale visual switch before snapshot");
Task(null).Apply(net);
Check(owner.held==null && ((BaseTaskHarvest)root.child!).Captured==null,"barehand harvest explicitly clears stale tool");
var oldChild=root.child;var cancelled=TaskCache.Cancelled;Task(sickle).Apply(net);
Check(root.child==oldChild && TaskCache.Cancelled==cancelled+1 && sickle.parent==foreign,"foreign tool is rejected without stealing or replacing task");
pick.isDestroyed=true;Task(pick).Apply(net);
Check(root.child==oldChild && TaskCache.Cancelled==cancelled+2,"destroyed selected tool rejects task");pick.isDestroyed=false;
owner.held=axe;Task(pick,new IdleArgs()).Apply(net);
Check(owner.held==axe,"non-harvest tasks leave tool alone");
owner.IsPC=true;oldChild=root.child;Task(pick).Apply(new ElinNetBase());
Check(root.child==oldChild && owner.held==axe,"relayed task does not reassign local player's tool");owner.IsPC=false;
TaskCache.Taken=true;cancelled=TaskCache.Cancelled;Task(pick).Apply(net);
Check(owner.held==axe && TaskCache.Cancelled==cancelled+1,"occupied tile rejection precedes tool mutation");TaskCache.Taken=false;
Task(pick).Apply(new ElinNetBase());
Check(owner.held==pick && ((BaseTaskHarvest)root.child!).Captured==pick,"observer reproduces task with supplied tool");

// Vanilla mutation methods throw at the boundary: these production replay paths
// must never reach the overflow/stack/renderer side effects, at any capacity.
root.HaltChildAct();owner.held=pick;var pickCount=pick.Num;Switch(axe).Apply(net);
Check(owner.held==axe && pick.parent==owner && axe.parent==owner && pick.Num==pickCount,
    "idle switch preserves old and new tool ownership and stack count");
Switch(null).Apply(net);
Check(owner.held==null && axe.parent==owner,"empty-hand replay clears selection without dropping old tool");
owner.held=pick;axe.isDestroyed=true;Switch(axe).Apply(net);
Check(owner.held==pick,"destroyed visual target cannot replace selected tool");axe.isDestroyed=false;
axe.Num=0;Switch(axe).Apply(net);
Check(owner.held==pick,"zero-count visual target cannot replace selected tool");axe.Num=1;
Check(!ElinTogether.Helper.RemoteHeldItem.TrySelect(owner,sickle) && owner.held==pick,
    "shared helper rejects foreign items without inventory mutation");
Check(!ElinTogether.Helper.RemoteHeldItem.TrySelect(owner,new Chara{parent=owner}) && owner.held==pick,
    "shared helper does not turn character carrying into tool selection");
var bag=new Thing{parent=owner};var nested=new Thing{parent=bag};
Check(ElinTogether.Helper.RemoteHeldItem.TrySelect(owner,nested) && nested.parent==bag,
    "owned nested tool selection does not move it out of its container");
var torch=new Thing{parent=owner,LightRadius=4};var lightBefore=owner.LightRefreshes;
ElinTogether.Helper.RemoteHeldItem.TrySelect(owner,torch);
Check(owner.LightRefreshes==lightBefore+1,"selecting held light refreshes FOV without recreating renderer");
ElinTogether.Helper.RemoteHeldItem.TrySelect(owner,torch);
Check(owner.LightRefreshes==lightBefore+1,"repeated same-item selection has no side effects");
ElinTogether.Helper.RemoteHeldItem.TrySelect(owner,null);
Check(owner.LightRefreshes==lightBefore+2 && torch.parent==owner,"clearing light refreshes FOV without dropping torch");
owner.IsPC=true;owner.held=pick;
Check(!ElinTogether.Helper.RemoteHeldItem.TrySelect(owner,axe) && owner.held==pick,
    "shared helper cannot replace local player pickup and overflow mechanics");
