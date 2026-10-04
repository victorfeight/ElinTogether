using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

// State only: never rerun lock rolls, XP, karma, rewards or achievements on clients.
[MessagePackObject]
public sealed class CardLockStateDelta : ElinDelta
{
    [Key(0)] public required RemoteCard Card { get; init; }
    [Key(1)] public int LockLevel { get; init; }
    [Key(2)] public bool HardLock { get; init; }
    [Key(3)] public int PriceAdjustment { get; init; }
    [Key(4)] public bool LostProperty { get; init; }
    [Key(5)] public int Level { get; init; }

    internal static CardLockStateDelta Capture(Card card) => new() {
        Card = card, LockLevel = card.c_lockLv, HardLock = card.c_lockedHard,
        PriceAdjustment = card.c_priceAdd, LostProperty = card.isLostProperty, Level = card.LV,
    };

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || Card.Find() is not Thing { isDestroyed: false } item) return;
        var previous = item.c_lockLv;
        item.c_lockLv = LockLevel;
        item.c_lockedHard = HardLock;
        item.c_priceAdd = PriceAdjustment;
        item.isLostProperty = LostProperty;
        if (item.trait is TraitChestPractice) item.LV = Level;
        LayerInventory.SetDirty(item);
        EmpLog.Debug("Chest lock state applied: item {Uid}, lock {Before}->{After}, hard {Hard}, lost {Lost}",
            item.uid, previous, item.c_lockLv, HardLock, LostProperty);
    }
}
