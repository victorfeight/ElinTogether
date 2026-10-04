using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;

int passed=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);passed++;}
var home=new Zone{uid=8}; EClass.game.spatials.zones[8]=home;
var party=new Party();
var hostActor=new Chara{uid=1,CHA=100,IsPlayer=true,currentZone=EClass._zone};
var clientActor=new Chara{uid=2,CHA=10,IsPlayer=true,currentZone=EClass._zone};
party.AddMemeber(hostActor);party.AddMemeber(clientActor);EClass.player.chara=hostActor;
var host=new ElinNetHost();host.ActiveRemoteCharas[1]=clientActor;NetSession.Instance.Connection=host;
var npc=new Chara{uid=3,currentZone=EClass._zone,homeZone=home};
void Request(bool join)=>new PartyCommandDelta{Member=npc,ZoneUid=7,Join=join}.Apply(host);
npc.trait.RequiredCHA=20;Request(true);
Check(npc.party==null,"uses client CHA, not host CHA");
Check(EClass.pc==hostActor,"restores actor after rejection");
clientActor.CHA=25;Request(true);
Check(npc.party==party && party.members.Contains(npc),"client recruits into shared party");
Check(npc.homeZone==home,"joining preserves residence");
Check(host.Sent.Last() is PartyMemberDelta{Joined:true},"accepted membership result");
Request(true);Check(party.members.Count(c=>c==npc)==1,"duplicate request is idempotent");
Request(false);Check(npc.party==null && npc.currentZone==home,"client dismissal returns home");
Check(EClass.pc==hostActor,"restores actor after success");
npc.currentZone=EClass._zone;npc.homeZone=EClass._zone;Request(true);Request(false);
Check(npc.party==null && npc.currentZone==EClass._zone,"already-home dismissal stays on map");
Request(false);Check(host.Sent.Last() is PartyMemberDelta{Joined:false},"stale local membership receives correction");
npc.HomeMember=false;npc.homeZone=home;Request(false);
Check(npc.currentZone==EClass._zone,"stale-dismissal recovery cannot send unrelated townsfolk away");
npc.HomeMember=true;npc.homeZone=EClass._zone;
npc.memberType=FactionMemberType.Livestock;Request(true);Check(npc.party==null,"livestock rejected");
npc.memberType=FactionMemberType.Default;npc.HomeMember=false;Request(true);Check(npc.party==null,"outsiders rejected");
npc.HomeMember=true;npc.trait.CanJoinParty=false;Request(true);Check(npc.party==null,"trait veto retained");
npc.trait.CanJoinParty=true;clientActor.Distance=4;Request(true);Check(npc.party==null,"remote map-range request rejected");
clientActor.Distance=1;Request(true);npc.isSummon=true;Request(false);Check(npc.party==party,"summon cannot be dismissed by resident command");
npc.isSummon=false;npc.host=hostActor;Request(false);Check(npc.party==party,"ridden companion protected");
npc.host=null;npc.IsPlayer=true;Request(false);Check(npc.party==party,"human protected");npc.IsPlayer=false;
host.Accept=false;Request(false);Check(npc.party==party,"blocked peer cannot mutate party");host.Accept=true;
npc.homeZone=home;npc.ThrowOnMove=true;
try{Request(false);throw new Exception("expected move failure");}catch(InvalidOperationException){}
Check(EClass.pc==hostActor,"restores actor even when native move throws");npc.ThrowOnMove=false;

// Host native join emits a result; client landing never sends a request back.
party.AddMemeber(npc);PartyJoinEvent.After(party,npc);
Check(host.Delta.Sent.Last() is PartyMemberDelta{Joined:true},"host join broadcast");
party.RemoveMember(npc);var client=new ElinNetClient();NetSession.Instance.Connection=client;
new PartyMemberDelta{Member=npc,Joined=true}.Apply(client);
Check(npc.party==party,"host join lands on client");
PartyJoinEvent.After(party,npc);Check(client.Delta.Sent.Count==0,"no client result echo");
new PartyMemberDelta{Member=npc,DestZoneUid=8,DestPos=new(){X=4,Z=5}}.Apply(client);
Check(npc.party==null && npc.currentZone==home && npc.Removed && npc.pos.X==4,"dismissal lands with destination");

// Both native dialogue actions are replaced, including the premature MoveZone.
bool nativeRan=false;
var drama=new DramaCustomSequence();
drama.events.Add(new(){step="_joinParty"});drama.events.Add(new DramaEventMethod{action=()=>nativeRan=true});
drama.events.Add(new(){step="_leaveParty"});drama.events.Add(new DramaEventMethod{action=()=>nativeRan=true});
PartyDialoguePatch.After(drama,npc);
((DramaEventMethod)drama.events[1]).action();((DramaEventMethod)drama.events[3]).action();
Check(!nativeRan,"client never mutates membership or moves zone speculatively");
Check(client.Delta.Sent[0] is PartyCommandDelta{Join:true} && client.Delta.Sent[1] is PartyCommandDelta{Join:false},"both dialogue commands routed");
Check(drama.Jump==drama.StepEnd,"no premature hired response");
NetSession.Instance.Connection=host;
var hostDrama=new DramaCustomSequence();
hostDrama.events.Add(new(){step="_joinParty"});hostDrama.events.Add(new DramaEventMethod{action=()=>nativeRan=true});
PartyDialoguePatch.After(hostDrama,npc);((DramaEventMethod)hostDrama.events[1]).action();
Check(nativeRan,"host keeps native dialogue eligibility and effects");
nativeRan=false;NetSession.Instance.Connection=null;
PartyDialoguePatch.After(hostDrama,npc);((DramaEventMethod)hostDrama.events[1]).action();
Check(nativeRan,"solo dialogue unchanged");
Console.WriteLine($"{passed} party membership checks passed");
