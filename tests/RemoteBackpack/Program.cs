using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
int checks = 0;
void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
// Exercise registration ownership as well as helpers: the old helper-only test
// passed while Release builds never installed this correction in solo.
RemoteBackpackCapacityPatch.Apply();
var registrations = HarmonyLib.Harmony.Registered.ToArray();
Check(registrations.Length == 3 && registrations.All(r => r.Owner == "test.mp.player-backpack"), "all capacity hooks have persistent ownership");
RemoteBackpackCapacityPatch.Apply();
Check(HarmonyLib.Harmony.Registered.Count == 3, "repeat initialization cannot double patch");
new HarmonyLib.Harmony("test.mp").UnpatchSelf();
Check(HarmonyLib.Harmony.Registered.Count == 3, "MP teardown retains backpack hooks");
Check(!Attribute.IsDefined(typeof(RemoteBackpackCapacityPatch), typeof(HarmonyLib.HarmonyPatch)), "session PatchAll cannot register these hooks again");
var freyfor = new Chara { SavedPlayer = true };
var savedBag = new ThingContainer { owner = freyfor, GridSize = 35 };
savedBag.AddRange(Enumerable.Range(0, 29).Select(_ => new Thing()));
savedBag.AddRange(Enumerable.Range(0, 13).Select(_ => new Thing { isEquipped = true }));
savedBag.AddRange(Enumerable.Range(0, 25).Select(_ => new Thing { invY = 1 }));
bool savedFull = true;
Check(savedBag.Count == 67 && !RemoteBackpackCapacityPatch.IsFull(savedBag, 0, ref savedFull) && !savedFull, "actual solo save: 67 entries use 29 of 35 slots");
savedBag.AddRange(Enumerable.Range(0, 6).Select(_ => new Thing()));
RemoteBackpackCapacityPatch.IsFull(savedBag, 0, ref savedFull);
Check(savedFull, "solo gift capacity still rejects genuinely full 35-slot backpack");
var host = new ElinNetHost(); NetSession.Instance.Connection = host;
var remote = new Chara { IsPlayer = true };
var bag = new ThingContainer { owner = remote, GridSize = 35 };
for (int i = 0; i < 23; i++) bag.Add(new Thing());
for (int i = 0; i < 21; i++) bag.Add(new Thing { isEquipped = i < 14, invY = i >= 14 ? 1 : 0 });
bool full = true;
Check(bag.Count == 44 && PlayerBackpackCapacity.Used(bag) == 23, "recorded 44-entry inventory has 23 backpack items");
Check(!RemoteBackpackCapacityPatch.IsFull(bag, 0, ref full) && !full, "remote no-grid inventory admits pickup and shop delivery");
var originalPC = EClass.pc; EClass.pc = remote;
Check(!RemoteBackpackCapacityPatch.IsFull(bag, 0, ref full) && !full, "temporary replay PC context cannot bypass remote capacity correction");
EClass.pc = originalPC;
var slots = bag.Select(t => { bool visible = true; RemoteBackpackCapacityPatch.ShouldShowOnGrid(bag, t, ref visible); return visible; }).Count(v => v);
Check(slots == 23, "host inspection uses same equipment and hotbar exclusions as client");
// Production IsRemotePlayer becomes false while the host runs companion AI.
remote.IsPlayer = false; remote.SavedPlayer = true;
Check(!RemoteBackpackCapacityPatch.IsFull(bag, 0, ref full) && !full, "Take a Break retains free slots despite NPC-sized total item count");
Check(!RemoteBackpackCapacityPatch.IsAlmostFull(bag, 2, ref full) && !full, "gift follow-up cannot prematurely sell inventory by counting equipment and hotbar");
slots = bag.Count(t => { bool visible = true; RemoteBackpackCapacityPatch.ShouldShowOnGrid(bag, t, ref visible); return visible; });
Check(slots == 23, "Take a Break inspection still excludes equipped and hotbar items");
NetSession.Instance.Connection = null;
Check(!RemoteBackpackCapacityPatch.IsFull(bag, 0, ref full) && !full, "offline saved-character ally retains player backpack capacity");
EClass.pc = remote;
Check(RemoteBackpackCapacityPatch.IsFull(bag, 0, ref full), "switching to saved character restores vanilla local-player grid handling");
EClass.pc = originalPC; NetSession.Instance.Connection = host;
remote.IsPlayer = true;
Check(!RemoteBackpackCapacityPatch.IsFull(bag, 0, ref full) && !full, "regaining human control preserves capacity");
for (int i = 0; i < 12; i++) bag.Add(new Thing());
RemoteBackpackCapacityPatch.IsFull(bag, 0, ref full);
Check(full, "exactly full backpack is still full");
bag.Add(new Thing()); RemoteBackpackCapacityPatch.IsFull(bag, 0, ref full);
Check(full, "overflowing backpack is still full");
RemoteBackpackCapacityPatch.IsFull(bag, 1, ref full);
Check(!full, "native non-main inventory row exemption preserved");
bag.owner = EClass.pc;
Check(RemoteBackpackCapacityPatch.IsFull(bag, 0, ref full), "local player retains native UI grid behavior");
bag.owner = new Chara { IsPlayer = false };
Check(RemoteBackpackCapacityPatch.IsFull(bag, 0, ref full), "ordinary NPC capacity remains native");
bag.owner = new Card();
Check(RemoteBackpackCapacityPatch.IsFull(bag, 0, ref full), "bags and special containers retain native rules");
bag.owner = remote; NetSession.Instance.Connection = null;
remote.IsPlayer = false; remote.SavedPlayer = false;
Check(RemoteBackpackCapacityPatch.IsFull(bag, 0, ref full), "ordinary solo NPC inventories remain native");
NetSession.Instance.Connection = host;
var map = EClass.game.activeZone.map;
map.bounds.SetBounds(4, 5, 12, 13);
MapBoundsSyncPatch.After(map.bounds);
var packet = (MapBoundsDelta)host.Delta.Sent.Single();
Check(packet.ZoneUid == 7 && packet.X == 4 && packet.MaxZ == 13, "host expansion publishes actual resulting rectangle");
map.bounds.SetBounds(6, 7, 10, 11);
packet.Apply(host);
Check(map.bounds.x == 6, "host ignores client-authored boundary packets");
var client = new ElinNetClient(); NetSession.Instance.Connection = client;
packet.Apply(client);
Check(map.bounds.x == 4 && map.bounds.maxZ == 13 && map.Refreshes == 1 && WidgetMinimap.Updates == 1, "client updates bounds, tiles, and minimap live");
packet.Apply(client);
Check(map.bounds.x == 4, "absolute boundary packet does not expand twice");
MapBoundsSyncPatch.After(map.bounds);
Check(client.Delta.Sent.Count == 0, "client does not publish expansion authority");
EClass.game.activeZone.uid = 8; packet.Apply(client);
Check(map.Refreshes == 2, "stale-zone packet is ignored"); EClass.game.activeZone.uid = 7;
new MapBoundsDelta { ZoneUid = 7, MapSize = 20, X = -1, Z = 1, MaxX = 10, MaxZ = 10 }.Apply(client);
Check(map.Refreshes == 2, "invalid bounds ignored");
new MapBoundsDelta { ZoneUid = 7, MapSize = 30, X = 1, Z = 1, MaxX = 10, MaxZ = 10 }.Apply(client);
Check(map.Refreshes == 2, "different map-size packet ignored");
NetSession.Instance.Connection = host; EClass.game.isLoading = true;
MapBoundsSyncPatch.After(map.bounds);
Check(host.Delta.Sent.Count == 1, "generation during loading emits no live expansion");
EClass.game.isLoading = false; MapBoundsSyncPatch.After(new MapBounds());
Check(host.Delta.Sent.Count == 1, "unrelated bounds emit nothing");
Console.WriteLine($"{checks} remote backpack and map bounds checks passed");

