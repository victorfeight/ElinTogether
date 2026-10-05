using ElinTogether.Models;
using ElinTogether.Net;

int checks=0;
void Check(bool value,string why){if(!value)throw new Exception(why);Console.WriteLine("PASS "+why);checks++;}
var host=new ElinNetHost();var client=new Chara();var log=new Thing();client.things.Items[11]=log;host.ActiveRemoteCharas[1]=client;NetSession.Instance.Connection=host;
var hostActor=EClass.pc;var hostMode=EClass.scene.actionMode;var hostBuild=ActionMode.Build;var hostUndo=EClass._map.tasks.undo;var hostSummary=EClass.screen.tileSelector.summary;
EClass.player.Agent.pos.Set(new(6,6));EClass.screen.tileSelector.start=new(7,7);EClass.screen.tileSelector.temp.Set(new(5,5));EClass.screen.tileSelector.processing=true;EClass.screen.tileSelector.firstInMulti=true;
ConstructionCommand Request()=>new(){Id=Guid.NewGuid(),ZoneUid=10,RecipeId="wall",Ingredients=[11],Instant=true,End=new(){X=2,Z=2}};
bool Restored()=>EClass.pc==hostActor&&EClass.scene.actionMode==hostMode&&ActionMode.Build==hostBuild&&EClass._map.tasks.undo==hostUndo&&EClass.screen.tileSelector.summary==hostSummary&&EClass.screen.tileSelector.start?.x==7&&EClass.screen.tileSelector.temp.x==5&&EClass.screen.tileSelector.processing&&EClass.screen.tileSelector.firstInMulti&&EClass.player.Agent.pos.x==6&&ConstructionContext.Current==null&&!EClass.player.instaComplete;
var request=Request();ConstructionManagement.Execute(host,request);ConstructionManagement.Execute(host,request);
Check(AM_Build.Builds==1&&HitSummary.Payments==1,"duplicate construction request builds and pays only once");
Check(client.Money==90&&hostActor.Money==100&&log.Num==4,"native payment runs as requesting character");
Check(BuildMenu.Instance==null,"construction does not require opening the host build menu");
Check(Restored(),"construction restores host actor, mode, selector, Agent and undo history");
Check(host.Replies.OfType<ConstructionReply>().Count()==1,"one completion reply after native results");
BuildMenu.Instance=new();BuildMenu.Instance.info1.lastMats["wall"]=99;
ConstructionManagement.Execute(host,Request());
Check(BuildMenu.Instance.info1.lastMats["wall"]==99,"remote material choice does not overwrite host UI preference");
var queued=Request();queued.Instant=false;ConstructionManagement.Execute(host,queued);
Check(DesignationManagement.Batches.Count==1&&hostUndo.items.Count==0,"pending construction enters client undo history only");
int builds=AM_Build.Builds,payments=HitSummary.Payments;
log.id="wrong material";ConstructionManagement.Execute(host,Request());log.id="log";
Check(AM_Build.Builds==builds&&HitSummary.Payments==payments&&Restored(),"invalid material rejected before building/payment and restores context");
HitSummary.Allowed=false;ConstructionManagement.Execute(host,Request());HitSummary.Allowed=true;
Check(AM_Build.Builds==builds&&HitSummary.Payments==payments,"native affordability/factory validation precedes building");
var unknown=Request();unknown.RecipeId="unknown";ConstructionManagement.Execute(host,unknown);
Check(AM_Build.Builds==builds,"unknown recipe rejected");
var invalid=Request();invalid.Altitude=100;ConstructionManagement.Execute(host,invalid);
Check(AM_Build.Builds==builds&&Restored(),"invalid altitude rejected before mutation");
var queuedRoof=Request();queuedRoof.Roof=true;queuedRoof.Instant=false;ConstructionManagement.Execute(host,queuedRoof);
Check(AM_Build.Builds==builds&&Restored(),"queued roof construction rejected instead of later building a wall");
AM_Build.ThrowBuild=true;try{ConstructionManagement.Execute(host,Request());throw new Exception("missing native exception");}catch(InvalidOperationException){}finally{AM_Build.ThrowBuild=false;}
Check(Restored()&&HitSummary.Payments==payments,"native failure restores host context and does not run later payment");
var keys=Request();keys.Roof=true;keys.IgnoreStackHeight=false;
using(var scope=new ConstructionContext(client,keys)) {
    Check(EClass.scene.actionMode.IsRoofEditMode()&&!ConstructionContext.ReadKey(UnityEngine.KeyCode.LeftControl),"client roof/control intent independent of host keyboard");
    Check(EClass.scene.actionMode.id=="Build","native mode identity preserved");
}
Check(Restored(),"standalone scope restores state");

