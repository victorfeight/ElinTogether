using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
using HarmonyLib;
using Mono.Cecil;
using System.Reflection.Emit;
int checks=0;
void Check(bool ok,string why) { if(!ok) throw new Exception(why); Console.WriteLine("PASS "+why); checks++; }
var host = new ElinNetHost(); var client = new ElinNetClient();
NetSession.Instance.Connection = host;
var local = EClass.pc; local.uid=1; var remote = new Chara { uid=2 }; host.ActiveRemoteCharas[1]=remote;
var originOwner = new Chara { uid=123 };
var originQuest = new Quest { uid=1234, person=new() { chara=originOwner } };
QuestReplica.Apply(originQuest, true);
Check(originOwner.quest == originQuest, "replica links giver directly without invoking native SetClient side effects");
EClass.game.quests.list.Clear();
var old = new Quest { uid=5, counter=1, track=true, isNew=true, task=new() };
var fresh = new Quest { uid=5, counter=7, task=new() };
EClass.game.quests.list.Add(old);
NetSession.Instance.Connection=client;
new RandomQuestStateSnapshot { Revision=1, Quests=[LZ4Bytes.Create(fresh)] }.Apply();
Check(ReferenceEquals(EClass.game.quests.list.Single(),old) && old.counter==7,"replica updates saved progress without replacing tracker quest reference");
Check(old.track && old.isNew && old.task!.owner==old,"local tracker flags retained and new task rebound to live quest");
new RandomQuestStateSnapshot { Revision=0, Quests=[] }.Apply();
Check(EClass.game.quests.list.Count==1,"older state cannot remove current quest");
var story = new Quest { uid=6, IsRandomQuest=false }; var guild=new Quest { uid=7,Guild=true };
EClass.game.quests.list.AddRange([story,guild]); old.person.chara=remote; remote.quest=old; old.ClientZone=new();
new RandomQuestStateSnapshot { Revision=2, CompletedIds=["finished"], CompletedTypes=["QuestDeliver"] }.Apply();
Check(!EClass.game.quests.list.Contains(old) && remote.quest==null && old.ClientZone.completedQuests.Contains(5),"removed quest clears giver and zone bookkeeping without replaying failure");
Check(EClass.game.quests.list.Count==2 && EClass.game.quests.completedIDs.Contains("finished"),"ordinary removal preserves story/guild state and merges completion history");
NetSession.Instance.Connection=host;
new RandomQuestStateSnapshot { Revision=99 }.Apply();
Check(EClass.game.quests.list.Count==2,"host rejects replica application");
RandomQuestStateSnapshot.Reset(); EClass.game.quests.list.Add(fresh);
var snap=RandomQuestStateSnapshot.Capture(); var count=LZ4Bytes.Captures;
Check(ReferenceEquals(snap,RandomQuestStateSnapshot.Capture()) && count==LZ4Bytes.Captures,"unchanged snapshots reuse cached serialization");
RandomQuestStateSnapshot.MarkDirty(); var next=RandomQuestStateSnapshot.Capture();
Check(next.Revision>snap.Revision && next.Quests.Length==1,"mutation invalidates random-only state cache");
NetSession.Instance.Connection=client;
Check(!SharedQuestLifecyclePatch.Before(fresh) && !SharedQuestLifecyclePatch.Before(new QuestTask { owner=fresh }),"client quest and task mutations remain host-owned");
Check(SharedQuestLifecyclePatch.Before(story) && SharedQuestLifecyclePatch.Before(guild),"existing story and guild ownership paths left intact");
Check(!SharedQuestEventAuthorityPatch.Before(new ZoneEventQuest()) && SharedQuestEventAuthorityPatch.Before(new ZoneEvent()),"only quest events blocked on replica");
Check(!QuestExpansionPreEnterPatch.Before(),"client cannot execute expansion ambush spawn or decision callbacks");
EClass.player.returnInfo = new object();
Check(!SharedQuestInstanceReturnPatch.Before() && EClass.player.returnInfo == null,
    "client instance return skips completion after quest removal while retaining native travel cleanup");
NetSession.Instance.Connection=host;
var returnInfo = EClass.player.returnInfo = new object();
Check(SharedQuestInstanceReturnPatch.Before() && EClass.player.returnInfo == returnInfo,
    "host retains native instance success/failure processing");
