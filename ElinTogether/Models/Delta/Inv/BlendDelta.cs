using System;
using System.Collections.Generic;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class BlendRequestDelta : ElinDelta
{
    private static readonly HashSet<(int, Guid)> Completed = [];
    [ElinPreLoad] private static void Reset(GameIOContext _) => Completed.Clear();
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public int ZoneUid { get; init; }
    [Key(2)] public required RemoteCard Potion { get; init; }
    [Key(3)] public required RemoteCard Target { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is not ElinNetHost host || Id == Guid.Empty || !host.AcceptsPlayerInput(OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var actor) ||
            !Completed.Add((OriginPeer, Id))) return;
        var potion = Potion.Find() as Thing;
        var target = Target.Find() as Thing;
        if (ZoneUid != _zone.uid || !actor.IsInActiveMap || actor.IsDisabled ||
            potion is not { isDestroyed: false, Num: > 0, trait: TraitDrink drink } ||
            target is not { isDestroyed: false, Num: > 0 } || potion == target ||
            potion.GetRootCard() != actor || !CanTarget(actor, target) || !drink.CanBlend(target)) {
            host.SendDeltaTo(OriginPeer, new MsgSayDelta {
                Text = "The blend could not be applied. Check the mixture and target.", R = 1, G = 1, B = 1, A = 1,
            });
            EmpLog.Information("Blend rejected: peer {Peer}, potion {Potion}, target {Target}", OriginPeer, Potion.Uid, Target.Uid);
            return;
        }
        using var simulation = Simulate();
        using var messages = MsgRelayContext.RedirectTo(actor);
        var previous = player.chara;
        try {
            player.chara = actor;
            // Native UI execution handles coating, blessing, food splitting,
            // cursed mixtures and exactly one potion consumed.
            new InvOwnerBlend(potion)._OnProcess(target);
        } finally { player.chara = previous; }
    }

    private static bool CanTarget(Chara actor, Thing target)
    {
        var root = target.GetRootCard();
        return root == actor || root is Chara ally && ally.party is not null && ally.party == actor.party &&
            ally.IsInActiveMap && !ally.IsDisabled && actor.Dist(ally) <= 3;
    }
}

// Absolute property results on the existing item. Inventory ownership, splits,
// merges and potion consumption retain their existing host delta paths.
[MessagePackObject]
public sealed class BlendResultDelta : ElinDelta
{
    [Key(0)] public required RemoteCard Item { get; init; }
    [Key(1)] public bool Acidproof { get; init; }
    [Key(2)] public BlessedState Blessing { get; init; }
    [Key(3)] public int Poison { get; init; }
    [Key(4)] public int Love { get; init; }
    [Key(5)] public int Dream { get; init; }

    internal static BlendResultDelta Capture(Thing item) => new() {
        Item = item, Acidproof = item.isAcidproof, Blessing = item.blessedState,
        Poison = item.elements.Base(702), Love = item.elements.Base(703), Dream = item.elements.Base(704),
    };

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || Item.Find() is not Thing { isDestroyed: false } item) return;
        item.isAcidproof = Acidproof;
        if (item.blessedState != Blessing) item.SetBlessedState(Blessing);
        Set(702, Poison); Set(703, Love); Set(704, Dream);
        LayerInventory.SetDirty(item);
        EmpLog.Debug("Blend result applied: item {Item}, acidproof {Acidproof}, blessing {Blessing}", item.uid, Acidproof, Blessing);
        void Set(int id, int value) { if (item.elements.Base(id) != value) item.elements.SetBase(id, value); }
    }
}
