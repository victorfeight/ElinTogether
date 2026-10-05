using ElinTogether.Models;
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
Console.WriteLine($"{checks} widget checks passed; native/mod IL checked, rendering boundaries simulated.");

public class EClass { public static Zone _zone = new(); }
public class Zone { public int uid = 10; public string TextWidgetDate => "zone"; public ZoneEventManager events = new(); }
public class ZoneEventManager { public List<ZoneEvent> list = []; }
public class ZoneEvent { public string Text = ""; public virtual string TextWidgetDate => Text; }
public class ZoneEventQuest : ZoneEvent { public object? quest = new(); public override string TextWidgetDate => quest == null ? throw new InvalidOperationException() : Text; }
namespace ElinTogether.Net { public class NetSession { public static NetSession Instance = new(); public bool IsClient; } }
