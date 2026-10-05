using ElinTogether.Models;
using ElinTogether.Net;
using Mono.Cecil;
using Mono.Cecil.Cil;

int checks=0;
void Check(bool ok,string why){if(!ok)throw new Exception(why);Console.WriteLine("PASS "+why);checks++;}
using var native=ModuleDefinition.ReadModule(args.Single());
foreach(var name in new[]{"BaseTileMap","TileMapElona"}) {
    var il=native.Types.Single(t=>t.Name==name).Methods.Single(m=>m.Name=="DrawTile").Body.Instructions;
    var calls=il.Where(i=>i.Operand is MethodReference {Name:"Draw",DeclaringType.Name:"TaskDesignation"}).ToArray();
    Check(calls.Length==1,name+" has one native designation draw boundary");
    Check(il.Count(i=>i.Operand==calls[0].Next)>=2,name+" missing-detail and missing-task branches join after draw");
}
var areaType=native.Types.Single(t=>t.Name=="BaseArea");
var callbacks=areaType.NestedTypes.Append(areaType).SelectMany(t=>t.Methods).Where(m=>
    (m.DeclaringType!=areaType || m.CustomAttributes.Any(a=>a.AttributeType.Name=="CompilerGeneratedAttribute")) && m.HasBody && m.Body.Instructions.Any(i=>
    i.OpCode==OpCodes.Stfld && i.Operand is FieldReference {Name:"name",DeclaringType.Name:"AreaData"} ||
    i.Operand is MethodReference {DeclaringType.Name:"AreaData"} call && call.Name.StartsWith("set_"))).ToArray();
Check(callbacks.Length==6,"six confirmed native area property callbacks");
Check(new[]{"AM_EditArea","InspectGroupArea"}.SelectMany(name=>native.Types.Single(t=>t.Name==name).NestedTypes)
    .SelectMany(t=>t.Methods).Count(m=>m.HasBody&&m.Body.Instructions.Any(i=>i.Operand is MethodReference {Name:"RemoveArea",DeclaringType.Name:"RoomManager"}))==2,
    "both native area delete UI paths covered without patching general cleanup");
var destroy=native.Types.Single(t=>t.Name=="TaskBuild").Methods.Single(m=>m.Name=="OnDestroy");
Check(destroy.Body.Instructions.Any(i=>i.Operand is MethodReference {Name:"AddCard"}),"native build cancellation returns resources on host");
Check(native.Types.Single(t=>t.Name=="Area").Methods.Single(m=>m.Name=="OnRemove").Body.Instructions.Any(i=>i.Operand is MethodReference {Name:"Destroy"}),"native area deletion destroys tasks; snapshot must not call it");

