using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
int checks=0;
void Check(bool condition,string why) { if(!condition) throw new Exception(why); checks++; }
var host=new ElinNetBase{IsHost=true}; var client=new ElinNetBase();
var book=new Thing{uid=10,c_charges=2}; CardCache.Item=book;
NetSession.Instance.Connection=host;
// Host completion consumes one charge; the original implementation queued this
// ahead of completion, causing client 2 -> host result 1 -> replay 0.
CharaProgressCompleteEvent.Capturing=true;
CardChargeEvent.Before(book,out var before); book.c_charges=1;
CardChargeEvent.OnChargeChanged(book,before);
Check(host.Delta.Count==0,"charge must not precede completion");
Check(CharaProgressCompleteEvent.Results.Count==1,"completion captures authoritative charge result");
var result=CharaProgressCompleteEvent.Results.Single();
NetSession.Instance.Connection=client;
book.c_charges=2; book.c_charges--; result.Apply(client);
Check(book.c_charges==1,"normal reading leaves one charge on both peers");
book.c_charges=0; result.Apply(client);
Check(book.c_charges==1,"authoritative result repairs a locally undercounted book");
result.Apply(client); Check(book.c_charges==1,"duplicate absolute result does not consume another charge");
book.c_charges=2; result.Apply(client);
Check(book.c_charges==1,"missing or cancelled replay still receives host count");
book.c_charges=8; result.Apply(host);
Check(book.c_charges==8,"client cannot author host charge counts");
NetSession.Instance.Connection=host; CharaProgressCompleteEvent.Results.Clear();
book.c_charges=1; CardChargeEvent.Before(book,out before); book.c_charges=0;
CardChargeEvent.OnChargeChanged(book,before);
result=CharaProgressCompleteEvent.Results.Single();
book.c_charges=1; book.c_charges--; result.Apply(client);
// Native final consumption is a separate following CardModNum result.
book.isDestroyed=true; result.Apply(client);
Check(book.isDestroyed&&book.c_charges==0,"zero-charge result never resurrects consumed book");
book.isDestroyed=false; CharaProgressCompleteEvent.Capturing=false;
host.Delta.Clear(); CardChargeEvent.Before(book,out before); book.c_charges=3;
CardChargeEvent.OnChargeChanged(book,before);
Check(host.Delta.Count==1,"recharges and non-completion actions still publish immediately");
CardChargeEvent.OnChargeChanged(book,3); Check(host.Delta.Count==1,"unchanged values emit nothing");
NetSession.Instance.Connection=client; book.c_charges=2; CardChargeEvent.OnChargeChanged(book,3);
Check(client.Delta.Count==0,"client prediction does not echo charges to host");
NetSession.Instance.Connection=null; CardChargeEvent.OnChargeChanged(book,3);
Check(host.Delta.Count==1,"solo updates emit no network traffic");
Console.WriteLine($"{checks} charge replay checks passed");
public class Card { public int uid,c_charges; public bool isDestroyed; }
public class Thing:Card {}
public static class LayerInventory { public static void SetDirty(Thing t){} }
public static class EmpLog { public static void Debug(string s,params object[] args){} }
namespace ElinTogether.Net {
 public class ElinNetBase { public bool IsHost; public Queue Delta=new(); }
 public class Queue:List<ElinTogether.Models.ElinDelta> { public void AddRemote(ElinTogether.Models.ElinDelta d)=>Add(d); }
 public class NetSession { public static NetSession Instance=new(); public ElinNetBase? Connection; }
}
namespace ElinTogether.Models {
 public abstract class ElinDelta { protected abstract void OnApply(ElinNetBase net); public void Apply(ElinNetBase net)=>OnApply(net); }
 public class RemoteCard { public Card Item=null!; public Card Find()=>Item; public static implicit operator RemoteCard(Card c)=>new(){Item=c}; }
 public static class CardCache { public static Card? Item; public static bool Contains(Card c)=>c==Item; }
}
namespace ElinTogether.Patches {
 internal static class CharaProgressCompleteEvent { internal static bool Capturing; internal static List<ElinDelta> Results=[]; internal static bool ShouldPack(bool remoteOnly)=>Capturing; internal static void Pack(ElinDelta delta)=>Results.Add(delta); }
}
namespace HarmonyLib {
 public enum MethodType { Setter }
 public class HarmonyPatch:Attribute { public HarmonyPatch(Type t,string method,MethodType kind){} }
 public class HarmonyPrefix:Attribute {} public class HarmonyPostfix:Attribute {}
}
namespace MessagePack { public class MessagePackObjectAttribute:Attribute {} public class KeyAttribute:Attribute { public KeyAttribute(int key){} } }
