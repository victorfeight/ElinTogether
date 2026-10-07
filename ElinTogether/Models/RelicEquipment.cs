using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Elements;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class RelicEquipResult
{
    [Key(0)] public Guid Id { get; init; }
    [Key(1)] public int BeforeFeat { get; init; }
    [Key(2)] public int AfterFeat { get; init; }
    [Key(3)] public bool Locked { get; init; }
    [Key(4)] public bool HasSlot { get; init; }
}

internal static class RelicEquipment
{
    internal const int Slot = 46;
    internal const int SleepLock = 135;
    private static Scope? _current;
    private static int _replayDepth;
    private static readonly HashSet<(int Owner, Guid Id)> Completed = [];

    [ElinPreLoad] private static void Reset(GameIOContext context)
    {
        _current = null; _replayDepth = 0; Completed.Clear();
    }

    internal sealed class Scope : IDisposable
    {
        internal readonly Chara Owner;
        internal readonly int BeforeFeat;
        private readonly Scope? _previous;
        internal Scope(Chara owner)
        {
            Owner = owner; BeforeFeat = owner.feat; _previous = _current; _current = this;
        }
        public void Dispose() => _current = _previous;
    }

    internal static bool Changing(Card? actor) => actor != null && _current?.Owner == actor;
    internal static bool Relevant(CharaBody body, Thing thing, BodySlot? slot) => thing.c_DNA != null ||
        (slot != null ? slot.thing?.c_DNA != null : body.slots.Any(s => s.elementId == thing.category.slot && s.thing?.c_DNA != null));

    internal static bool SkipTaskReplay(Chara actor) => _replayDepth == 0 &&
        NetSession.Instance.Connection != null && !actor.IsPC && actor.ai is GoalRemote;

    internal static RelicEquipResult? Capture(Chara owner, Thing thing) => Changing(owner) ? new() {
        Id = Guid.NewGuid(), BeforeFeat = _current!.BeforeFeat, AfterFeat = owner.feat,
        Locked = thing.GetBool(SleepLock), HasSlot = owner.body.GetSlot(Slot, onlyEmpty: false) != null,
    } : null;

    internal static bool HasCompleted(Chara actor, RelicEquipResult result) => Completed.Contains((actor.uid, result.Id));
    internal static bool Valid(RelicEquipResult result) => result.Id != Guid.Empty && result.BeforeFeat >= 0 && result.AfterFeat >= 0;

    // The original machine has already resolved this equipment operation. Native
    // replay owns DNA/slots/abilities; its final feat total belongs to the result,
    // not a competing CharaFeatPointDelta queued before that replay.
    internal static bool Replay(Chara actor, Thing thing, RelicEquipResult result, Func<bool> apply)
    {
        var previousFeat = actor.feat;
        var success = false;
        _replayDepth++;
        try {
            actor.feat = result.BeforeFeat;
            success = apply();
            if (!success) return false;
            actor.feat = result.AfterFeat;
            thing.SetBool(SleepLock, result.Locked);
            Completed.Add((actor.uid, result.Id));
            return true;
        } finally {
            if (!success) actor.feat = previousFeat;
            _replayDepth--;
        }
    }

    internal static void EnsureSlot(Chara actor)
    {
        if (actor.body.GetSlot(Slot, onlyEmpty: false) != null) return;
        actor.body.AddBodyPart(Slot);
        actor.body.RefreshBodyParts();
        if (actor.IsPC) WidgetEquip.OnChangeBodyPart();
    }

    internal static void PublishState(Chara actor)
    {
        if (ElinDelta.IsApplying || NetSession.Instance.Connection is not { } net ||
            (net.IsClient && !actor.IsPC)) return;
        net.Delta.AddRemote(RelicStateDelta.Capture(actor));
    }
}
