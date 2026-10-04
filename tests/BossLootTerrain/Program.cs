using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
using HarmonyLib;
using System.Reflection.Emit;
using Mono.Cecil;
void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
// Validate replacement sites against the actual installed game assembly, without native detours.
using var module=ModuleDefinition.ReadModule(args[0]);
var vanilla=module.Types.Single(t=>t.Name=="Chara").Methods.Single(m=>m.Name=="TryDropBossLoot");
var calls=vanilla.Body.Instructions.Where(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="Point" && m.Name is "SetBlock" or "SetObj").Select(i=>(MethodReference)i.Operand).ToArray();
Check(calls.Length==2 && calls.Count(m=>m.Name=="SetBlock"&&m.Parameters.Count==2)==1 && calls.Count(m=>m.Name=="SetObj"&&m.Parameters.Count==3)==1,"installed vanilla boss loot has exactly the supported terrain call sites");
var codes=calls.Select(m=>new CodeInstruction(OpCodes.Callvirt,AccessTools.Method(typeof(Point),m.Name))).ToList();
var label=new DynamicMethod("label",typeof(void),Type.EmptyTypes).GetILGenerator().DefineLabel();codes[0].labels.Add(label);
var routed=BossLootTerrainPatch.RouteTerrain(codes).ToArray();
Check(routed.All(c=>c.opcode==OpCodes.Call&&((System.Reflection.MethodInfo)c.operand).DeclaringType==typeof(BossLootTerrainPatch)) && routed[0].labels.Contains(label),"transpiler replaces only terrain calls and preserves branch labels");
var failed=false;try{BossLootTerrainPatch.RouteTerrain([]).ToArray();}catch(InvalidOperationException){failed=true;}
Check(failed,"changed vanilla shape fails explicitly");
var host=new ElinNetHost();var client=new ElinNetClient();NetSession.Instance.Connection=host;
var chosen=new Point(2,2);var alternate=new Point(3,2);Point.Tiles[(2,2)]=(5,9);Point.Tiles[(3,2)]=(6,8);
BossLootTerrainPatch.SetBlock(chosen,0,0);BossLootTerrainPatch.SetObj(chosen,0,1,0);
Check(Point.Tiles[(2,2)]==(0,0)&&host.Delta.Items.Count==2,"host clears chosen chest tile and publishes both edits");
Point.Tiles[(2,2)]=(5,9);NetSession.Instance.Connection=client;
foreach(var delta in host.Delta.Items)delta.Apply(client);
Check(Point.Tiles[(2,2)]==(0,0),"client clears exact host tile");
// The authoritative chest already occupies chosen; local search selects alternate.
BossLootTerrainPatch.SetBlock(alternate,0,0);BossLootTerrainPatch.SetObj(alternate,0,1,0);
Check(Point.Tiles[(3,2)]==(6,8),"client death replay cannot clear alternate chest location");
foreach(var delta in host.Delta.Items)delta.Apply(client);
Check(Point.Tiles[(2,2)]==(0,0)&&Point.Tiles[(3,2)]==(6,8),"duplicate edits are idempotent and leave neighboring terrain intact");
Point.Tiles[(2,2)]=(5,9);foreach(var delta in host.Delta.Items)delta.Apply(host);
Check(Point.Tiles[(2,2)]==(5,9),"host rejects incoming terrain edits");
EClass._zone.uid=8;foreach(var delta in host.Delta.Items)delta.Apply(client);
Check(Point.Tiles[(2,2)]==(5,9),"late edits cannot touch another zone");EClass._zone.uid=7;
new BossLootTileDelta{ZoneUid=7,Pos=new(){X=99,Z=99},Block=true,Id=0}.Apply(client);
Check(Point.Tiles.Count==2,"out-of-bounds edit is ignored");
NetSession.Instance.Connection=host;CharaProgressCompleteEvent.Packing=true;var before=host.Delta.Items.Count;
BossLootTerrainPatch.SetBlock(chosen,0,0);BossLootTerrainPatch.SetObj(chosen,0,1,0);
Check(CharaProgressCompleteEvent.Packed.Count==2&&host.Delta.Items.Count==before,"progress-triggered boss kill packs edits into completion stream");
NetSession.Instance.Connection=null;Point.Tiles[(3,2)]=(6,8);
BossLootTerrainPatch.SetBlock(alternate,0,0);BossLootTerrainPatch.SetObj(alternate,0,1,0);
Check(Point.Tiles[(3,2)]==(0,0)&&host.Delta.Items.Count==before,"single-player retains vanilla edits without networking");

