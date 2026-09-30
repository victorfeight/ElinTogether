using System;
using System.IO;
using ElinTogether.Models;
using UnityEngine;

namespace ElinTogether.Components;

internal class TabSavedCharacters : TabEmpBase
{
    public override void OnLayout()
    {
        Header("Play a saved multiplayer character solo");
        Header("Switching saves and backs up this world, exports both characters to JSON, then reloads. You can switch back here.");
        Header("World quests, collections, recipes and finances stay with this world. Older saves may lack personal counters and UI history.");
        foreach (var actor in SoloCharacterSelection.Candidates()) {
            var row = Horizontal();
            row.Header($"{actor.NameSimple} — Lv {actor.LV}" + (actor.IsPC ? " (playing)" : ""));
            row.Button("Export JSON", () => Run(() => {
                var folder = SoloCharacterSelection.Export(actor);
                Application.OpenURL(new Uri(folder + Path.DirectorySeparatorChar).AbsoluteUri);
            }));
            if (actor.IsPC) continue;
            var reason = SoloCharacterSelection.CannotSelect(actor);
            if (reason is not null) { Header(reason); continue; }
            row.Button("Play solo", () => {
                var migration = SoloPlayerProfile.HasMissingHistory(actor)
                    ? "\nThis character is missing personal history from before profile tracking. Available counters and hotbars are retained; unknown history cannot be recovered. Inventory, equipment and skills are retained." : "";
                Dialog.YesNo($"Save, back up, and reload as {actor.NameSimple}?{migration}", () => Run(() => {
                    EClass.ui.RemoveLayer(LayerElinTogether.Instance);
                    // Defer until the dialog's click handler has finished closing.
                    EClass.core.actionsNextFrame.Add(() => Run(() => SoloCharacterSelection.Select(actor)));
                }), () => { });
            });
        }
    }

    private static void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) {
            EmpLog.Error(ex, "Saved character action failed");
            EClass.ui.Say(ex.Message);
        }
    }
}
