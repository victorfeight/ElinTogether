using ElinTogether.Net;
using ElinTogether.Patches;
using Mono.Cecil;

int checks = 0;
void Check(bool ok, string why) { if (!ok) throw new Exception(why); Console.WriteLine("PASS " + why); checks++; }
using var native = ModuleDefinition.ReadModule(args.Single());
var selector = native.Types.Single(t => t.Name == "BaseTileSelector").Methods.Single(m => m.Name == "TryProcessTiles");
var calls = selector.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().Select(m => m.Name).ToList();
Check(calls.IndexOf("OnBeforeProcessTiles") < calls.IndexOf("ExecuteSummary"), "selector boundary precedes task creation and payment");
var undo = native.Types.Single(t => t.Name == "UndoManager").Methods.Single(m => m.Name == "Perform");
Check(undo.Body.Instructions.Any(i => i.Operand is MethodReference { Name: "Destroy" }), "native undo destroys tasks, requiring an input guard");
var destroy = native.Types.Single(t => t.Name == "TaskBuild").Methods.Single(m => m.Name == "OnDestroy");
Check(destroy.Body.Instructions.Any(i => i.Operand is MethodReference { Name: "AddCard" }), "cancelled native build tasks return items to the map");
foreach (var name in new[] { "AM_Build", "AM_Mine", "AM_Cut", "AM_Dig", "AM_Harvest", "AM_MoveInstalled" })
    Check(native.Types.Single(t => t.Name == name).BaseType.Name.StartsWith("AM_Designation"), name + " uses the guarded native designation family");

var client = new ElinNetClient();
NetSession.Instance.Connection = client;
foreach (var mode in new ActionMode[] { new CustomBuild(), new AM_CreateArea(), new AM_ExpandArea(), new AM_EditArea(), new AM_RemoveDesignation(), new AM_Deconstruct(), new AM_Blueprint() }) {
    var s = new BaseTileSelector { mode = mode, start = new() };
    Check(!AdvancedBuildingSelectionPatch.Before(s) && s.start == null, mode.GetType().Name + " rejected before native mutation");
}
Check(AdvancedBuildingSelectionPatch.Before(new()), "ordinary action mode untouched");
Check(!AdvancedBuildingUndoPatch.Before(), "client cannot refund cloned build resources through undo");
int edits = 0;
var entries = new List<BaseArea.Interaction> { new() { action = () => edits++ } };
AdvancedBuildingAreaMenuPatch.After(entries);
entries[0].action!();
Check(edits == 0, "client area menu cannot open mutation dialogs");
foreach (var connection in new object?[] { new object(), null }) {
    NetSession.Instance.Connection = connection;
    Check(AdvancedBuildingSelectionPatch.Before(new() { mode = new AM_Build() }) && AdvancedBuildingUndoPatch.Before(), "host/solo building and undo retained");
    entries[0].action!();
}
Check(edits == 2, "area menu checks current connection at click time");
Console.WriteLine($"{checks} checks passed");
