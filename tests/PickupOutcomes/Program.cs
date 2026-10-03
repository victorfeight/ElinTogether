using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
var checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
T Cache<T>(T c) where T:Card {CardCache.Items[c.uid]=c;return c;}
(ElinNetClient,Chara,Thing) Setup(bool full=false){CardCache.Items.Clear();CharaProgressCompleteEvent.Chara=null;CharaProgressCompleteEvent.Results.Clear();EClass._zone=new();EClass._map=new();var net=new ElinNetClient();NetSession.Instance.Connection=net;var c=Cache(new Chara{uid=10,IsPC=true,IsPlayer=true,Full=full});EClass.pc=c;var t=Cache(new Thing{uid=20,parent=EClass._zone});return(net,c,t);}
CharaPickThingDelta Handoff(Chara c,Thing t,CharaPickThingDelta.PickType type=CharaPickThingDelta.PickType.Pick)=>new(){Owner=c,Thing=t,Type=type,Pos=new(){X=3,Z=4}};
var (client,owner,item)=Setup(true);
owner.Pick(item);
Check(item.parent==EClass._zone&&client.Delta.Items.Count==0,"full manual/AutoAct direct pickup stays ground and publishes no attempt");
owner.Full=false;var bag=Cache(new Thing{uid=30,parent=owner});owner.Destination=bag;owner.Pick(item);
Check(client.Delta.Items is [CardAddThingDelta d]&&d.Parent.Uid==bag.uid,"successful pickup publishes only chosen bag transfer");
var transfer=(CardAddThingDelta)client.Delta.Items.Single();item.parent=EClass._zone;var host=new ElinNetHost();host.ActiveRemoteCharas[7]=owner;NetSession.Instance.Connection=host;transfer.OriginPeer=7;transfer.Apply(host);
Check(item.parent==bag&&owner.Picks==2,"host reproduces destination without rerunning pickup");
transfer.Apply(host);Check(item.parent==bag,"repeated transfer is idempotent");
(client,owner,item)=Setup(true);var stack=Cache(new Thing{uid=40,parent=owner,Num=5});owner.Stack=stack;item.Num=2;owner.Pick(item);
Check(client.Delta.Items is [CardTryStackToDelta]&&!item.isDestroyed&&stack.Num==5,"full inventory with compatible stack submits one host merge");
var merge=(CardTryStackToDelta)client.Delta.Items.Single();host=new();host.ActiveRemoteCharas[7]=owner;NetSession.Instance.Connection=host;merge.OriginPeer=7;merge.Apply(host);merge.Apply(host);
Check(item.isDestroyed&&stack.Num==7,"host merges source once even if request repeats");
(client,owner,item)=Setup(true);owner.Pick(item);owner.Pick(item);owner.Pick(item);
Check(client.Delta.Items.Count==0&&item.parent==EClass._zone,"repeated AutoAct-style failed calls cannot manufacture inventory additions");
(client,owner,item)=Setup();item.parent=null;Handoff(owner,item,CharaPickThingDelta.PickType.TrySmoothPick).Apply(client);
Check(item.parent==owner&&client.Delta.Items is [CardAddThingDelta],"harvest handoff emits ordinary transfer despite outer replay context");
Check(!ElinDelta.IsApplying,"handoff restores outer apply depth");
(client,owner,item)=Setup(true);item.parent=null;Handoff(owner,item,CharaPickThingDelta.PickType.TrySmoothPick).Apply(client);
Check(item.parent==EClass._zone&&item.pos.x==3&&client.Delta.Items is [ZoneAddCardDelta],"full harvest leaves product at native drop position with one ground outcome");
(client,owner,item)=Setup();item.parent=null;EClass._map.Smooth=false;Handoff(owner,item,CharaPickThingDelta.PickType.TrySmoothPick).Apply(client);
Check(item.parent==EClass._zone&&client.Delta.Items is [ZoneAddCardDelta],"smooth pickup disabled retains vanilla ground placement");
(client,owner,item)=Setup(true);Handoff(owner,item).Apply(client);
Check(client.Delta.Items.Count==0&&item.parent==EClass._zone,"rejected existing-ground pickup emits no guessed outcome");
(client,owner,item)=Setup(true);item.parent=null;Handoff(owner,item).Apply(client);
Check(client.Delta.Items is [ZoneAddCardDelta],"parentless full product drops once without duplicate ground message");
(client,owner,item)=Setup(true);stack=Cache(new Thing{uid=40,parent=owner});owner.Stack=stack;Handoff(owner,item).Apply(client);
Check(client.Delta.Items is [CardTryStackToDelta]&&!item.isDestroyed,"harvest stack uses existing host merge rather than local destructive replay");
(client,owner,item)=Setup();owner.IsPC=false;item.parent=null;Handoff(owner,item).Apply(client);
Check(item.parent==EClass._zone&&owner.Picks==0&&client.Delta.Items.Count==0,"observer only stages new product and emits no pickup");
item.pos=new(8,9);Handoff(owner,item).Apply(client);
Check(item.pos.x==8,"observer does not relocate already grounded product");
item.parent=owner;Handoff(owner,item).Apply(client);
Check(item.parent==owner,"late handoff never removes a collected product");
(client,owner,item)=Setup();owner.ThrowPick=true;try{Handoff(owner,item).Apply(client);}catch(InvalidOperationException){}
Check(!ElinDelta.IsApplying,"native pickup exception restores simulation scope");
(client,owner,item)=Setup();host=new();NetSession.Instance.Connection=host;Handoff(owner,item).Apply(host);
Check(owner.Picks==0&&host.Delta.Items.Count==0,"host refuses product handoff as client pickup request");
CharaProgressCompleteEvent.Chara=owner;Thing result=null!;
var run=CharaPickThingEvent.OnCharaPickThingy(owner,item,ref result);
Check(!run&&result==item&&CharaProgressCompleteEvent.Results is [CharaPickThingDelta],"remote crafting still defers product instead of picking on host");
CharaProgressCompleteEvent.Chara=null;
(client,owner,item)=Setup();host=new();host.ActiveRemoteCharas[7]=owner;NetSession.Instance.Connection=host;var other=Cache(new Chara{uid=50,IsPlayer=true});item.parent=other;
new CardAddThingDelta{Thing=item,Parent=owner,TryStack=false,DestInvX=-1,DestInvY=-1,OriginPeer=7}.Apply(host);
Check(item.parent==other&&host.Delta.Items is [CardAddThingDelta correction]&&correction.Parent.Uid==other.uid,"competing pickup corrects client to actual host owner");
host.Delta.Items.Clear();stack=Cache(new Thing{uid=40,parent=owner});new CardTryStackToDelta{Card=item,To=stack,Parent=owner,OriginPeer=7}.Apply(host);
Check(!item.isDestroyed&&stack.Num==1&&host.Delta.Items is [CardAddThingDelta],"stack cannot consume another player's item");
host.Delta.Items.Clear();new ZoneAddCardDelta{Card=item,ZoneUid=1,Pos=new(),OriginPeer=7}.Apply(host);
Check(item.parent==other&&host.Delta.Items is [CardAddThingDelta],"late overflow ground outcome cannot drop another player's item");
item.parent=EClass._zone;stack.isDestroyed=true;host.Delta.Items.Clear();new CardTryStackToDelta{Card=item,To=stack,Parent=owner,OriginPeer=7}.Apply(host);
Check(item.parent==EClass._zone&&host.Delta.Items is [ZoneAddCardDelta],"missing stack target returns actual ground location without forced add");
stack.isDestroyed=false;stack.parent=other;host.Delta.Items.Clear();new CardTryStackToDelta{Card=item,To=stack,Parent=owner,OriginPeer=7}.Apply(host);
Check(item.parent==EClass._zone&&!item.isDestroyed,"moved stack target cannot trigger unchecked insertion");
item.parent=null;stack.isDestroyed=true;host.Delta.Items.Clear();new CardTryStackToDelta{Card=item,To=stack,Parent=owner,OriginPeer=7}.Apply(host);
Check(item.parent==EClass._zone&&host.Delta.Items is [ZoneAddCardDelta],"failed deferred-product stack preserves parentless loot on requesting player's tile");
// Host-staged products survive failure/disconnect and are eligible for normal
// ground-card collection validation before the collecting client responds.
(client,owner,item)=Setup();host=new();NetSession.Instance.Connection=host;CharaProgressCompleteEvent.Chara=owner;item.parent=null;
CharaPickThingEvent.DeferPickup(item,null,CharaPickThingDelta.PickType.Pick);
Check(item.parent==EClass._zone&&item.pos.x==owner.pos.x&&item.pos.z==owner.pos.z&&host.Delta.Items is [ZoneAddCardDelta]&&CharaProgressCompleteEvent.Results is [CharaPickThingDelta],"generated pickup is grounded on host before product handoff");
var placed=item.pos;CharaProgressCompleteEvent.Results.Clear();host.Delta.Items.Clear();CharaPickThingEvent.DeferPickup(item,new Point(8,9),CharaPickThingDelta.PickType.PickOrDrop);
Check(item.pos==placed&&host.Delta.Items.Count==0,"deferring existing loot does not move it again");
CharaProgressCompleteEvent.Chara=null;
// A paid shop drag may already belong to the actor on the host.
(client,owner,item)=Setup();host=new();host.ActiveRemoteCharas[7]=owner;NetSession.Instance.Connection=host;
item.parent=owner;item.invX=2;item.invY=0;
var place=new CardAddThingDelta{Thing=item,Parent=owner,TryStack=false,DestInvX=5,DestInvY=1,OriginPeer=7};
place.Apply(host);
Check(item.parent==owner&&item.invX==5&&item.invY==1&&owner.Removes==1,"same-container paid reservation moves into requested hotbar slot on host");
place.Apply(host);
Check(owner.Removes==1,"duplicate same-slot placement does not detach again");
new CardAddThingDelta{Thing=item,Parent=owner,TryStack=false,DestInvX=-1,DestInvY=-1,OriginPeer=7}.Apply(host);
Check(owner.Removes==1&&item.invX==5&&item.invY==1,"unspecified destination preserves existing host slot");

