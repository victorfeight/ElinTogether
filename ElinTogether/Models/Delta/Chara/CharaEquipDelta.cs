using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CharaEquipDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Owner { get; init; }

    [Key(1)]
    public required RemoteCard Thing { get; init; }

    [Key(2)]
    public required int SlotIndex { get; init; }

    [Key(3)]
    public required int SlotElementId { get; init; }

    [Key(4)]
    public required bool Equip { get; init; }

    [Key(5)] public RelicEquipResult? Relic { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (Owner.Find() is not Chara chara || Thing.Find() is not Thing { isDestroyed: false } thing) {
            EmpLog.Warning("Dropping {DeltaType} from peer {PeerIndex}, owner {OwnerUid} or thing {Uid} unresolved",
                nameof(CharaEquipDelta), OriginPeer, Owner.Uid, Thing.Uid);
            return;
        }

        // client only equips self
        if (net is ElinNetHost host &&
            (!host.AcceptsPlayerInput(OriginPeer) || !host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var sender) || sender != chara)) {
            EmpLog.Warning("Refusing {DeltaType} from peer {PeerIndex}, owner {OwnerUid} is not the sender",
                nameof(CharaEquipDelta), OriginPeer, chara.uid);
            return;
        }

        if (net.IsHost && Relic == null) net.Delta.AddRemote(this);

        if (chara.IsPC) {
            return;
        }

        if (Relic is { } relic) {
            if (!RelicEquipment.Valid(relic) || RelicEquipment.HasCompleted(chara, relic)) return;
            if (net.IsHost && thing.GetRootCard() != chara) return;
            if (relic.HasSlot) RelicEquipment.EnsureSlot(chara);
        }

        if (Relic == null && Equip && thing.c_equippedSlot == SlotIndex + 1) {
            return;
        }

        if (Relic == null && !Equip && thing.c_equippedSlot == 0) {
            return;
        }

        var slots = chara.body.slots;
        var slot = SlotIndex >= 0 && SlotIndex < slots.Count && slots[SlotIndex].elementId == SlotElementId
            ? slots[SlotIndex]
            : null;

        if (slot is null) {
            slot = slots.Find(s => s.elementId == SlotElementId && s.thing is null) ??
                   slots.Find(s => s.elementId == SlotElementId);
            EmpLog.Warning("Slot index {SlotIndex} drifted on chara {OwnerUid}, fallback by element {ElementId}",
                SlotIndex, chara.uid, SlotElementId);
        }

        if (!Equip && slot?.thing != thing) slot = slots.Find(s => s.thing == thing);

        if (slot is null) {
            EmpLog.Warning("Dropping {DeltaType}, no slot of element {ElementId} on chara {OwnerUid}",
                nameof(CharaEquipDelta), SlotElementId, chara.uid);
            return;
        }

        bool ApplyEquipment()
        {
            if (Equip && slot.thing == thing) return true;
            if (Equip) return chara.body.Equip(thing, slot, false);
            // Never unequip an adjacent item after a slot layout change.
            if (slot.thing != thing) return false;
            chara.body.Unequip(slot);
            return true;
        }

        var success = Relic is { } result ? RelicEquipment.Replay(chara, thing, result, ApplyEquipment) : ApplyEquipment();
        if (!success) {
            EmpLog.Warning("Equipment replay rejected: owner {OwnerUid}, item {ThingUid}, slot {Slot}, relic {Relic}",
                chara.uid, thing.uid, SlotElementId, Relic != null);
            return;
        }
        if (net.IsHost && Relic != null) net.Delta.AddRemote(this);
    }
}
