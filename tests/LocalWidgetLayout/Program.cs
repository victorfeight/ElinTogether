using System.Reflection;
using ElinTogether.Models;
using Newtonsoft.Json;

var folder = Path.Combine(Path.GetTempPath(), "emp-widgets-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
var path = Path.Combine(folder, "widgets.json");
int checks = 0;
void Check(bool pass, string why) { if (!pass) throw new Exception(why); checks++; }
void ForgetMemory() => typeof(LocalWidgetLayout).GetField("_snapshot", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, null);
WidgetManager.SaveData Layout(string id) => new() { dict = new() { [id] = new() { id = id } } };
Player Host() => new() { mainWidgets = Layout("host"), subWidgets = Layout("host-sub"), useSubWidgetTheme = true, Hotbar = 42 };
try {
    var first = Host();
    LocalWidgetLayout.Restore(first, path);
    Check(first.mainWidgets is null && first.subWidgets is null && !first.useSubWidgetTheme, "First join must request native local theme");
    Check(first.Hotbar == 42, "Do not reset hotbars");
    var local = new Player { mainWidgets = Layout("memo"), subWidgets = Layout("stats"), useSubWidgetTheme = true };
    LocalWidgetLayout.Capture(local, () => { local.mainWidgets!.dict!["memo"].x = 73; local.mainWidgets.dict["memo"].extra = new Memo { text = "local note" }; }, path);
    local.mainWidgets!.dict!["memo"].x = 999;
    var joined = Host();
    LocalWidgetLayout.Restore(joined, path);
    Check(joined.mainWidgets!.dict!["memo"].x == 73, "Capture updates live configs before snapshot and detaches references");
    Check(joined.subWidgets!.dict!.ContainsKey("stats") && joined.useSubWidgetTheme, "Both themes and selected theme survive");
    Check(joined.mainWidgets.dict["memo"].extra is Memo { text: "local note" }, "Typed widget extras survive");
    joined.mainWidgets.dict["memo"].x = 100;
    LocalWidgetLayout.Restore(joined, path);
    Check(joined.mainWidgets!.dict!["memo"].x == 73, "Every restoration has fresh config objects");
    ForgetMemory();
    LocalWidgetLayout.Restore(joined, path);
    Check(joined.mainWidgets!.dict!["memo"].x == 73, "Restart restores disk snapshot");
    LocalWidgetLayout.Capture(local, () => { }, path);
    ForgetMemory();
    LocalWidgetLayout.Restore(joined, path);
    Check(joined.mainWidgets!.dict!["memo"].x == 999, "Atomic replacement saves latest layout");
    File.WriteAllText(path, "broken json");
    ForgetMemory();
    LocalWidgetLayout.Restore(joined, path);
    Check(joined.mainWidgets is null && joined.subWidgets is null, "Corrupt backup falls back to local theme, not host");
    File.WriteAllText(path, "{\"Main\":{\"dict\":null}}");
    LocalWidgetLayout.Restore(joined, path);
    Check(joined.mainWidgets is null, "Reject malformed config before native initialization");
    var blocked = Path.Combine(folder, "file");
    File.WriteAllText(blocked, "not a directory");
    LocalWidgetLayout.Capture(local, () => { }, Path.Combine(blocked, "widgets.json"));
    LocalWidgetLayout.Restore(joined, path);
    Check(joined.mainWidgets!.dict!["memo"].x == 999, "Disk failure still preserves in-memory handoff");
    LocalWidgetLayout.Capture(local, () => throw new Exception("widget destroyed"), path);
    LocalWidgetLayout.Restore(joined, path);
    Check(joined.mainWidgets!.dict!["memo"].x == 999, "Failed capture does not discard last good layout");
    Console.WriteLine($"{checks} local widget layout checks passed");
} finally { Directory.Delete(folder, true); }

public class Memo { public string? text; }
public class Widget { public class Config { public string? id; public float x; public object? extra; } }
public class WidgetManager { public class SaveData { public Dictionary<string, Widget.Config>? dict; } public void UpdateConfigs() {} }
public class Player { public WidgetManager.SaveData? mainWidgets, subWidgets; public bool useSubWidgetTheme; public int Hotbar; public WidgetManager.SaveData? widgets => useSubWidgetTheme ? subWidgets : mainWidgets; }
public class Core { public bool IsGameStarted; }
public class Game { public bool isKilling; }
public class UI { public WidgetManager widgets = new(); }
public static class EClass { public static Core? core; public static Game? game; public static Player? player; public static UI ui = new(); }
public static class CorePath { public static string PathBackup = Path.GetTempPath(); }
public static class GameIOContext { public static JsonSerializerSettings Settings = new() { PreserveReferencesHandling = PreserveReferencesHandling.Objects, TypeNameHandling = TypeNameHandling.Auto, ObjectCreationHandling = ObjectCreationHandling.Replace }; }
public static class EmpLog { public static void Warning(Exception ex, string message) {} }