public class Card { }
public class Chara : Card { public bool IsPlayer, SavedPlayer; public bool GetBool(string key) => key == "remote_chara" && SavedPlayer; }
public class Thing { public bool isEquipped; public int invY; }
public class ThingContainer : List<Thing> { public Card owner = new(); public int GridSize; public bool IsFull(int y = 0) => throw new NotImplementedException(); public bool IsAlmostFull(int threshold = 2) => throw new NotImplementedException(); public bool ShouldShowOnGrid(Thing t) => throw new NotImplementedException(); }
public class EClass { public static Chara pc = new() { IsPlayer = true }; public static Game game = new(); }
public class Game { public bool isLoading; public Zone activeZone = new(); }
public class Zone { public int uid = 7; public Map map = new(); }
public class Map { public int Size = 20, Refreshes; public MapBounds bounds = new(); public void RefreshAllTiles() => Refreshes++; }
public class MapBounds { public int x,z,maxX,maxZ; public void Expand(int a) {} public void SetBounds(int a,int b,int c,int d) {x=a;z=b;maxX=c;maxZ=d;} }
public static class WidgetMinimap { public static int Updates; public static void UpdateMap() => Updates++; }
public static class EmpLog { public static void Information(string s, params object[] args) {} }
namespace ElinTogether.Models { public abstract class ElinDelta : EClass { protected abstract void OnApply(ElinNetBase net); public void Apply(ElinNetBase net) => OnApply(net); } }
namespace ElinTogether.Net {
 public class ElinNetBase { public Queue Delta = new(); }
 public class ElinNetHost : ElinNetBase {}
 public class ElinNetClient : ElinNetBase {}
 public class Queue { public List<ElinDelta> Sent = new(); public void AddRemote(ElinDelta delta) => Sent.Add(delta); }
 public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; public Chara Player = EClass.pc; }
}
namespace ElinTogether.Helper { internal class Placeholder {} }
namespace MessagePack { public class MessagePackObjectAttribute : Attribute {} public class KeyAttribute(int n) : Attribute {} }
namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple=true)] public class HarmonyPatch : Attribute { public HarmonyPatch() {} public HarmonyPatch(Type t,string method,params Type[] args) {} } public class HarmonyPrefix : Attribute {} public class HarmonyPostfix : Attribute {} }

namespace ElinTogether { internal static class ModInfo { internal const string Guid = "test.mp"; } }
namespace HarmonyLib {
 public class Harmony(string id) {
  public string Id=id;
  public static List<(string Owner, System.Reflection.MethodInfo Method)> Registered=[];
  public static bool HasAnyPatches(string id)=>Registered.Any(r=>r.Owner==id);
  public void Patch(System.Reflection.MethodInfo method,HarmonyMethod prefix)=>Registered.Add((Id,method));
  public void UnpatchSelf()=>Registered.RemoveAll(r=>r.Owner==Id);
 }
 public class HarmonyMethod(Type type,string name) {}
 public static class AccessTools {
  public static System.Reflection.MethodInfo Method(Type type,string name,Type[]? args=null)=>
   args is null ? type.GetMethod(name)! : type.GetMethod(name,args)!;
 }
}