var rejection=new List<string>();var priorMine=ActionMode.Mine;
DesignationCommand Terrain(bool instant=true,bool roof=false)=>new(){Operation=DesignationOperation.Mine,Instant=instant,Roof=roof,Start=new(){X=1,Z=1},End=new(){X=1,Z=1}};
client.Money=100;
TerrainManagement.Execute(client,Terrain(roof:true),rejection.Add);
Check(TerrainBoundary.WorkCount==1&&TerrainBoundary.Actor==client&&TerrainBoundary.Roof,"terrain uses requesting actor and requested roof mode");
Check(client.Money==90&&hostActor.Money==100&&ActionMode.Mine==priorMine&&Restored(),"terrain charges client and restores native mode/context");
var harvest=Terrain();harvest.Operation=DesignationOperation.Harvest;
TerrainManagement.Execute(client,harvest,rejection.Add);
Check(TerrainBoundary.WorkCount==1&&rejection.Count==1,"instant harvest no-op rejected with queued-work guidance");
rejection.Clear();
var paid=HitSummary.Payments;
TerrainManagement.Execute(client,Terrain(false,true),rejection.Add);
Check(TerrainBoundary.WorkCount==1&&HitSummary.Payments==paid&&rejection.Count==1,"queued roof job rejected before a wall could be targeted later");
TerrainManagement.Execute(client,Terrain(false),rejection.Add);
Check(DesignationManagement.Batches.Last().Length==1&&!DesignationManagement.Batches.Last()[0].isDestroyed,"native pending terrain work enters requesting player's undo history");
HitSummary.Allowed=false;TerrainManagement.Execute(client,Terrain(),rejection.Add);HitSummary.Allowed=true;
Check(TerrainBoundary.WorkCount==2&&Restored(),"blocked terrain never executes and restores state");
TerrainBoundary.Throw=true;try{TerrainManagement.Execute(client,Terrain(),rejection.Add);throw new Exception("missing terrain failure");}catch(InvalidOperationException){}finally{TerrainBoundary.Throw=false;}
Check(Restored()&&ActionMode.Mine==priorMine,"terrain exception restores static mining mode and shared host fields");

var box=new Thing{uid=22,parent=EClass._zone,pos=new(2,2)};CardCache.Items[22]=box;
BuildingObjectCommand Move()=>new(){Id=Guid.NewGuid(),ZoneUid=10,Move=true,Items=[22],Before=[new(){X=2,Z=2}],End=new(){X=3,Z=3},Instant=true};
var move=Move();BuildingObjectManagement.Execute(host,move);BuildingObjectManagement.Execute(host,move);
Check(box.pos==new Point(3,3)&&host.Delta.Items.OfType<CardConstructionState>().Count()==1,"one authoritative move and final result for duplicate request");
BuildingObjectManagement.Execute(host,Move());
Check(box.pos==new Point(3,3)&&Restored(),"stale original position rejects delayed item move");
var put=Move();put.Move=false;put.Before=[new(){X=3,Z=3}];BuildingObjectManagement.Execute(host,put);BuildingObjectManagement.Execute(host,put);
Check(EClass._map.PutAways==1&&box.parentCard==client&&Restored(),"put-away runs once as requesting character");
var second=new Thing{uid=23,parent=EClass._zone,pos=new(2,2)};CardCache.Items[23]=second;
var invalidMove=Move();invalidMove.Items=[22,23];invalidMove.Before=[new(){X=2,Z=2},new(){X=2,Z=2}];BuildingObjectManagement.Execute(host,invalidMove);
Check(second.parent==EClass._zone&&Restored(),"multi-target move rejected without mutating adjacent item");
box.parent=EClass._zone;box.parentCard=null;box.pos.Set(new(2,2));
var oldTask=new TaskMoveInstalled{target=box};EClass._map.tasks.designations.moveInstalled.items.Add(oldTask);
TaskMoveInstalled.Allowed=false;BuildingObjectManagement.Execute(host,Move());TaskMoveInstalled.Allowed=true;
Check(oldTask.isDestroyed&&box.pos==new Point(2,2),"retarget cancels previous job as vanilla selection does, but invalid destination cannot move item");
var badPeer=Move();host.Accept=false;BuildingObjectManagement.Execute(host,badPeer);host.Accept=true;
Check(box.pos==new Point(2,2),"inactive peer cannot move building objects");

