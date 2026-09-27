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
