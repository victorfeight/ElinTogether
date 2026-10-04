using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
using Mono.Cecil;
int checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
using var module=ModuleDefinition.ReadModule(args[0]);
IEnumerable<TypeDefinition> Types(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(Types));
var board=module.Types.Single(t=>t.Name=="ListPeopleParty");
var load=Types(board).SelectMany(t=>t.Methods).Where(m=>m.HasBody&&m.Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="JoinParty")).ToArray();
// The public OnClick is a separate direct entry; the preset is one captured callback.
var capturedLoad=load.Where(m=>m.DeclaringType!=board).ToArray();
Check(capturedLoad.Length==1&&capturedLoad[0].DeclaringType.Fields.Count(f=>f.FieldType.Name=="PartySetup")==1,"installed preset callback uniquely captures PartySetup");
Check(capturedLoad[0].Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="Disband")&&capturedLoad[0].Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="Ride"),"whole preset callback includes disband and direct ride calls");
var favorite=Types(board).SelectMany(t=>t.Methods).Where(m=>m.HasBody&&m.Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="set_isFav")).ToArray();
Check(favorite.Length==1&&favorite[0].DeclaringType.Fields.Count(f=>f.FieldType.Name=="Chara")==1,"installed favorite callback uniquely captures character");
foreach(var name in new[]{"TraitPartyBoard","TraitBookRoster"}) Check(Types(module.Types.Single(t=>t.Name==name)).SelectMany(t=>t.Methods).Any(m=>m.HasBody&&m.Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name=="CreateParty")),name+" opens the same patched party UI");

