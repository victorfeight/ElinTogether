using ElinTogether.Net;
using ElinTogether.Net.Steam;
var checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
double now=0;var logs=new List<string>();
using var trace=new NetworkFlightRecorder(logs.Add,()=>now,watchdog:false);
trace.Context("role=client lobby=123 tick=8851");
trace.Expect("rx/1",true);trace.Mark("rx/1","message=100 bytes=22");
using(var outer=trace.Enter("dispatch","outer-save")){
 using(trace.Enter("dispatch","profile-receipt")){trace.Report("nested");Check(logs[^1].Contains("active=[profile-receipt]"),"nested handler is identified");}
 trace.Report("restored");Check(logs[^1].Contains("active=[outer-save]"),"outer handler restored after recursive save dispatch");
}
now=20;trace.Report("heartbeat");
Check(logs[^1].Contains("RECENT")&&logs[^1].Contains("message=100")&&logs[^1].Contains("rx/1: count=1 age=20.0s"),"missing receive is detected from monotonic age with last message preserved");
for(var i=0;i<100;i++)trace.Mark("tx/1","message="+(1000+i));
now=21;trace.Fault("send failed");
Check(!logs[^1].Contains("RECENT"),"history dumps are bounded under closely spaced faults");
var oldCount=logs.Count;trace.Fault("send failed again");Check(logs.Count==oldCount,"repeated failures do not flood disk");
now=30;trace.Report("manual",true);
Check(logs[^1].Contains("rx/1 0.000 message=100")&&logs[^1].Contains("message=1099")&&!logs[^1].Contains("message=1000"),"outgoing flood cannot overwrite the separate receive history and each history is bounded");
using(var blocked=trace.Enter("decode","message=101")){now=100;trace.Report("heartbeat");Check(logs[^1].Contains("active=[message=101] activeAge=70.0s"),"watchdog identifies a blocked decode even without new main-thread records");}
var peer=new Peer();var inbox=new SteamNetPacketInbox{Trace=trace};var seen=new List<string>();
inbox.Add("world-A",peer,200);inbox.Add("profile",peer,201);inbox.Add("world-B",peer,202);
inbox.Dispatch((p,_)=>seen.Add((string)p),p=>(string)p=="profile");
Check(seen.SequenceEqual(new[]{"profile"})&&inbox.Count==2,"profile-only wait preserves queued gameplay");
inbox.Dispatch((p,_)=>seen.Add((string)p));
Check(seen.SequenceEqual(new[]{"profile","world-A","world-B"}),"instrumentation preserves deferred gameplay order");
now=110;trace.Report("manual",true);Check(logs[^1].Contains("message=202 type=String"),"native message ID survives defer and dispatch");
inbox.Add("bad",peer,203);
try{inbox.Dispatch((_,_)=>throw new InvalidOperationException("fixture"));}catch(InvalidOperationException){}
trace.Report("after-failure");Check(logs[^1].Contains("dispatch: count=6")&&logs[^1].Contains("active=[idle]"),"throwing handler unwinds diagnostic scope");
using(var throwing=new NetworkFlightRecorder(_=>throw new Exception("sink"),()=>now,watchdog:false)){throwing.Report("safe");Check(true,"log sink failure cannot escape into game processing");}
trace.Dispose();oldCount=logs.Count;trace.Report("disposed");Check(logs.Count==oldCount,"disposed recorder emits nothing");
Console.WriteLine($"{checks} checks passed");
namespace ElinTogether.Net.Steam{public interface ISteamNetPeer{bool IsConnected{get;}}public class Peer:ISteamNetPeer{public bool IsConnected=>true;}}
