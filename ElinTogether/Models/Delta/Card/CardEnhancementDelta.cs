using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

// Absolute enhancement, not another repair/enchantment attempt. Native SetEncLv
// updates material bonuses and linked equipment stats on the existing item.
[MessagePackObject]
public sealed class CardEnhancementDelta : ElinDelta
{
    [Key(0)] public required RemoteCard Card { get; init; }
    [Key(1)] public int Level { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || Card.Find() is not Thing { isDestroyed: false } item ||
            !item.IsEquipmentOrRangedOrAmmo) return;
        var previous = item.encLV;
        if (previous != Level) item.SetEncLv(Level);
        LayerInventory.SetDirty(item);
        EmpLog.Debug("Equipment enhancement applied: item {Uid}, level {Before}->{After}",
            item.uid, previous, item.encLV);
    }
}
