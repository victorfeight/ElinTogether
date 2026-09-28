using ElinTogether.Models;
using ElinTogether.Net;
using ElinTogether.Patches;

int checks = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); checks++; }
var host = new ElinNetHost();
var client = new ElinNetClient();
var hostGame = new Game();
var clientGame = new Game();
EClass.game = hostGame;
NetSession.Instance.Connection = host;
GuildStateSnapshot.Reset();
var thiefTrial = new QuestGuild { id = "guild_thief", uid = 100, phase = 0, task = new() { num = -2 } };
thiefTrial.task.SetOwner(thiefTrial);
hostGame.quests.list.Add(thiefTrial);
Guild.Thief.relation.affinity = 42;
var initial = GuildStateSnapshot.Capture();
Check(ReferenceEquals(initial, GuildStateSnapshot.Capture()), "unchanged state is cached instead of serialized every tick");
EClass.game = clientGame;
NetSession.Instance.Connection = client;
var existing = new QuestGuild { id = "guild_thief", uid = 100, phase = 0, task = new() { num = 0 }, track = true };
clientGame.quests.list.Add(existing);
var unrelated = new Quest { uid = 5, id = "random" };
clientGame.quests.list.Add(unrelated);
initial.Apply();
Check(existing.task!.num == -2 && existing.task.owner == existing && clientGame.quests.list.Contains(existing), "replica updates trial count in place and rebinds task owner");
Check(existing.track && clientGame.quests.list.Contains(unrelated) && Guild.Thief.relation.affinity == 42, "local tracker preference and unrelated quest survive reconciliation");
Check(!GuildQuestProgressPatch.Before(existing) && GuildQuestProgressPatch.Before(unrelated), "client replay cannot progress shared guild trial but ordinary quests are unaffected");
initial.Apply();
Check(existing.task.num == -2, "repeated snapshot cannot add progress twice");
var retainedTask = existing.task;
var decoded = LZ4Bytes.DecodeCount;
Guild.Thief.relation.type = FactionRelation.RelationType.Member;
Guild.Thief.relation.rank = 4;
Guild.Thief.relation.exp = 500;
Guild.Thief.relation.affinity = -10;
initial.Apply();
Check(Guild.Thief.relation.type == FactionRelation.RelationType.Default && Guild.Thief.relation.rank == 0 &&
      Guild.Thief.relation.exp == 0 && Guild.Thief.relation.affinity == 42,
    "same revision repairs client membership, rank, contribution and affinity drift");
Check(LZ4Bytes.DecodeCount == decoded && ReferenceEquals(retainedTask, existing.task) && existing.track,
    "same revision does not deserialize quests or replace task/UI references");
foreach (var connection in new ElinNetBase?[] { host, null }) {
    NetSession.Instance.Connection = connection;
    Guild.Thief.relation.exp = 123;
    initial.Apply();
    Check(Guild.Thief.relation.exp == 123 && LZ4Bytes.DecodeCount == decoded,
        "relation reconciliation cannot mutate host or solo state");
}
EClass.game = hostGame; NetSession.Instance.Connection = host;
Guild.Thief.relation.exp = 17;
thiefTrial.CompleteTask();
var completed = GuildStateSnapshot.Capture();
Check(completed.Revision > initial.Revision, "phase mutation advances shared revision");
EClass.game = clientGame; NetSession.Instance.Connection = client;
completed.Apply(); initial.Apply();
Check(existing.phase == 1 && existing.task == null, "completed phase and removed task arrive together; stale snapshot cannot undo them");
Check(Guild.Thief.relation.exp == 17, "older revision cannot roll back newer contribution");
Guild.Thief.relation.exp = 999;
initial.Apply();
Check(Guild.Thief.relation.exp == 999, "older revision is rejected even when client state has drifted");
completed.Apply();
Check(Guild.Thief.relation.exp == 17, "current revision repairs drift after stale revision was rejected");
var missing = new QuestGuild { id = "guild_mage", uid = 101, phase = 0 };
QuestReplica.Apply(missing, false);
Check(clientGame.quests.list.Contains(missing), "missing quest is safely inserted");
var order = new QuestGuildOrder { id = "guild_order_thief", uid = 102 };
Check(GuildStateSnapshot.IsGuildQuest(order), "guild orders use same synchronization boundary");

