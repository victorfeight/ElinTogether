using System.Collections.Generic;
using ElinTogether.Helper.Extensions;

namespace ElinTogether.Models;

// Carried container presentation follows the character through the existing
// acknowledged profile checkpoint. Shared behavior remains on the host's item.
internal static class PersonalContainerWindows
{
    internal static Dictionary<int, Window.SaveData> Capture(Chara actor)
    {
        foreach (var layer in LayerInventory.listInv) {
            foreach (var inv in layer.invs) {
                var container = inv.owner.Container;
                if (container.GetRootCard() == actor &&
                    ReferenceEquals(inv.window.saveData, container.c_windowSaveData))
                    inv.window.UpdateSaveData();
            }
        }

        var result = new Dictionary<int, Window.SaveData>();
        foreach (var item in actor.things.Flatten()) {
            if (item.c_windowSaveData is { } data)
                result[item.uid] = IO.DeepCopy(data);
        }
        return result;
    }

    internal static void Restore(Chara actor, Dictionary<int, Window.SaveData>? layouts)
    {
        if (layouts is null) return; // Older profiles have no window snapshots.
        foreach (var item in actor.things.Flatten()) {
            if (!layouts.TryGetValue(item.uid, out var data)) continue;
            if (item.c_windowSaveData is { } saved) {
                ContainerWindowSettings.CopyLayout(saved, data);
            } else {
                // This container was only opened by its owner. Preserve the
                // native first-open defaults captured there, including size.
                item.c_windowSaveData = IO.DeepCopy(data);
                item.c_windowSaveData.open = false;
            }
        }
    }
}