NetSession.Instance.Connection=null;
Check(SharedQuestInstanceReturnPatch.Before(), "solo instance return retains native processing");
Check(SharedQuestLifecyclePatch.Before(fresh) && SharedQuestEventAuthorityPatch.Before(new ZoneEventQuest()) && QuestExpansionPreEnterPatch.Before(),"solo lifecycle unchanged");
NetSession.Instance.Connection=host;
Check(QuestExpansionCompat.QuestLimit==5,"optional expansion absent falls back to native limit");
var parcel=new Thing { uid=11 }; remote.things.Add(parcel);
var delivery=new QuestDeliver();
Check(QuestExpansionCompat.PartyDeliveryItems(delivery,true).Single()==parcel && EClass.pc==local,"ambush eligibility includes remote parcel and restores host actor");
delivery.Throw=true;
try { QuestExpansionCompat.PartyDeliveryItems(delivery,true); } catch(InvalidOperationException) { }
Check(EClass.pc==local,"parcel check restores actor on exception");
remote.ai.child=new AI_PlayMusic();
Check(QuestExpansionCompat.PartyPerforming(local.ai),"remote wrapped performance counts for shared concert mood");
remote.ai.child.IsRunning=false;
Check(!QuestExpansionCompat.PartyPerforming(local.ai),"finished remote performance gives no mood bonus");
Check(QuestExpansionCompat.PartyPerforming(new AI_PlayMusic()),"native host performance remains eligible");
var rewritten=QuestExpansionMusicPatch.Transpile([new CodeInstruction(OpCodes.Isinst,typeof(AI_PlayMusic)),new CodeInstruction(OpCodes.Brfalse_S)]).ToArray();
Check(rewritten[0].opcode==OpCodes.Call && rewritten[1].opcode==OpCodes.Brfalse_S,"concert adapter preserves original single chance branch");
EClass.game.quests.list.Clear(); var harvest=new QuestHarvest { uid=30 }; EClass.game.quests.list.Add(harvest);
EClass._zone.events.list.Add(new ZoneEventQuest { uidQuest=30 });
var chest=new Thing { uid=40,trait=new TraitFarmChest() }; EClass._map.things.Add(chest);
Thing Item(int uid) { var t=new Thing { uid=uid,Root=remote }; remote.things.Add(t); return t; }
QuestDepositDelta Deposit(Thing t,int amount=2) => new(){ RequestId=Guid.NewGuid(), ZoneUid=10,QuestUid=30, Chest=chest, Items=[new RemoteCard(t){Num=amount}] };
var crop=Item(50); var req=Deposit(crop); req.Apply(host); req.Apply(host);
Check(crop.Num==1 && harvest.weightDelivered==20 && EClass.pc==local,"selected quantity consumed and credited once despite duplicate packet");
var freshCrop=Item(51); var wrong=Deposit(freshCrop); wrong.ZoneUid=999; wrong.Apply(host);
Check(freshCrop.Num==3 && harvest.weightDelivered==20,"old-zone deposit cannot consume");
wrong=Deposit(freshCrop); wrong.QuestUid=999; wrong.Apply(host);
Check(freshCrop.Num==3,"stale quest identity rejected");
remote.Distance=10; Deposit(freshCrop).Apply(host); remote.Distance=1;
Check(freshCrop.Num==3,"remote chest deposit rejected");
freshCrop.Root=local; Deposit(freshCrop).Apply(host); freshCrop.Root=remote;
Check(freshCrop.Num==3,"cannot consume another player's inventory");
freshCrop.Eligible=false; Deposit(freshCrop).Apply(host); freshCrop.Eligible=true;
Check(freshCrop.Num==3,"mod/native guide eligibility enforced by host");
freshCrop.Accessible=false; Deposit(freshCrop).Apply(host); freshCrop.Accessible=true;
Check(freshCrop.Num==3,"inaccessible bag contents rejected");
Deposit(freshCrop,4).Apply(host);
Check(freshCrop.Num==3,"oversized selection rejected without partial consumption");
var bulk=Deposit(freshCrop,3); var second=Item(52); bulk.Items=[new(freshCrop),new(freshCrop),new(second)]; bulk.Apply(host);
Check(harvest.weightDelivered==80 && freshCrop.isDestroyed && second.isDestroyed,"bulk selection consumes unique eligible stacks exactly once");
NetSession.Instance.Connection=client; var clientItem=Item(53); QuestDeposits.Submit(new(chest),[clientItem]);
Check(!clientItem.isDestroyed && clientItem.Num==3 && client.Delta.Items.Single() is QuestDepositDelta,"client sends intent without destroying or incrementing weight");
Deposit(clientItem).Apply(client);
Check(!clientItem.isDestroyed,"deposit packet never executes on client");

client.Delta.Items.Clear();
Check(!QuestDepositPatch.Before(new(chest),clientItem) && client.Delta.Items.Count==1 && !clientItem.isDestroyed,
    "single-item UI waits for host rather than consuming locally");
ElinDelta.IsApplying=true;
Check(!QuestDepositPatch.Before(new(chest),clientItem) && client.Delta.Items.Count==1,
    "replay cannot queue a new deposit or consume again");
ThingRequest.IsReplayingIntent=true;
Check(!QuestDepositPatch.Before(new(chest),clientItem) && client.Delta.Items.Count==2 && !clientItem.isDestroyed,
    "host-confirmed player drag submits delivery during delta application without consuming locally");
var resumedDeposit = (QuestDepositDelta)client.Delta.Items.Last();
NetSession.Instance.Connection=host;
var beforeDelivery=harvest.weightDelivered;
resumedDeposit.Apply(host); resumedDeposit.Apply(host);
Check(clientItem.isDestroyed && harvest.weightDelivered==beforeDelivery+30,
    "resumed delivery reaches host and consumes selected stack exactly once");
