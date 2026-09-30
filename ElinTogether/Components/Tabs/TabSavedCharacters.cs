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
        Header("Offline companions use normal ally AI and keep equipment/skill changes. They return to dormant players before multiplayer starts.");
        foreach (var actor in SoloCharacterSelection.Candidates()) {
            var row = Horizontal();
            row.Header($"{actor.NameSimple} — Lv {actor.LV}" + (actor.IsPC ? " (playing)" : SoloCompanions.IsCompanion(actor) ? " (AI companion)" : ""));
            row.Button("Export JSON", () => Run(() => {
                var folder = SoloCharacterSelection.Export(actor);
                Application.OpenURL(new Uri(folder + Path.DirectorySeparatorChar).AbsoluteUri);
            }));
            if (actor.IsPC) continue;
            if (SoloCompanions.IsCompanion(actor)) {
                var dismissReason = SoloCompanions.CannotDismiss(actor);
                if (dismissReason is null)
                    row.Button("Dismiss companion", () => Run(() => {
                        SoloCompanions.Dismiss(actor);
                        EClass.ui.RemoveLayer(LayerElinTogether.Instance);
                    }));
                else Header(dismissReason);
            } else if (SoloCompanions.CannotRecruit(actor) is null) {
                row.Button("Bring as companion", () => Dialog.YesNo(
                    $"Save and back up, then bring {actor.NameSimple} as an AI companion? They use their real equipment and can be injured or die under normal companion rules.",
                    () => Run(() => {
                        EClass.ui.RemoveLayer(LayerElinTogether.Instance);
                        EClass.core.actionsNextFrame.Add(() => Run(() => SoloCompanions.Recruit(actor)));
                    }), () => { }));
            }
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