EClass.game = hostGame; NetSession.Instance.Connection = host;
var actor = new Chara { uid = 719 };
var other = new Chara { uid = 720 };
host.ActiveRemoteCharas[1] = actor;
host.ActiveRemoteCharas[2] = other;
Chara Npc(string guild, string role = "doorman") {
    var npc = new Chara { id = "guild_" + role + "_" + guild };
    if (role == "doorman") npc.trait = new TraitGuildDoorman { owner = npc, GuildId = guild };
    return npc;
}
GuildActionDelta Request(Guild guild, Chara npc, GuildActionDelta.Operation op, int peer = 1) => new() {
    GuildId = guild.id, Npc = npc, Action = op, OriginPeer = peer,
    ExpectedPhase = guild.Quest?.phase ?? -1, ExpectedRank = guild.relation.rank,
};
var doorman = Npc("thief");
var join = Request(Guild.Thief, doorman, GuildActionDelta.Operation.Join);
join.Apply(host); join.Apply(host);
Check(Guild.Thief.IsMember && thiefTrial.phase == 10 && ((TraitGuildDoorman)doorman.trait).JoinCalls == 1, "validated join executes once and retains vanilla doorman side effects");
var clerk = Npc("thief", "clerk");
Guild.Thief.relation.exp = 150;
var promote = Request(Guild.Thief, clerk, GuildActionDelta.Operation.Promote);
var concurrent = Request(Guild.Thief, clerk, GuildActionDelta.Operation.Promote, 2);
promote.Apply(host); promote.Apply(host); concurrent.Apply(host);
Check(Guild.Thief.relation.rank == 1 && Guild.Thief.relation.exp == 50 && Guild.Thief.DevelopmentRefreshes == 1, "promotion charges contribution once and rejects stale concurrent request");
Request(Guild.Thief, clerk, GuildActionDelta.Operation.Promote).Apply(host);
Check(Guild.Thief.relation.rank == 1, "insufficient contribution cannot promote");
var mageDoorman = Npc("mage");
Request(Guild.Mage, mageDoorman, GuildActionDelta.Operation.Trial).Apply(host);
Check(Guild.Mage.Quest?.phase == 0, "client can request the real vanilla guild trial");
Request(Guild.Mage, mageDoorman, GuildActionDelta.Operation.MageLetter).Apply(host);
Check(!Guild.Mage.IsMember, "letter hand-in requires acting player's actual inventory item");
var letter = new Thing { id = "letter_trial", Num = 1 };
actor.things.list.Add(letter);
var handIn = Request(Guild.Mage, mageDoorman, GuildActionDelta.Operation.MageLetter);
handIn.Apply(host); handIn.Apply(host);
Check(Guild.Mage.IsMember && Guild.Mage.Quest!.phase == 10 && letter.Num == 0, "letter consumed once and vanilla automatic admission completed atomically");
var fighterDoorman = Npc("fighter");
actor.Distance = 20;
Request(Guild.Fighter, fighterDoorman, GuildActionDelta.Operation.Trial).Apply(host);
actor.Distance = 1;
Check(Guild.Fighter.Quest == null, "remote player cannot start trial while far from representative");
Request(Guild.Fighter, doorman, GuildActionDelta.Operation.Trial).Apply(host);
Check(Guild.Fighter.Quest == null, "wrong guild representative rejected");
Request(Guild.Fighter, fighterDoorman, GuildActionDelta.Operation.Trial, 99).Apply(host);
Check(Guild.Fighter.Quest == null, "unregistered peer rejected");
Request(Guild.Fighter, fighterDoorman, GuildActionDelta.Operation.Trial).Apply(host);
Request(Guild.Fighter, fighterDoorman, GuildActionDelta.Operation.Join).Apply(host);
Check(!Guild.Fighter.IsMember, "unfinished trial cannot grant membership");
var karmaTrial = new QuestGuild { id = "guild_thief_test", uid = 777, task = new() { num = -1 } };
hostGame.quests.list.Add(karmaTrial);
RemoteGuildBookPatch.After(new TraitBaseSpellbook { BookType = TraitBaseSpellbook.Type.Dojin }, actor);
Check(karmaTrial.task.num == -2, "remote Dojin completion credits shared karma trial on host");
hostGame.quests.list.Remove(karmaTrial);
Guild.Mage.relation.exp = 0;
var book = new TraitBaseSpellbook { BookType = TraitBaseSpellbook.Type.Ancient, owner = new Card { refVal = 4 } };
RemoteGuildBookPatch.After(book, actor);
Check(Guild.Mage.relation.exp == 13, "host credits remote ancient-book completion once using vanilla formula");
NetSession.Instance.Connection = client;
RemoteGuildBookPatch.After(book, actor);
ElinDelta.IsApplying = true;
Check(!GuildContributionReplayPatch.Before(Guild.Mage) && Guild.Mage.relation.exp == 13, "client replay cannot duplicate guild contribution");
ElinDelta.IsApplying = false;
NetSession.Instance.Connection = null;
Check(GuildQuestProgressPatch.Before(thiefTrial) && GuildContributionReplayPatch.Before(Guild.Thief), "solo guild gameplay is unchanged");

NetSession.Instance.Connection = host;
hostGame.quests.list.Remove(thiefTrial);
hostGame.quests.completedIDs.Add("guild_thief");
hostGame.quests.completedTypes.Add("QuestGuildThief");
GuildStateSnapshot.MarkDirty();
var removed = GuildStateSnapshot.Capture();
EClass.game = clientGame; NetSession.Instance.Connection = client;
clientGame.quests.completedIDs.Add("random_done");
removed.Apply();
Check(!clientGame.quests.list.Contains(existing) && clientGame.quests.completedIDs.Contains("guild_thief") && clientGame.quests.completedIDs.Contains("random_done"), "completed guild state removes stale quest while retaining unrelated completion history");
GuildStateSnapshot.Reset(); initial.Apply();
Check(clientGame.quests.list.Any(q => q.uid == 100), "new session accepts its own revision sequence");
Console.WriteLine($"{checks} checks passed");