// The movement path uses GoalRemote on clients; capture host edits rather than
// relying on the client's CanDestroyPath/GoalCombat predicate.
var charaType=module.Types.Single(t=>t.Name=="Chara");
var path=charaType.Methods.Single(m=>m.Name=="DestroyPath");
Check(path.HasBody && charaType.Methods.Single(m=>m.Name=="CanDestroyPath").Body.Instructions.Any(i=>i.Operand is TypeReference t && t.Name=="GoalCombat"),"installed native wall-breaking eligibility depends on GoalCombat");
var mapType=module.Types.Single(t=>t.Name=="Map");
Check(mapType.Methods.Any(m=>m.Name=="SetBlock"&&m.Parameters.Count==5) && mapType.Methods.Any(m=>m.Name=="SetObj"&&m.Parameters.Count==7),"installed terrain setters match capture hooks");
Check(mapType.Methods.Single(m=>m.Name=="RemoveLonelyRamps").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="MineBlock"),"native dependent ramp cleanup re-enters captured mining path");
CharaProgressCompleteEvent.Packing=false;NetSession.Instance.Connection=host;
before=host.Delta.Items.Count;
PathTerrainSync.Block(EClass._map,2,2);
Check(host.Delta.Items.Count==before,"ordinary mining and building are not captured again");
Check(PathTerrainSync.Begin(out var outer),"host runs native path destruction");
EClass._map.cells[2,2]._block=0;PathTerrainSync.Block(EClass._map,2,2);
PathTerrainSync.Begin(out var inner);PathTerrainSync.Object(EClass._map,2,2);PathTerrainSync.End(inner);
PathTerrainSync.Block(EClass._map,3,2);PathTerrainSync.End(outer);
Check(host.Delta.Items.Count==before+3,"nested path and dependent tile changes publish exact edits");
PathTerrainSync.Block(EClass._map,2,2);
Check(host.Delta.Items.Count==before+3,"finalizer restores capture scope, including exceptional exits");
NetSession.Instance.Connection=client;
Check(!PathTerrainSync.Begin(out outer),"client movement never independently mines walls or creates drops");PathTerrainSync.End(outer);
NetSession.Instance.Connection=null;
Check(PathTerrainSync.Begin(out outer),"solo destruction remains native");PathTerrainSync.End(outer);
var shadows=EClass._map.Shadows;var fovs=EClass._map.Fovs;
new BossLootTileDelta{ZoneUid=7,Pos=new(){X=2,Z=2},Block=true,Material=4,Id=0,Direction=2}.Apply(client);
Check(EClass._map.Shadows==shadows+2&&EClass._map.Fovs==fovs+1&&EClass._map.cells[2,2].blockDir==2,"authoritative terrain refreshes shadows/FOV and preserves block direction");

// Tp floor mining shares the scoped native mutation capture, including nesting.
Check(mapType.Methods.Any(m=>m.Name=="SetFloor"&&m.Parameters.Count==5),"installed floor setter matches optional spell capture");
NetSession.Instance.Connection=host;before=host.Delta.Items.Count;
var endSpell=PathTerrainSync.CaptureMap(EClass._map);
EClass._map.SetFloor(2,2,6,4,3);PathTerrainSync.Floor(EClass._map,2,2);
PathTerrainSync.Begin(out outer);PathTerrainSync.End(outer);
PathTerrainSync.Floor(EClass._map,2,2);endSpell();
PathTerrainSync.Floor(EClass._map,2,2);
Check(host.Delta.Items.Count==before+2,"nested path capture restores spell capture and final scope stops publishing");
EClass._map.SetFloor(2,2,1,1,0);
host.Delta.Items[before].Apply(client);
Check(EClass._map.cells[2,2]._floor==4&&EClass._map.cells[2,2]._floorMat==6&&EClass._map.cells[2,2].floorDir==3,"client applies exact floor result without mining again");

// Check FLAM against installed IL and exercise the production terrain wrappers.
var damage=module.Types.Single(t=>t.Name=="ActEffect").Methods.Single(m=>m.Name=="DamageEle");
var mining=damage.Body.Instructions.Where(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="Map" && m.Name is "MineBlock" or "MineObj").Select(i=>(MethodReference)i.Operand).ToArray();
Check(mining.Length==2 && mining.Count(m=>m.Name=="MineBlock"&&m.Parameters.Count==4)==1 && mining.Count(m=>m.Name=="MineObj"&&m.Parameters.Count==3)==1,"installed explosion mining signatures match wrappers");
var ec=mining.Select(m=>new CodeInstruction(OpCodes.Callvirt,AccessTools.Method(typeof(Map),m.Name))).ToList();ec[0].labels.Add(label);
var er=ExplosionTerrainPatch.RouteTerrain(ec).ToArray();
Check(er.All(c=>c.opcode==OpCodes.Call&&((System.Reflection.MethodInfo)c.operand).DeclaringType==typeof(ExplosionTerrainPatch))&&er[0].labels.Contains(label),"explosion routes mining and preserves labels");
failed=false;try{ExplosionTerrainPatch.RouteTerrain([]);}catch(InvalidOperationException){failed=true;}
Check(failed,"unsupported explosion shape fails explicitly");
NetSession.Instance.Connection=host;before=host.Delta.Items.Count;
ExplosionTerrainPatch.MineBlock(EClass._map,chosen,false,null!,true);
ExplosionTerrainPatch.MineObj(EClass._map,chosen,null!,null!);
Check(host.Delta.Items.Count==before+2,"host explosion publishes block/object results without throw replay");
var mines=EClass._map.Mines;NetSession.Instance.Connection=client;
ExplosionTerrainPatch.MineBlock(EClass._map,chosen,false,null!,true);
ExplosionTerrainPatch.MineObj(EClass._map,chosen,null!,null!);
Check(EClass._map.Mines==mines,"client explosion cannot mine or create terrain drops");
NetSession.Instance.Connection=null;ExplosionTerrainPatch.MineBlock(EClass._map,chosen,false,null!,true);
Check(EClass._map.Mines==mines+1 && host.Delta.Items.Count==before+2,"solo retains native mining without packets");
NetSession.Instance.Connection=host;EClass._map.FailMining=true;
try{ExplosionTerrainPatch.MineBlock(EClass._map,chosen,false,null!,true);}catch(InvalidOperationException){}
EClass._map.FailMining=false;before=host.Delta.Items.Count;PathTerrainSync.Block(EClass._map,2,2);
Check(host.Delta.Items.Count==before,"exception restores explosion capture scope");
var endOuter=PathTerrainSync.CaptureMap(EClass._map);
ExplosionTerrainPatch.MineBlock(EClass._map,chosen,false,null!,true);
PathTerrainSync.Object(EClass._map,2,2);endOuter();
Check(host.Delta.Items.Count==before+2,"explosion preserves enclosing capture scope");