var home=new Zone{uid=8,IsPCFaction=true};EClass.game.spatials.zones[7]=EClass._zone;EClass.game.spatials.zones[8]=home;
var party=new Party();
Chara Make(int uid,bool human=false){var c=new Chara{uid=uid,IsPlayer=human,CHA=10,homeZone=home};EClass._zone.AddCard(c,uid,uid);EClass.game.cards.globalCharas[uid]=c;return c;}
var hostActor=Make(1,true);var clientActor=Make(2,true);hostActor.CHA=100;
NetSession.Instance.Player=hostActor;NetSession.Instance.CurrentPlayers.Add(new(){CharaUid=1});NetSession.Instance.CurrentPlayers.Add(new(){CharaUid=2});
party.AddMemeber(hostActor);party.AddMemeber(clientActor);EClass.player.chara=hostActor;
var host=new ElinNetHost();host.ActiveRemoteCharas[1]=clientActor;NetSession.Instance.Connection=host;
EClass.player.chara=clientActor;
Check(!hostActor.IsPlayer&&PartyMembership.Protected(hostActor),"host remains protected while vanilla pc temporarily points at client");
EClass.player.chara=hostActor;clientActor.Human=false;
Check(!clientActor.IsPlayer&&PartyMembership.Protected(clientActor),"taking-a-break human remains protected by session identity");clientActor.Human=true;
var npc=Make(3);var mount=Make(4);var otherMount=Make(5);var saved=Make(6);saved.Remote=true;
party.AddMemeber(otherMount);hostActor.ride=otherMount;otherMount.host=hostActor;
party.AddMemeber(saved);
PartyBoardCommandDelta Request(PartyBoardOperation op,int uid=0,int[]? members=null,int ride=0,int parasite=0,bool favorite=false)=>new(){RequestId=Guid.NewGuid(),ZoneUid=7,Operation=op,MemberUid=uid,Members=members??[],RideUid=ride,ParasiteUid=parasite,Favorite=favorite};
void Run(PartyBoardOperation op,int uid=0)=>Request(op,uid).Apply(host);
npc.trait.RequiredCHA=20;Run(PartyBoardOperation.Join,3);Check(npc.party==null,"client recruitment uses client CHA rather than host CHA");
clientActor.CHA=25;home.AddCard(npc,9,9);Run(PartyBoardOperation.Join,3);
Check(npc.party==party&&npc.currentZone==EClass._zone&&npc.pos.X==clientActor.pos.X+1,"eligible off-map NPC is recalled beside requesting player");
Check(npc.homeZone==home&&EClass.pc==hostActor,"recall preserves home and restores host actor");
Run(PartyBoardOperation.Join,3);Check(party.members.Count(c=>c==npc)==1,"repeated join does not duplicate membership");
Run(PartyBoardOperation.Dismiss,3);Check(npc.party==null&&npc.currentZone==home,"client board dismissal sends NPC home");
npc.homeZone=EClass._zone;Run(PartyBoardOperation.Join,3);Run(PartyBoardOperation.Dismiss,3);Check(npc.party==null&&npc.currentZone==EClass._zone,"same-home dismissal stays visible on home map");npc.homeZone=home;
Run(PartyBoardOperation.Dismiss,1);Check(hostActor.party==party&&hostActor.Moves==0,"client cannot dismiss or move the host");
Run(PartyBoardOperation.Dismiss,5);Check(otherMount.party==party&&otherMount.host==hostActor,"client cannot dismiss the other player's mount");
Run(PartyBoardOperation.Join,3);Run(PartyBoardOperation.Disband);
Check(hostActor.party==party&&clientActor.party==party&&saved.party==party&&hostActor.Moves==0&&clientActor.Moves==0&&saved.Moves==0,"disband protects both humans and saved player characters from removal and movement");
Check(otherMount.party==party&&hostActor.ride==otherMount&&npc.party==null,"disband preserves other player's mount while dismissing available NPCs");
npc.Suspended=true;Run(PartyBoardOperation.Join,3);Check(npc.party==null,"suspended NPC cannot join");npc.Suspended=false;
npc.isDead=true;Run(PartyBoardOperation.Join,3);Check(npc.party==null,"dead NPC cannot join");npc.isDead=false;
npc.memberType=FactionMemberType.Livestock;Run(PartyBoardOperation.Join,3);Check(npc.party==null,"livestock excluded from board candidates");npc.memberType=FactionMemberType.Default;
npc.IsPCFaction=false;Run(PartyBoardOperation.Join,3);Check(npc.party==null,"outsider cannot be recruited through board");npc.IsPCFaction=true;
var preset=Request(PartyBoardOperation.Load,members:[1,2,3,3,4,5,6,9999],ride:4,parasite:3);preset.Apply(host);
Check(npc.party==party&&mount.party==party&&party.members.Count(c=>c==npc)==1,"preset skips missing and protected entries and deduplicates NPCs");
Check(clientActor.ride==mount&&clientActor.parasite==npc&&mount.host==clientActor&&npc.host==clientActor,"preset restores requester's mount and parasite");
Check(hostActor.ride==otherMount&&otherMount.host==hostActor,"preset does not steal the other player's mount");
var nested=Make(40);nested.host=otherMount;otherMount.parasite=nested;
Check(!PartyMembership.ManagedBy(nested,clientActor),"nested mount chain protects the other human rider");otherMount.parasite=null;nested.host=null;
int rides=ActRide.Calls,moves=npc.Moves;preset.Apply(host);Check(ActRide.Calls==rides&&npc.Moves==moves,"duplicate preset request does not replay disband or riding");
Request(PartyBoardOperation.Favorite,3,favorite:true).Apply(host);Check(npc.isFav,"client favorite committed on host");
Request(PartyBoardOperation.Favorite,3,favorite:true).Apply(host);Check(npc.isFav,"favorite request sets desired state rather than toggling twice");
host.Accept=false;Run(PartyBoardOperation.Disband);Check(clientActor.ride==mount,"blocked peer cannot disband");host.Accept=true;
new PartyBoardCommandDelta{RequestId=Guid.NewGuid(),ZoneUid=8,Operation=PartyBoardOperation.Disband}.Apply(host);Check(clientActor.ride==mount,"stale-zone request rejected");
host.Delta.Sent.Clear();Run(PartyBoardOperation.Refresh);Check(host.Sent.Last() is PartyBoardStateDelta,"opening client board gets authoritative snapshot");

