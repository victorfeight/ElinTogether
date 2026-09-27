using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

// Authoritative embedded ammo/spell state; never replace the owning weapon.
[MessagePackObject]
public class CardAmmoDelta : ElinDelta
{
    [Key(0)] public required RemoteCard Card { get; init; }
    [Key(1)] public required LZ4Bytes? Ammo { get; init; }
    [Key(2)] public required int Count { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || Card.Find() is not Thing { isDestroyed: false } weapon) return;
        weapon.ammoData = Ammo?.Decompress<Thing>();
        weapon.c_ammo = Count;
        LayerInventory.SetDirty(weapon);
        EmpLog.Debug("Applied weapon spell state: uid {Uid}, spell {Spell}, charges {Charges}",
            weapon.uid, weapon.ammoData?.refVal ?? 0, Count);
    }
}
