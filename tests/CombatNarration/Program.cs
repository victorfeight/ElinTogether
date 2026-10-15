using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;
using HarmonyLib;

int passed=0;
void Check(bool b,string name){if(!b)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
var host=new ElinNetHost(); var client=new ElinNetClient();
var attacker=new Chara{Name="Farris",Male=false};var victim=new Chara{Name="grudge",Male=false};
EClass.pc=attacker;
foreach(var id in new[]{"attackMelee","kill_attack","evade","plain"}) EClass.sources.langGame.map[id]=new(){group=id=="plain"?"other":"combat"};
Check(CombatNarrationMessagePatch.TargetMethods().Count()==6,"all six native Msg.Say overloads resolved");
Check(CombatNarrationScopePatch.TargetMethods().All(m=>m!=null),"attack, melee and damage scopes resolved");
var pair=AccessTools.Method(typeof(Msg),"Say",[typeof(string),typeof(Card),typeof(Card),typeof(string),typeof(string)]);
CombatText Capture(string id,string template,string text,string? verb=null){
 string result="";
 Check(CombatNarrationMessagePatch.Before(pair,[id,attacker,victim,verb!,null!],ref result,out var previous),"host native message runs");
 var pending=CombatNarrationMessagePatch.Pending!;
 CombatNarrationTextPatch.Template(template);
 CombatNarrationTextPatch.Capture(text,ref result);
 CombatNarrationMessagePatch.After(previous);
 return pending;
}
NetSession.Instance.Connection=host;
var outer=CombatNarration.Begin();
var intro=Capture("attackMelee","#1 #3 #2 and","you smash grudge and","smash(es)");
var inner=CombatNarration.Begin();
var ending=Capture("kill_attack","kill(s) #him2.","kill her.");
CombatNarration.End(inner);
Check(host.Delta.Sent.Count==0,"nested damage does not send an independent ending");
CombatNarration.End(outer);
var packet=(CombatNarrationDelta)host.Delta.Sent.Single();
Check(packet.Text.SequenceEqual(new[]{intro,ending}),"introduction and ending share one ordered packet");
Check(CombatNarration.Current==null&&CombatNarrationMessagePatch.Pending==null,"all capture state unwound");
Check(intro.Resolve()=="you smash grudge and"&&ending.Resolve()=="kill her.","attacker perspective uses you and singular verb");
EClass.pc=victim;
Check(intro.Resolve()=="Farris smashes you and"&&ending.Resolve()=="kills you.","recipient perspective resolves names, you and pronouns locally");
EClass.pc=new Chara{Name="Din"};
Check(intro.Resolve()=="Farris smashes grudge and"&&ending.Resolve()=="kills her.","observer perspective preserves native third-person grammar");
NetSession.Instance.Connection=client;
var prediction=CombatNarration.Begin();string ignored="";
Check(!CombatNarrationMessagePatch.Before(pair,["attackMelee",attacker,victim,"smash(es)",null!],ref ignored,out var prior),"client predicted introduction suppressed");
CombatNarrationMessagePatch.After(prior);
Check(!CombatNarrationMessagePatch.Before(pair,["evade",attacker,victim,null!,null!],ref ignored,out prior),"different client hit roll cannot emit a competing miss");
CombatNarrationMessagePatch.After(prior);
Check(!CombatNarrationMessagePatch.Before(pair,["kill_attack",attacker,victim,null!,null!],ref ignored,out prior),"separate damage replay ending suppressed");
CombatNarrationMessagePatch.After(prior);
Check(CombatNarrationMessagePatch.Before(pair,["plain",attacker,victim,null!,null!],ref ignored,out prior),"unrelated messages are not suppressed");
CombatNarrationMessagePatch.After(prior);CombatNarration.End(prediction);
Msg.Output.Clear();packet.Apply(client);
Check(Msg.Output.SequenceEqual(new[]{"<line>","Farris smashes grudge and","kills her.","<line>"}),"confirmed sentence rendered together with explicit boundaries");
Check(client.Delta.Sent.Count==0&&!CombatNarration.Rendering,"display does not echo packets or retain render state");
Msg.Output.Clear();packet.Apply(host);Check(Msg.Output.Count==0,"host rejects narration authored by clients");
NetSession.Instance.Connection=null;
Check(CombatNarration.Begin()==null,"solo gameplay has no narration scope");
Check(CombatNarrationMessagePatch.Before(pair,["kill_attack",attacker,victim,null!,null!],ref ignored,out prior),"solo message runs unchanged");
CombatNarrationMessagePatch.After(prior);
NetSession.Instance.Connection=host;
outer=CombatNarration.Begin();
try { inner=CombatNarration.Begin();try{Capture("kill_attack","kill(s) #him2.","kills her.");throw new InvalidOperationException();}finally{CombatNarration.End(inner);}}
catch(InvalidOperationException){}finally{CombatNarration.End(outer);}
Check(CombatNarration.Current==null&&host.Delta.Sent.Count==2,"exception cleanup restores nesting and preserves completed text");
outer=CombatNarration.Begin();Msg.ignoreAll=true;
Check(CombatNarrationMessagePatch.Before(pair,["kill_attack",attacker,victim,null!,null!],ref ignored,out prior)&&CombatNarrationMessagePatch.Pending==null,"native ignoreAll respected");
CombatNarrationMessagePatch.After(prior);Msg.ignoreAll=false;CombatNarration.End(outer);
Check(host.Delta.Sent.Count==2,"empty capture sends no packet");
outer=CombatNarration.Begin();attacker.isSynced=false;
CombatNarrationSpeakerPatch.Before(attacker,"attackMelee",out var speaker);
bool visible=false;CombatNarrationSpeakerPatch.Visibility.After(attacker,ref visible);
Check(visible,"host can capture combat outside its own visibility");
CombatNarrationMessagePatch.Before(pair,["attackMelee",attacker,victim,"smash(es)",null!],ref ignored,out prior);
CombatNarrationTextPatch.Template("#1 #3 #2 and");
Check(!CombatNarrationTextPatch.Capture("Farris smashes grudge and",ref ignored),"offscreen capture does not display on host");
var hidden=CombatNarrationMessagePatch.Pending!;
CombatNarrationMessagePatch.After(prior);CombatNarrationSpeakerPatch.After(speaker);CombatNarration.End(outer);
Msg.Output.Clear();hidden.Display();Check(Msg.Output.Count==0,"observer visibility filters offscreen narration");
attacker.isSynced=true;hidden.Display();Check(Msg.Output.Count==1,"client that sees the speaker receives narration");
ending.Second!.Value=null;Check(ending.Resolve()=="kill her.","removed target uses captured text without throwing");
var variants=new[]{
 new CombatText{Kind=CombatText.Format.Raw,Template="Critical hit!"},
 new CombatText{Kind=CombatText.Format.Strings,Template="#1 #2 #3 #4",Refs=["a","b","c","d"]},
 new CombatText{Kind=CombatText.Format.CardStrings,First=attacker,Template="#1 #2 #3 #4",Refs=["a","b","c"]},
 new CombatText{Kind=CombatText.Format.CardNumber,First=attacker,Number=4,Template="#1 #2 #3",Refs=["hits"]},
 new CombatText{Kind=CombatText.Format.Number,Number=4,Template="#1 #2",Refs=["four","hits"]},
};
Check(variants.Select(v=>v.Resolve()).SequenceEqual(new[]{"Critical hit!","a b c d","Farris a b c","Farris 4 hits","four hits"}),"remaining native overloads preserve argument order");
outer=CombatNarration.Begin();
var hits=new List<CombatText>();
for(int n=0;n<3;n++) {
 inner=CombatNarration.Begin();
 hits.Add(Capture("attackMelee","#1 #3 #2 and","intro "+n,"smash(es)"));
 hits.Add(Capture("kill_attack","kill(s) #him2.","ending "+n));
 CombatNarration.End(inner);
}
CombatNarration.End(outer);
Check(((CombatNarrationDelta)host.Delta.Sent.Last()).Text.SequenceEqual(hits),"multi-hit operation preserves each introduction/ending pair");
outer=CombatNarration.Begin();
CombatNarrationMessagePatch.Before(pair,["kill_attack",attacker,victim,null!,null!],ref ignored,out prior);
CombatNarrationTextPatch.Template("kill(s) #him2.");MsgRelayContext.IsSuppressing=true;
CombatNarrationTextPatch.Capture("kills her.",ref ignored);MsgRelayContext.IsSuppressing=false;
Check(outer!.Text.Count==0,"existing intentional message suppression is respected");
CombatNarrationMessagePatch.After(prior);CombatNarration.End(outer);
Console.WriteLine($"{passed} checks passed; simulated game boundary, not Unity acceptance.");
