using System.Collections.Generic;

namespace ElinTogether.Models;

// Share container behavior, not another player's window position/appearance.
// Keep the native object alive: Window.OnKill/Player.OnBeforeSave write into it.
internal static class ContainerWindowSettings
{
    internal static void CopyLayout(Window.SaveData target, Window.SaveData source)
    {
        // Serialized integer coordinates avoid float -> int round-trip drift.
        foreach (var index in new[] { 1, 2, 3, 4, 5, 7, 8, 12, 13 })
            target.ints[index] = source.ints[index];
        target.useBG = source.useBG;
        target.noRightClickClose = source.noRightClickClose;
        target.fixedPos = source.fixedPos;
        target.shiftToShowMenu = source.shiftToShowMenu;
    }

    internal static void CopyShared(Window.SaveData target, Window.SaveData source)
    {
        target.autodump = source.autodump;
        target.sharedType = source.sharedType;
        target.priority = source.priority;
        target.flag = source.flag;
        target.category = source.category;
        target.sortMode = source.sortMode;
        target.excludeDump = source.excludeDump;
        target.excludeCraft = source.excludeCraft;
        target.compress = source.compress;
        target.advDistribution = source.advDistribution;
        target.noRotten = source.noRotten;
        target.onlyRottable = source.onlyRottable;
        target.alwaysSort = source.alwaysSort;
        target.sort_ascending = source.sort_ascending;
        target.cats = new HashSet<int>(source.cats);
        target.filter = source.filter;
        target._filterStrs = null;
    }
}
