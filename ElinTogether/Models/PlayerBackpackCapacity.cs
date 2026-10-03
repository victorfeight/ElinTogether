using System.Collections.Generic;

namespace ElinTogether.Models;

internal static class PlayerBackpackCapacity
{
    // Same membership as vanilla PC ShouldShowOnGrid/IsOverflowing. Inventory
    // UI grids are optional, transient, and may have been built as an NPC grid.
    internal static bool UsesSlot(Thing item) => !item.isEquipped && item.invY != 1;

    internal static int Used(IEnumerable<Thing> items)
    {
        var count = 0;
        foreach (var item in items) if (UsesSlot(item)) count++;
        return count;
    }
}