// Exact incident: host transfer lands first, then Ally Expansion's completion
// callback attempts to redirect that same item to the observing client's pc.
foreach(var full in new[]{false,true}) {
 (client,owner,item)=Setup(full);var remote=Cache(new Chara{uid=1,IsPlayer=true});item.id="gun";item.parent=remote;
 AllyPickup.Source=remote;AllyPickup.PickForPC=true;AllyPickup.Redirects=0;
 new CharaProgressCompleteDelta{Replay=()=>remote.Pick(item)}.Apply(client);
 Check(item.parent==remote&&owner.Picks==0&&AllyPickup.Redirects==0&&client.Delta.Items.Count==0,
  $"host handgun remains host-owned before ally redirection; client full={full}");
 // Guard the alternate entry points and even a callback that directly uses pc.
 new CharaProgressCompleteDelta{Replay=()=>{owner.Pick(item);owner.PickOrDrop(new(7,7),item);EClass._map.TrySmoothPick(new(7,7),item,owner);}}.Apply(client);
 Check(item.parent==remote&&owner.Picks==0&&client.Delta.Items.Count==0,"all replay pickup entry points preserve authoritative ownership");
 // A recipient's handoff runs inside the outer replay but explicitly simulates input.
 item.parent=EClass._zone;AllyPickup.Source=null;
 new CharaProgressCompleteDelta{Replay=()=>Handoff(owner,item).Apply(client)}.Apply(client);
 Check(full ? item.parent==EClass._zone&&client.Delta.Items.Count==0 : item.parent==owner&&client.Delta.Items is [CardAddThingDelta],
  $"explicit product handoff inside replay retains vanilla capacity outcome; full={full}");
 Check(!ElinDelta.IsApplying&&!CharaProgressCompleteDelta.IsReplaying,"nested handoff restores replay and apply scopes");
}
(client,owner,item)=Setup();var gatheringAlly=Cache(new Chara{uid=2});AllyPickup.Source=gatheringAlly;AllyPickup.PickForPC=true;
NetSession.Instance.Connection=null;gatheringAlly.Pick(item);
Check(item.parent==owner,"solo ally pick-for-PC remains enabled");
item.parent=EClass._zone;NetSession.Instance.Connection=new ElinNetHost();gatheringAlly.Pick(item);
Check(item.parent==owner,"host ally gathering keeps configured pickup recipient");
AllyPickup.Source=null;NetSession.Instance.Connection=client;item.parent=EClass._zone;owner.Pick(item);
Check(item.parent==owner,"ordinary client pickup remains enabled outside replay");
var threw=false;try{new CharaProgressCompleteDelta{Replay=()=>throw new InvalidOperationException("test")}.Apply(client);}catch(InvalidOperationException){threw=true;}
Check(threw&&!ElinDelta.IsApplying&&!CharaProgressCompleteDelta.IsReplaying,"failed replay does not leave pickup blocked");
foreach(var (type,method) in new[]{(typeof(CharaPickThingEvent),"OnCharaPickThingy"),(typeof(CharaPickOrDropEvent),"OnCharaPickOrDrop"),(typeof(CharaTrySmoothPickEvent),"OnTrySmoothPick")}) {
 var attr=type.GetMethod(method,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetCustomAttributes(typeof(HarmonyLib.HarmonyBefore),false).Cast<HarmonyLib.HarmonyBefore>().Single();
 Check(attr.Before.Contains("AutoActAllyExpansion"),$"{method} declares ordering before actual Ally Expansion Harmony owner");
}
// Exact robe race: client picks on B2F after host has moved to B1F.
(client,owner,item)=Setup();
var oldFloor=EClass._zone; oldFloor.uid=2043;
oldFloor.map.Ground.Add(item);oldFloor.map.CellEntries.Add(item);
var newFloor=new Zone{uid=8};EClass._zone=newFloor;
EClass.game.spatials.Others[2043]=oldFloor;
host=new();host.ActiveRemoteCharas[7]=owner;NetSession.Instance.Connection=host;
var late=new CardAddThingDelta{Thing=item,Parent=owner,TryStack=true,DestInvX=-1,DestInvY=-1,ZoneUid=2043,OriginPeer=7};
late.Apply(host);
Check(item.parent==oldFloor&&oldFloor.map.Ground.Contains(item)&&oldFloor.map.CellEntries.Contains(item),"old-floor pickup cannot detach inactive map ownership or lists");
Check(host.Delta.Items is [ZoneAddCardDelta {ZoneUid:2043}],"rejected pickup sends ground correction, never relays transfer success");
var floorCorrection=(ZoneAddCardDelta)host.Delta.Items.Single();
// Client predicted inventory ownership; correction arrives after activating B1F.
item.parent=owner;owner.things.Add(item);owner.held=item;NetSession.Instance.Connection=client;
floorCorrection.Apply(client);
Check(item.parent==null&&!owner.things.Contains(item)&&owner.held==null&&!CardCache.Contains(item)&&!item.isDestroyed,"off-floor correction removes predicted inventory/cursor/cache without destroying host item");
Check(newFloor.map.Ground.Count==0&&newFloor.map.CellEntries.Count==0,"correction cannot place old-floor item on the new floor");
floorCorrection.Apply(client);
Check(newFloor.map.Ground.Count==0,"duplicate correction cannot resurrect forgotten replica");
// Restored host snapshot supplies the original floor item; a later pickup is legal.
Cache(item);item.parent=oldFloor;EClass._zone=oldFloor;NetSession.Instance.Connection=host;host.Delta.Items.Clear();
late.Apply(host);
Check(item.parent==owner&&!oldFloor.map.Ground.Contains(item)&&!oldFloor.map.CellEntries.Contains(item),"returning to original floor allows native pickup and removes ground references");
// Even a forged/default zone stamp cannot transfer an inactive ground object.
item.parent=oldFloor;oldFloor.map.Ground.Add(item);oldFloor.map.CellEntries.Add(item);EClass._zone=newFloor;host.Delta.Items.Clear();
new CardAddThingDelta{Thing=item,Parent=owner,TryStack=false,DestInvX=-1,DestInvY=-1,OriginPeer=7}.Apply(host);
Check(item.parent==oldFloor&&host.Delta.Items is [ZoneAddCardDelta],"source root-zone validation catches unstamped inactive ground transfer");
stack=Cache(new Thing{uid=777,parent=owner,Num=5});host.Delta.Items.Clear();
new CardTryStackToDelta{Card=item,To=stack,Parent=owner,ZoneUid=2043,OriginPeer=7}.Apply(host);
Check(!item.isDestroyed&&stack.Num==5&&item.parent==oldFloor,"late stack merge cannot consume old-floor source");
// Destination container belongs to the old floor, although source is carried.
item.parent=owner;var oldChest=Cache(new Thing{uid=778,parent=oldFloor});host.Delta.Items.Clear();
new CardAddThingDelta{Thing=item,Parent=oldChest,TryStack=false,DestInvX=-1,DestInvY=-1,ZoneUid=8,OriginPeer=7}.Apply(host);
Check(item.parent==owner&&host.Delta.Items is [CardAddThingDelta],"inactive destination container rejects transfer and restores actual inventory owner");
host.Delta.Items.Clear();new ZoneAddCardDelta{Card=item,ZoneUid=2043,Pos=new(){X=3,Z=4},OriginPeer=7}.Apply(host);
Check(item.parent==owner&&host.Delta.Items is [CardAddThingDelta],"late drop cannot install carried item on an inactive floor");
host.Delta.Items.Clear();
new CardPlacedDelta{Owner=item,PlaceState=PlaceState.installed,Dir=2,ByPlayer=true,ZoneUid=2043,OriginPeer=7}.Apply(host);
Check(item.Placements==0&&host.Delta.Items is [CardAddThingDelta],"stale placement following rejected drop cannot mutate carried item");
item.parent=oldFloor;host.Delta.Items.Clear();
new CardTryStackToDelta{Card=stack,To=item,Parent=null,ZoneUid=8,OriginPeer=7}.Apply(host);
Check(!stack.isDestroyed&&item.Num==1,"stack destination on inactive floor is also rejected");
item.parent=owner;
// Source map correction arriving before activation uses native active-map placement.
EClass._zone=oldFloor;NetSession.Instance.Connection=client;host.Delta.Items.Clear();
new ZoneAddCardDelta{Card=item,ZoneUid=2043,Pos=new(){X=3,Z=4}}.Apply(client);
Check(item.parent==oldFloor&&oldFloor.map.CellEntries.Contains(item),"correction received before transition restores normal ground item");
Console.WriteLine($"{checks} checks passed (native boundary model; production pickup hooks and outcome handlers)");
