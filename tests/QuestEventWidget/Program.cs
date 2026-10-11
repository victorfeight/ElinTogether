using ElinTogether.Models;
using ElinTogether.Patches;
using ElinTogether.Net;
using Mono.Cecil;

int checks = 0;
void Check(bool ok, string why) { if (!ok) throw new Exception(why); Console.WriteLine("PASS " + why); checks++; }
using var native = ModuleDefinition.ReadModule(args[0]);
var refresh = native.Types.Single(t => t.Name == "WidgetDate").Methods.Single(m => m.Name == "_Refresh");
foreach (var owner in new[] { "Zone", "ZoneEvent" })
    Check(refresh.Body.Instructions.Count(i => i.Operand is MethodReference m && m.Name == "get_TextWidgetDate" && m.DeclaringType.Name == owner) == 1,
        $"installed date widget has one {owner} text call site");
using var timer = ModuleDefinition.ReadModule(args[1]);
foreach (var (type, field) in new[] { ("Harvest", "minElapsed"), ("Music", "minElapsed"), ("DefenseGame", "rounds") }) {
    var method = timer.Types.Single(t => t.Name == $"KK_ZoneEvent{type}_TextWidgetDate_Postfix").Methods.Single(m => m.Name == "TextWidgetDatePostfix");
    Check(method.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == field), $"installed KK timer {type} reads {field}");
}
using var expansion = ModuleDefinition.ReadModule(args[2]);
var monsterProgress = expansion.Types.Single(t => t.Name == "QuestMusicMonster").Methods.Single(m => m.Name == "GetTextProgress");
Check(monsterProgress.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "GetEvent"), "installed monster concert progress reads the local live zone event");
Check(monsterProgress.Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "get_GetVoltageString"), "installed concert progress dereferences the event mood");
var zone = EClass._zone;
var harvest = new ZoneEventQuest { Text = " harvest <color=cyan>Time left: 120</color>" };
var other = new ZoneEvent { Text = " weather event" };
zone.events.list.AddRange([harvest, other]);
var text = QuestEventWidgetSync.Capture();
Check(text == harvest.Text, "capture preserves host mod markup and excludes unrelated events");
Check(QuestEventWidgetSync.EventText(harvest) == harvest.Text && QuestEventWidgetSync.ZoneText(zone) == "zone", "host widget remains native");
NetSession.Instance.IsClient = true;
zone.events.list.Clear();
QuestEventWidgetSync.Apply(zone.uid, text);
Check(QuestEventWidgetSync.ZoneText(zone) == "zone" + text, "client without a quest event gets the host timer");
Check(zone.events.list.Count == 0, "display sync never adds executable quest events");
Check(QuestEventWidgetSync.EventText(harvest) == "", "existing client event cannot duplicate the host timer");
Check(QuestEventWidgetSync.EventText(other) == other.Text, "unrelated event display retained");
QuestEventWidgetSync.Apply(zone.uid + 1, "wrong zone");
Check(QuestEventWidgetSync.ZoneText(zone) == "zone" + text, "late snapshot for another zone ignored");
QuestEventWidgetSync.Apply(zone.uid, " war Retreat: 20 Boss wave");
Check(QuestEventWidgetSync.ZoneText(zone).Contains("Retreat: 20"), "updated host war state replaces prior display");
QuestEventWidgetSync.Apply(zone.uid, "");
Check(QuestEventWidgetSync.ZoneText(zone) == "zone" && QuestEventWidgetSync.EventText(harvest) == "", "host removing quest events clears stale client text");
QuestEventWidgetSync.Reset();
Check(QuestEventWidgetSync.ZoneText(zone) == "zone", "session/zone reset clears cached text");
Check(QuestEventWidgetSync.EventText(harvest) == harvest.Text, "before first snapshot a valid local event can still render");
harvest.quest = null;
Check(QuestEventWidgetSync.EventText(harvest) == "", "event without its quest cannot dereference a missing quest for display");
zone.events.list.Add(harvest);
Check(QuestEventWidgetSync.Capture() == "", "completed/removed quests skipped during host capture");
NetSession.Instance.IsClient = false;
QuestEventWidgetSync.Apply(zone.uid, "client injection");
Check(QuestEventWidgetSync.ZoneText(zone) == "zone", "host ignores incoming display state");
// Instance progress must never evaluate a missing client-side quest event.
var concert = new Quest { uid = 42, Text = "Audience: delighted" };
harvest.quest = concert;
var progress = QuestEventWidgetSync.CaptureProgress();
Check(progress[42] == concert.Text, "host captures instance progress using native/mod quest code");
string rendered = "";
Check(QuestInstanceProgressPatch.Before(concert, ref rendered), "host keeps native quest progress");
NetSession.Instance.IsClient = true;
QuestEventWidgetSync.Reset();
Check(!QuestInstanceProgressPatch.Before(concert, ref rendered) && rendered == "", "transition before event snapshot safely renders empty progress");
zone.events.list.Clear();
QuestEventWidgetSync.Apply(zone.uid, " concert timer", progress);
Check(!QuestInstanceProgressPatch.Before(concert, ref rendered) && rendered == concert.Text, "client with no live event renders host concert mood");
Check(concert.Reads == 1, "client rendering does not invoke native event-dependent quest code");
Check(QuestInstanceProgressPatch.Before(new Quest { UseInstanceZone = false }, ref rendered), "ordinary quests keep native progress");
Check(QuestEventWidgetSync.ProgressText(new Quest { uid = 43 }) == "", "another quest cannot inherit concert progress");
zone.uid++;
Check(QuestEventWidgetSync.ProgressText(concert) == "", "leaving instance hides stale progress");
QuestEventWidgetSync.Apply(zone.uid, "", new());
Check(QuestEventWidgetSync.ProgressText(concert) == "", "ended instance clears progress");
QuestEventWidgetSync.Reset();
Check(QuestEventWidgetSync.ProgressText(concert) == "", "session reset clears progress");
Console.WriteLine($"{checks} widget checks passed; native/mod IL checked, rendering boundaries simulated.");

public class EClass { public static Zone _zone = new(); }
public class Zone { public int uid = 10; public string TextWidgetDate => "zone"; public ZoneEventManager events = new(); }
public class ZoneEventManager { public List<ZoneEvent> list = []; }
public class ZoneEvent { public string Text = ""; public virtual string TextWidgetDate => Text; }
public class ZoneEventQuest : ZoneEvent { public Quest? quest = new(); public override string TextWidgetDate => quest == null ? throw new InvalidOperationException() : Text; }
public class Quest { public int uid; public bool UseInstanceZone = true; public string Text = ""; public int Reads; public string GetTextProgress() { Reads++; return Text; } }
namespace ElinTogether.Net { public class ElinNetClient { } public class NetSession { public static NetSession Instance = new(); public bool IsClient; public object? Connection => IsClient ? new ElinNetClient() : null; } }

namespace ElinTogether.Helper { public static class OverrideMethodComparer { public static IEnumerable<System.Reflection.MethodBase> FindAllOverrides(Type type, string name) => []; } }
