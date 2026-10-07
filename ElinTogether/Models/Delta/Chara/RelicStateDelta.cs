using System.Collections.Generic;
using System.Linq;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

// Only the new permanent slot and current relic locks. Never replace a body or
// replay DNA while synchronizing a story reward/sleep result.
[MessagePackObject]
public sealed class RelicStateDelta : ElinDelta
{
    [Key(0)] public required RemoteCard Owner { get; init; }
    [Key(1)] public bool HasSlot { get; init; }
    [Key(2)] public required Dictionary<int, bool> Locks { get; init; }

    internal static RelicStateDelta Capture(Chara actor) => new() {
        Owner = actor, HasSlot = actor.body.GetSlot(RelicEquipment.Slot, onlyEmpty: false) != null,
        Locks = actor.body.slots.Where(s => s.thing?.c_DNA != null)
            .Select(s => s.thing!).Distinct().ToDictionary(t => t.uid, t => t.GetBool(RelicEquipment.SleepLock)),
    };

    protected override void OnApply(ElinNetBase net)
    {
        if (Owner.Find() is not Chara actor || Locks == null) return;
        if (net is ElinNetHost host && (!host.AcceptsPlayerInput(OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var sender) || sender != actor)) return;
        if (actor.IsPC && !PlayerControl.WatchingOwnCharacter) return;
        if (HasSlot) RelicEquipment.EnsureSlot(actor);
        foreach (var slot in actor.body.slots) {
            if (slot.thing is not { c_DNA: not null } thing || !Locks.TryGetValue(thing.uid, out var locked)) continue;
            thing.SetBool(RelicEquipment.SleepLock, locked);
            LayerInventory.SetDirty(thing);
        }
        if (net.IsHost) net.Delta.AddRemote(this);
    }
}
