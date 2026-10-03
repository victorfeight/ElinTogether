using System;
using System.IO;
using Newtonsoft.Json;

namespace ElinTogether.Models;

// Widget layout belongs to this installation, not the character or host world.
// Keep serialized data, never live widget references, across world replacement.
internal static class LocalWidgetLayout
{
    internal sealed class State
    {
        public WidgetManager.SaveData? Main;
        public WidgetManager.SaveData? Sub;
        public bool UseSub;
    }

    private static string? _snapshot;
    private static string BackupPath => Path.Combine(CorePath.PathBackup, "ElinTogether-ui", "widgets.json");

    internal static void CaptureLive()
    {
        if (EClass.core?.IsGameStarted != true || EClass.game?.isKilling != false ||
            EClass.player?.widgets?.dict is null) return;
        Capture(EClass.player, EClass.ui.widgets.UpdateConfigs, BackupPath);
    }

    internal static void Capture(Player player, Action updateConfigs, string path)
    {
        try {
            // Includes positions and widget-specific extras such as memo text.
            updateConfigs();
            var state = new State { Main = player.mainWidgets, Sub = player.subWidgets, UseSub = player.useSubWidgetTheme };
            Validate(state);
            _snapshot = JsonConvert.SerializeObject(state, GameIOContext.Settings);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, _snapshot);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        } catch (Exception ex) {
            // An unwritable backup must not interrupt disconnect or saving.
            EmpLog.Warning(ex, "Could not back up local widget layout");
        }
    }

    internal static void Restore(Player player) => Restore(player, BackupPath);

    internal static void Restore(Player player, string path)
    {
        State? state = null;
        try {
            var json = _snapshot ?? (File.Exists(path) ? File.ReadAllText(path) : null);
            if (json is not null) {
                state = JsonConvert.DeserializeObject<State>(json, GameIOContext.Settings)
                    ?? throw new InvalidDataException("Empty widget layout");
                Validate(state);
            }
        } catch (Exception ex) {
            state = null;
            EmpLog.Warning(ex, "Could not restore local widget layout; using configured local theme");
        }

        // Null lets vanilla load this computer's configured theme. Never fall
        // back to the host's populated layout, and never call WidgetManager.Reset:
        // Reset also changes the character's hotbars.
        player.mainWidgets = state?.Main;
        player.subWidgets = state?.Sub;
        player.useSubWidgetTheme = state?.UseSub ?? false;
    }

    private static void Validate(State state)
    {
        foreach (var layout in new[] { state.Main, state.Sub }) {
            if (layout is null) continue;
            if (layout.dict is null) throw new InvalidDataException("Widget layout has no dictionary");
            foreach (var config in layout.dict.Values)
                if (config is null) throw new InvalidDataException("Widget layout has an empty configuration");
        }
    }
}