var putClient=new ElinNetClient();NetSession.Instance.Connection=putClient;
bool submitted=BuildingObjectManagement.SubmitPutAway(second);
Check(submitted&&putClient.Delta.Items.OfType<BuildingObjectCommand>().Single().Items.SequenceEqual(new[]{23})&&second.parent==EClass._zone,"move-menu put-away submits selected UID without local mutation");
NetSession.Instance.Connection=host;
// These tests call real result application against a deliberately inert map:
// no build, mining, currency or item-generation methods exist on the boundary.
var peer=new ElinNetClient();box.placeState=PlaceState.installed;box.dir=3;box.altitude=5;box.isRoofItem=true;box.isMasked=true;
var state=CardConstructionState.Capture(box);box.dir=0;box.altitude=0;box.isRoofItem=false;box.isMasked=false;
state.Apply(peer);state.Apply(peer);
Check(box.dir==3&&box.altitude==5&&box.isRoofItem&&box.isMasked&&box.PlacementCalls==0,"absolute item result updates final properties without duplicate placement");
box.parent=client;box.parentCard=client;box.altitude=1;state.Apply(peer);
Check(box.altitude==1,"old ground-item result cannot overwrite an item moved into inventory");
client.uid=100;var carried=CardConstructionState.Capture(box);box.altitude=0;carried.Apply(peer);
Check(box.altitude==1,"confirmed put-away metadata applies to matching inventory owner");
var source=EClass._map.cells[1,1];source._roofBlock=9;source._roofBlockMat=5;source._roofBlockDir=14;
var roof=ConstructionTerrainDelta.Capture(new(1,1),ConstructionTerrainKind.Roof);source._roofBlock=0;roof.Apply(peer);
Check(source._roofBlock==9&&source._roofBlockMat==5&&source._roofBlockDir==14,"terrain result preserves roof material, direction and height");
source.objDir=2;source.isModified=true;var rotation=ConstructionTerrainDelta.Capture(new(1,1),ConstructionTerrainKind.Rotation);source.objDir=0;source.isModified=false;rotation.Apply(peer);
Check(source.objDir==2&&source.isModified,"final terrain result includes direct rotation and modified-state writes");
var setters=EClass._map.Setters;roof.Apply(host);
Check(EClass._map.Setters==setters,"terrain result cannot mutate authoritative host");
new ConstructionTerrainDelta{ZoneUid=99,Pos=new(){X=1,Z=1},Kind=ConstructionTerrainKind.Roof}.Apply(peer);
Check(EClass._map.Setters==setters,"wrong-zone result cannot change new map");
source.effect=new(){ints=[1,2],strs=["water"]};var liquid=ConstructionTerrainDelta.Capture(new(1,1),ConstructionTerrainKind.Liquid);source.effect.ints[0]=99;liquid.Apply(peer);
Check(source.effect!.ints[0]==1&&source.effect.strs[0]=="water","liquid snapshot owns a copy of native effect data");
Console.WriteLine($"{checks} checks passed");