NetSession.Instance.Connection=client;
ThingRequest.IsReplayingIntent=false;
client.Delta.Items.RemoveAt(1);
clientItem=Item(54);
ElinDelta.IsApplying=false;
var grid=new LayerDragGrid(); EClass.player.chara=remote;
QuestBulkDepositPatch.After(grid,new InvOwnerDeliver(chest)); grid.buttonDeliver.Action!();
Check(grid.Closed && client.Delta.Items.Count==2 && !clientItem.isDestroyed,
    "bulk button sends one transaction and closes without local consumption");
EClass.player.chara=local;
NetSession.Instance.Connection=null;
Check(QuestDepositPatch.Before(new(chest),clientItem), "solo single deposit keeps native execution");
NetSession.Instance.Connection=client;
RandomQuestStateSnapshot.Reset();
new RandomQuestStateSnapshot { Revision=1,Quests=[LZ4Bytes.Create(new Quest {uid=700})] }.Apply();
Check(EClass.game.quests.list.Single().uid==700,"new session accepts lower revision and replaces previous random quest membership");

// Verify the actual installed assembly anchors, not a remembered mod version.
using var native=ModuleDefinition.ReadModule(args[0]); using var mod=ModuleDefinition.ReadModule(args[1]);
IEnumerable<TypeDefinition> All(TypeDefinition t) => new[]{t}.Concat(t.NestedTypes.SelectMany(All));
MethodDefinition Method(ModuleDefinition m,string type,string method) => m.Types.SelectMany(All).Single(t=>t.FullName==type).Methods.Single(x=>x.Name==method);
string[] Calls(MethodDefinition m) => m.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>((MethodReference)i.Operand).Name).ToArray();
Check(Calls(Method(native,"ZoneInstanceRandomQuest","OnLeaveZone")).Contains("AddPreEnter"),
    "installed vanilla instance exit queues the completion callback");
var instanceReturn = Method(native,"ZonePreEnterOnCompleteQuestInstance","Execute");
Check(Calls(instanceReturn).Contains("Complete") && Calls(instanceReturn).Contains("Fail") &&
    instanceReturn.Body.Instructions.Any(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Stfld && i.Operand is FieldReference {Name:"returnInfo"}),
    "installed completion callback owns success/failure and returnInfo cleanup");
Check(Calls(Method(mod,"RandomQuestExpantion.Patch.DramaPatch","HandleOnInvestShopEvent")).Contains("ForeachReverse"),"installed mod has its native investment dispatch callback");
Check(Method(mod,"RandomQuestExpantion.Config.ModConfig","get_MaxQuestLimit").ReturnType.FullName=="System.Int32","installed quest limit adapter has correct signature");
Check(Calls(Method(mod,"RandomQuestExpantion.ModQuests.Common.QuestRiskyDeliver","ShouldOverrideEncounter")).Count(n=>n=="ListDestThing")==1,"risky delivery has one inventory query; native probability retained");
var music=Method(mod,"RandomQuestExpantion.ModQuestEvent.ZoneEventMusicMonster","_OnTickRound");
Check(music.Body.Instructions.Count(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Isinst && i.Operand is TypeReference {Name:"AI_PlayMusic"})==1,"installed concert has exactly one replaceable performance condition");
var musicTest=music.Body.Instructions.Single(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Isinst && i.Operand is TypeReference {Name:"AI_PlayMusic"});
Check(musicTest.Next.OpCode.Code is Mono.Cecil.Cil.Code.Brfalse or Mono.Cecil.Cil.Code.Brfalse_S, "concert type test branches directly so boolean replacement preserves IL stack");
Check(Calls(Method(mod,"RandomQuestExpantion.ModQuests.ThiefGuild.QuestTGRiskyEscort","OnDropReward")).Count(n=>n=="get_MerchantGuildZone")==1,"installed thief reward has the confirmed wrong guild accessor");
Check(Method(mod,"RandomQuestExpantion.General.General","get_ThiefGuildZone").ReturnType.FullName=="Zone","correct thief destination accessor exists");
Check(Calls(Method(native,"InvOwnerDeliver","_OnProcess")).Contains("Destroy"),"native single deposit consumes via normal inventory destruction hooks");
var bulkCallback=native.Types.Single(t=>t.Name=="LayerDragGrid").NestedTypes.SelectMany(All).SelectMany(t=>t.Methods).Where(m=>m.HasBody).Any(m=>m.Body.Instructions.Any(i=>i.Operand is FieldReference {Name:"weightDelivered"}));
Check(bulkCallback,"native bulk UI directly writes progress and needs the unified transaction adapter");
Check(Calls(Method(mod,"RandomQuestExpantion.General.DramaWrapper","Release")).Contains("CleanArgumentsAndResults"),"drama release is a stable completion boundary for direct rescue callback fields");
Console.WriteLine($"{checks} quest replication checks passed");
