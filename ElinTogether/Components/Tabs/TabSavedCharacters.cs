using System;
using System.IO;
using ElinTogether.Models;
using UnityEngine;
using UnityEngine.UI;
using YKF;

namespace ElinTogether.Components;

internal class TabSavedCharacters : TabEmpBase
{
    public override void OnLayout()
    {
        Header("Saved characters");
        TextSmall("Play as another saved character or bring them along as an AI companion. Switching and recruitment back up the world first.");
        TextSmall("World progress stays shared. Companions keep their skill and equipment changes and are dismissed before multiplayer starts.");
        Spacer(8);
        foreach (var actor in SoloCharacterSelection.Candidates()) {
            var status = actor.IsPC ? "Playing" : SoloCompanions.IsCompanion(actor) ? "AI companion" : "Dormant";
            Header($"{actor.NameSimple} — Lv {actor.LV}");
            TextSmall(status);
            var row = Horizontal();
            row.Layout.spacing = 8;
            FitButton(row, "Export JSON", () => Run(() => {
                var folder = SoloCharacterSelection.Export(actor);
                Application.OpenURL(new Uri(folder + Path.DirectorySeparatorChar).AbsoluteUri);
            }));
            Spacer(12);
            if (actor.IsPC) continue;
            if (SoloCompanions.IsCompanion(actor)) {
                var dismissReason = SoloCompanions.CannotDismiss(actor);
                if (dismissReason is null)
                    FitButton(row, "Dismiss companion", () => Run(() => {
                        SoloCompanions.Dismiss(actor);
                        EClass.ui.RemoveLayer(LayerElinTogether.Instance);
                    }));
                else TextSmall(dismissReason);
            } else if (SoloCompanions.CannotRecruit(actor) is null) {
                FitButton(row, "Bring as companion", () => Dialog.YesNo(
                    $"Save and back up, then bring {actor.NameSimple} as an AI companion? They use their real equipment and can be injured or die under normal companion rules.",
                    () => Run(() => {
                        EClass.ui.RemoveLayer(LayerElinTogether.Instance);
                        EClass.core.actionsNextFrame.Add(() => Run(() => SoloCompanions.Recruit(actor)));
                    }), () => { }));
            }
            var reason = SoloCharacterSelection.CannotSelect(actor);
            if (reason is not null) { TextSmall(reason); continue; }
            FitButton(row, "Play solo", () => {
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

    private static void FitButton(YKLayout row, string text, Action action)
    {
        var button = row.Button(text, action);
        // YKF only sets minWidth=80. Measure the actual font/label instead of
        // guessing a fixed width, and stop the horizontal layout shrinking it.
        var width = Mathf.Ceil(button.mainText.preferredWidth + 32f);
        var layout = button.GetComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = Mathf.Max(80f, width);
        layout.flexibleWidth = 0;
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