var host=new ElinNetHost();var client=new ElinNetClient();host.ActiveRemoteCharas[1]=new();host.ActiveRemoteCharas[2]=new();
NetSession.Instance.Connection=host;
var area=Area.Create("public");area.AddPoint(new(1,1));area.AddPoint(new(1,2));area.data.name="old";
EClass._map.rooms.listArea.Add(area);EClass._map.rooms.mapIDs[area.uid]=area;
var initial=BuildingPlanning.Capture();
Check(ReferenceEquals(initial,BuildingPlanning.Capture()),"unchanged plan reuses cached baseline");
BuildingPlanning.Dirty();Check(ReferenceEquals(initial,BuildingPlanning.Capture()),"no-op invalidation does not broadcast a new revision");
AreaCommand Edit(string name){var before=AreaSettings.Capture(area);var after=AreaSettings.Capture(area);after.Name=name;return new(){Id=Guid.NewGuid(),ZoneUid=10,AreaUid=area.uid,Operation=AreaOperation.Settings,Before=before,After=after};}
var rename=Edit("client name");AreaManagement.Execute(host,rename);AreaManagement.Execute(host,rename);
Check(area.data.name=="client name","client settings apply once on host");
Check(EClass._map.rooms.Refreshes==0,"rename does not rebuild rooms and overwrite unplated room names");
var stale=Edit("stale");area.data.name="host newer";AreaManagement.Execute(host,stale);
Check(area.data.name=="host newer","stale same-field edit rejected");
var independent=Edit("new name");area.data.group=3;AreaManagement.Execute(host,independent);
Check(area.data.name=="new name"&&area.data.group==3,"independent field edits preserved");
var invalid=Edit("bad");invalid.After!.Group=99;AreaManagement.Execute(host,invalid);
Check(area.data.name=="new name","malformed settings rejected before changes");
host.Accept=false;AreaManagement.Execute(host,Edit("inactive"));host.Accept=true;
Check(area.data.name=="new name","inactive peer cannot edit");
var wrong=Edit("wrong");wrong.ZoneUid=99;AreaManagement.Execute(host,wrong);
Check(area.data.name=="new name","wrong-zone edit rejected");
var oldPc=EClass.pc;
var expand=new AreaCommand {Id=Guid.NewGuid(),ZoneUid=10,Revision=BuildingPlanning.Capture().Revision,AreaUid=area.uid,Operation=AreaOperation.Expand,Start=new(){X=1,Z=3},End=new(){X=1,Z=3}};
AreaManagement.Execute(host,expand);
Check(area.points.Count==3&&ReferenceEquals(EClass.pc,oldPc),"host expands area and restores player context");
var state=BuildingPlanning.Capture();
NetSession.Instance.Connection=client;BuildingPlanning.Reset();BuildingPlanning.Observe(10,state.Revision);BuildingPlanning.Observe(10,state.Revision);
Check(client.Delta.Items.Count==1,"missing baseline requests once per advertised revision");
BuildingPlanning.Receive(state);
Check(BuildingPlanning.HasState&&Area.Destroyed==0&&ReferenceEquals(EClass._map.rooms.mapIDs[area.uid],area),"baseline preserves identity without destructive cleanup");
BuildingPlanning.Receive(new(){ZoneUid=10,Revision=state.Revision+1,Areas=[],Markers=[new(){Index=9,Block=true}],NextAreaUid=2});
Check(EClass._map.rooms.listArea.Count==0&&new Point(1,1).detail?.area==null&&Area.Destroyed==0,"removed area detaches silently on client");
var renderer=new BaseTileMap {cx=1,cz=1};BuildingPlanning.Draw(renderer,new());
Check(renderer.passGuideBlock.Draws==1&&EClass._map.tasks.designations.mapAll.Count==0,"marker draws without creating executable tasks");
BuildingPlanning.Receive(state);Check(EClass._map.rooms.listArea.Count==0,"old snapshot cannot resurrect deleted area");
BuildingPlanning.Reset();Check(!BuildingPlanning.HasState,"zone/reconnect reset clears planning display");

NetSession.Instance.Connection=host;BuildingPlanning.Reset();DesignationManagement.Reset();
DesignationCommand Job(DesignationOperation op,int x,int peer=1)=>new(){Id=Guid.NewGuid(),OriginPeer=peer,ZoneUid=10,Revision=BuildingPlanning.Capture().Revision,Operation=op,Start=new(){X=x,Z=2},End=new(){X=x,Z=2},Ramp=3};
var job=Job(DesignationOperation.Mine,2);DesignationManagement.Execute(host,job);DesignationManagement.Execute(host,job);
Check(EClass._map.tasks.designations.mapAll.Count==1,"duplicate job packet creates only one task");
var other=Job(DesignationOperation.Cut,3,2);other.Revision=initial.Revision;DesignationManagement.Execute(host,other);
Check(EClass._map.tasks.designations.mapAll.Count==2,"independent queued work is revalidated instead of rejected for unrelated plan changes");
DesignationManagement.Execute(host,Job(DesignationOperation.Undo,0));
Check(!EClass._map.tasks.designations.mapAll.ContainsKey(18)&&EClass._map.tasks.designations.mapAll.ContainsKey(19),"client undo cancels only its own batch");
var staleCancel=Job(DesignationOperation.Cancel,3);EClass._map.tasks.designations.mapAll[19].Working=true;BuildingPlanning.Dirty();BuildingPlanning.Capture();DesignationManagement.Execute(host,staleCancel);
Check(EClass._map.tasks.designations.mapAll.ContainsKey(19),"stale cancel cannot remove changed plan");
DesignationManagement.Execute(host,Job(DesignationOperation.Cancel,3));
Check(EClass._map.tasks.designations.mapAll.Count==0,"confirmed cancellation executes host cleanup");
new Point(4,2).sourceBlock.tileType.CanInstaComplete=true;DesignationManagement.Execute(host,Job(DesignationOperation.Mine,4));
Check(EClass._map.tasks.designations.mapAll.Count==0,"forced instant terrain operation cannot become a free queued job");
Check(ReferenceEquals(EClass.pc,oldPc),"job execution restores player context on rejection");
Console.WriteLine($"{checks} checks passed");