// Recreate stale client state and land the production board result.
var state=(PartyBoardStateDelta)host.Sent.Last();
var stale=Make(20);party.AddMemeber(stale);
party.RemoveMember(npc);party.RemoveMember(mount);home.AddCard(mount,10,10);npc.isFav=false;clientActor.ride=null;clientActor.parasite=null;
var client=new ElinNetClient();NetSession.Instance.Connection=client;EClass.player.chara=clientActor;
var ui=new ListPeopleParty{main=true};PartyBoardManagement.OpenList=new(ui);
EClass.game.cards.globalCharas.Remove(npc.uid);
state.Apply(client);
Check(EClass.game.cards.globalCharas.GetValueOrDefault(npc.uid)==npc,"newly mirrored global NPC is registered for vanilla board candidate lookup");
Check(npc.party==party&&mount.party==party&&mount.currentZone==EClass._zone&&mount.parent==EClass._zone,"result repairs membership and recalls NPC onto client map");
Check(stale.party==null&&hostActor.party==party&&clientActor.party==party,"result removes old client-only membership without removing humans");
Check(clientActor.ride==mount&&clientActor.parasite==npc&&mount.host==clientActor&&npc.host==clientActor,"result restores reciprocal mount links");
Check(npc.isFav&&ActRide.Calls==rides,"favorite and mounts mirror without replaying native Ride");
Check(ui.Refreshes>0&&WidgetRoster.Updates>0,"open board and roster widgets refresh after results");
state.Apply(client);Check(party.members.Count(c=>c==npc)==1&&ActRide.Calls==rides,"repeated board result is idempotent");
party.members.Reverse();party.uidMembers.Reverse();state.Apply(client);
Check(party.members.Select(c=>c.uid).SequenceEqual(state.PartyOrder)&&party.uidMembers.SequenceEqual(state.PartyOrder),"preset member order and saved member IDs follow authoritative roster");
npc.isFav=false;
new PartyBoardStateDelta{ZoneUid=8,Members=state.Members}.Apply(client);
Check(!npc.isFav,"old-zone board result is ignored after travel");
var deferred=client.Delta.Deferred.Count;
new PartyBoardStateDelta{ZoneUid=7,Members=[new(){Card=new(){Uid=99999},Pos=new(){X=1,Z=1}}]}.Apply(client);
Check(client.Delta.Deferred.Count==deferred+1&&npc.party==party,"unresolved results defer before any party mutation");
state.Apply(client);
int queued=client.Delta.Sent.Count;
Check(!PartyBoardPatch.Click(ui,npc)&&client.Delta.Sent.Last() is PartyBoardCommandDelta{Operation:PartyBoardOperation.Join},"client board click sends intent rather than local mutation");
Check(!PartyBoardPatch.Disband(party)&&client.Delta.Sent.Count==queued+2,"client disband sends one host request");
var setup=new Player.PartySetup{uids=[3,4],ride=4};
Check(!PartyBoardPresetPatch.Load(new PresetClosure{setup=setup})&&client.Delta.Sent.Last() is PartyBoardCommandDelta{Operation:PartyBoardOperation.Load},"preset callback routes complete operation");
Check(!PartyBoardFavoritePatch.Toggle(new FavoriteClosure{c=npc})&&client.Delta.Sent.Last() is PartyBoardCommandDelta{Operation:PartyBoardOperation.Favorite,Favorite:false},"favorite callback routes desired value");
Check(setup.uids.SequenceEqual([3,4])&&setup.ride==4,"personal preset is not rewritten by network routing");
NetSession.Instance.Connection=null;
Check(PartyBoardPatch.Click(ui,npc)&&PartyBoardPatch.Disband(party)&&PartyBoardPresetPatch.Load(new PresetClosure{setup=setup})&&PartyBoardFavoritePatch.Toggle(new FavoriteClosure{c=npc}),"solo UI paths remain vanilla");
NetSession.Instance.Connection=host;EClass.player.chara=hostActor;
PartyBoardManagement.Execute(host,Request(PartyBoardOperation.Dismiss,3),hostActor);Check(npc.party==party,"host cannot dismiss client's mounted NPC");
PartyBoardManagement.Execute(host,Request(PartyBoardOperation.Favorite,3,favorite:false),hostActor);Check(!npc.isFav,"host board actions use same authoritative path");
// Native failures must restore actor and publish partial outcome for reconciliation.
var bad=Make(30);bad.ThrowOnMove=true;home.AddCard(bad,3,3);host.Delta.Sent.Clear();
try{Run(PartyBoardOperation.Join,30);throw new Exception("expected native exception");}catch(InvalidOperationException){}
Check(EClass.pc==hostActor&&host.Delta.Sent.Last() is PartyBoardStateDelta,"native exception restores actor and publishes resolved state");
Console.WriteLine($"{checks} party board checks passed");
class PresetClosure {public Player.PartySetup setup=null!;}
class FavoriteClosure {public Chara c=null!;}
