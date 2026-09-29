namespace ElinTogether.Helper;

internal static class RemoteHeldItem
{
    // Replicate selection of an existing inventory item, not a pickup. Vanilla
    // HoldCard/PickHeld can stack, drop overflow and recreate the item renderer.
    internal static bool TrySelect(Chara owner, Card? item)
    {
        if (owner.IsPC || (item is not null &&
            (item is not Thing || item.isDestroyed || item.Num <= 0 || item.GetRootCard() != owner))) {
            return false;
        }

        if (owner.held == item) return true;

        var refreshLight = (owner.held?.GetLightRadius() ?? 0) > 0 || (item?.GetLightRadius() ?? 0) > 0;
        owner.held = item;
        if (refreshLight) owner.RecalculateFOV();
        return true;
    }
}
