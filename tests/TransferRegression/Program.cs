using ElinTogether.Net;
using ElinTogether.Patches;
using UnityEngine;
int checks=0;
void Check(bool value,string name) {if(!value) throw new Exception(name);checks++;}
var abandoned=new ElinNetClient();
var first=abandoned.Begin();
Check(first.MoveNext() && abandoned.Scheduler.Starts==0,"wait while game is not ready");
((IDisposable)first).Dispose(); // equivalent iterator cancellation when its owner disconnects
abandoned.StopWorldStateUpdate();
var joined=new ElinNetClient();
var second=joined.Begin();
Check(second.MoveNext() && joined.Scheduler.Starts==0,"new connection owns independent wait");
joined.core.IsGameStarted=true;
Check(!second.MoveNext() && joined.Scheduler.Starts==1 && !joined.Paused,"rejoin starts sender after readiness");
abandoned.core.IsGameStarted=true;
Check(abandoned.Scheduler.Starts==0,"advancing the rejoin wait does not invoke abandoned connection");
Check(!second.MoveNext() && joined.Scheduler.Starts==1,"completed iterator cannot subscribe twice");
var ready=new ElinNetClient();ready.core.IsGameStarted=true;
Check(!ready.Begin().MoveNext() && ready.Scheduler.Starts==1,"already-ready start");
foreach(var mp in new[]{false,true}) foreach(var left in new[]{false,true})
 foreach(var shift in new[]{false,true}) foreach(var rightShift in new[]{false,true}) {
 NetSession.Instance.Connection=mp ? new object() : null;
 Input.Left=left;Input.Shift=shift;Input.RightShift=rightShift;
 bool latch=true;
 InvQuickTransferSortGuard.Before(ref latch);
 Check(latch==!(mp && left && (shift||rightShift)),"sort guard input matrix");
 }
// Reproduce the logged sequence: stale latch overrides the vanilla Shift check,
// sorting moves neighbor B into slot A before the queued callback reads that slot.
NetSession.Instance.Connection=new object();Input.Left=true;Input.Shift=true;Input.RightShift=false;
bool stale=true;InvQuickTransferSortGuard.Before(ref stale);
bool vanillaAutoSort=stale || (!Input.GetMouseButton(1) && !Input.Shift && !Input.RightShift);
Check(!vanillaAutoSort,"held gesture cannot use right-click sort exception");
Input.Left=false;
Check(!(stale || (!Input.GetMouseButton(1) && !Input.Shift && !Input.RightShift)),"mouse release alone preserves Shift sort hold");
Input.Shift=false;
Check(stale || (!Input.GetMouseButton(1) && !Input.Shift && !Input.RightShift),"release permits vanilla delayed sorting");
// Existing client sort merge scope is preserved by the new independent guard.
NetSession.Instance.Connection=new ElinNetClient();
InvSortEvent.Begin(out var outer); var queued=InvSortEvent.QueuedSources;
InvSortEvent.Begin(out var nested);
Check(queued!=null && ReferenceEquals(queued,InvSortEvent.QueuedSources),"nested client sort shares queue");
InvSortEvent.End(nested);InvSortEvent.End(outer);
Check(InvSortEvent.QueuedSources==null,"sort scope restored");
Console.WriteLine($"Passed {checks} transfer/startup regression checks");
