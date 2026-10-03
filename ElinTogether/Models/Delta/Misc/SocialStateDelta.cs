using System;
using System.Collections.Generic;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class SocialTalkDelta : ElinDelta
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public int ZoneUid { get; init; }
    [Key(2)] public required RemoteCard Actor { get; init; }
    [Key(3)] public required RemoteCard Target { get; init; }
    [Key(4)] public bool KnowsFavourites { get; init; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host) SocialInteractions.Talk(host, OriginPeer, this);
    }
}

// Absolute results, never a second random affinity roll on the receiving peer.
[MessagePackObject]
public sealed class SocialStateDelta : ElinDelta
{
    private static readonly HashSet<Guid> Received = [];
    [ElinPreLoad] private static void Reset(GameIOContext _) => Received.Clear();
    [Key(0)] public required RemoteCard Target { get; init; }
    [Key(1)] public int AffinityValue { get; init; }
    [Key(2)] public int Interest { get; init; }
    [Key(3)] public string? FavouriteCategory { get; init; }
    [Key(4)] public string? FavouriteFood { get; init; }
    [Key(5)] public ElementChangeDelta[] Elements { get; set; } = [];
    [Key(6)] public Guid Id { get; init; }
    [Key(7)] public int ZoneUid { get; init; }
    [Key(8)] public SocialGiftTask[] Tasks { get; set; } = [];
    [Key(9)] public bool KnowsFavourites { get; init; }

    internal static SocialStateDelta Capture(Chara target, ElementChangeDelta[]? elements = null, SocialGiftTask[]? tasks = null) => new() {
        Target = target, AffinityValue = target._affinity, Interest = target.interest,
        FavouriteFood = target.GetStr(72), FavouriteCategory = target.GetStr(73),
        Elements = elements ?? [],
        Id = Guid.NewGuid(), ZoneUid = _zone.uid, Tasks = tasks ?? [], KnowsFavourites = target.knowFav,
    };

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || Id == Guid.Empty || Target.Find() is not Chara target || !Received.Add(Id)) return;
        SocialNotifications.ApplyAffinity(target, AffinityValue);
        target.interest = Interest;
        target.SetStr(72, FavouriteFood);
        target.SetStr(73, FavouriteCategory);
        target.knowFav = KnowsFavourites;
        // Generic element replication intentionally skips the local PC. These
        // host-resolved action awards must also reach that character's owner.
        foreach (var element in Elements) {
            if (element.Owner.Find() is not Chara owner) continue;
            element.Write(owner);
            element.RefreshDerived(owner);
        }
        if (ZoneUid == _zone.uid && !PlayerControl.LocalInputBlocked) {
            using var simulation = Simulate();
            foreach (var task in Tasks) task.Start();
        }
    }
}

[MessagePackObject]
public sealed class SocialGiftTask
{
    [Key(0)] public required RemoteCard Owner { get; init; }
    [Key(1)] public required RemoteCard Target { get; init; }
    [Key(2)] public bool ArmPillow { get; init; }
    internal void Start()
    {
        if (Owner.Find() is not Chara { IsPC: true } owner || Target.Find() is not Chara target) return;
        owner.SetAI(ArmPillow ? new AI_ArmPillow { target = target } : new AI_Massage { target = target });
    }
}
