using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;

namespace ElinTogether.Models;

// Social actions keep vanilla's saved affinity field. This is not a new
// per-NPC/per-player relationship table: existing saves retain their meaning.
internal sealed class SocialInteractions : EClass
{
    private static readonly HashSet<(int Peer, Guid Id)> Completed = [];
    private static Context? _current;
    internal static bool Active => _current is not null;
    internal static bool ProtectGiftRecipient(Chara c) =>
        _current is { Gift: true, HumanRecipient: true } context && context.Target == c;

    // Host SetAI normally replaces human-client AI with GoalRemote. Retain a
    // native ticket's requested action so its human owner can start it instead.
    internal static void CaptureGiftTask(Chara owner, AIAct act)
    {
        if (_current is not { Gift: true } context || act is not AI_Massage massage ||
            NetSession.Instance.Connection is not ElinNetHost host ||
            !host.ActiveRemoteCharas.ContainsValue(owner) || host.IsCompanionControlled(owner)) return;
        context.Tasks[owner] = new SocialGiftTask {
            Owner = owner, Target = massage.target, ArmPillow = act is AI_ArmPillow,
        };
    }

    [ElinPreLoad]
    private static void Reset(GameIOContext _) { Completed.Clear(); _current = null; }

    internal static Context Begin(Chara actor, Chara target, bool gift = false) => new(actor, target, gift);

    private static bool Accept(ElinNetHost host, int peer, Guid id, int zone, RemoteCard owner,
        RemoteCard recipient, int distance, out Chara actor, out Chara target)
    {
        actor = null!;
        target = null!;
        if (id == Guid.Empty || !host.AcceptsPlayerInput(peer) || !Completed.Add((peer, id)) ||
            !host.ActiveRemoteCharas.TryGetValue(peer, out actor!) || actor.uid != owner.Uid ||
            recipient.Find() is not Chara found) return false;
        target = found;
        return zone == _zone.uid && actor != target && !actor.IsDisabled && !target.IsDisabled &&
            actor.IsInActiveMap && target.IsInActiveMap && actor.Dist(target) <= distance && !target.IsHostile(actor);
    }

    internal static void Give(ElinNetHost host, int peer, CharaGiveGiftDelta request)
    {
        if (!Accept(host, peer, request.Id, request.ZoneUid, request.From, request.To, 1, out var actor, out var target)) return;
        if (request.Thing.Find() is not Thing { isDestroyed: false } source ||
            source.GetRootCard() != actor || request.Thing.Num != 1 || source.Num < 1 ||
            !target.CanAcceptGift(actor, source) || !target.IsValidGiftWeight(source, 1)) {
            host.SendDeltaTo(peer, new MsgSayDelta { Text = "The gift could not be given. Check the item and the recipient's capacity.", R = 1, G = 1, B = 1, A = 1 });
            return;
        }
        using var simulation = ElinDelta.Simulate();
        using var context = Begin(actor, target, gift: true);
        var item = source.Split(1);
        try {
            actor.GiveGift(target, item);
        } finally {
            // A rejected flyer or another native early return can leave the
            // split detached. Return only a live, unowned remainder.
            if (!item.isDestroyed && item.parent is null) actor.AddThing(item);
            EmpLog.Information("Gift resolved: request {RequestId}, giver {GiverUid}, recipient {RecipientUid}, source {SourceUid}, item {ItemUid}, remaining {Remaining}, destroyed {Destroyed}",
                request.Id, actor.uid, target.uid, source.uid, item.uid, source.Num, item.isDestroyed);
        }
    }

    internal static void Talk(ElinNetHost host, int peer, SocialTalkDelta request)
    {
        if (!Accept(host, peer, request.Id, request.ZoneUid, request.Actor, request.Target, 3, out var actor, out var target)) return;
        using var simulation = ElinDelta.Simulate();
        // Native dialogue chooses and displays favourite disclosure locally.
        // Retain that knowledge when its validated conversation is committed.
        target.knowFav |= request.KnowsFavourites;
        Talk(actor, target);
    }

    internal static void Talk(Chara actor, Chara target)
    {
        using var context = Begin(actor, target);
        if (target.interest > 0 && (target.IsHumanSpeak || actor.HasElement(1640))) target.affinity.OnTalkRumor();
    }

    internal sealed class Context : IDisposable
    {
        internal readonly Chara Target;
        internal readonly bool Gift, HumanRecipient;
        internal readonly Dictionary<Chara, SocialGiftTask> Tasks = [];
        private readonly Chara _previousPlayer;
        private readonly Chara _previousAffinity;
        private readonly Context? _previousContext;
        private readonly Thing _previousUse;
        private readonly IDisposable _messages, _karma;
        private readonly Dictionary<Chara, Dictionary<int, ImmutableArray<int>>> _elements;
        private bool _disposed;

        internal Context(Chara actor, Chara target, bool gift)
        {
            Target = target; Gift = gift;
            _previousPlayer = pc;
            _previousAffinity = Affinity.CC;
            _previousContext = _current;
            _previousUse = target.nextUse;
            _elements = new[] { actor, target }.Distinct().ToDictionary(c => c,
                c => c.elements.dict.Values.ToDictionary(e => e.id, e => ElementChangeDelta.Create(c, e).Value));
            HumanRecipient = NetSession.Instance.Connection is ElinNetHost host &&
                (target == pc || host.ActiveRemoteCharas.ContainsValue(target)) && !host.IsCompanionControlled(target);
            _messages = MsgRelayContext.RedirectTo(actor);
            _karma = PersonalKarma.For(actor);
            _current = this;
            player.chara = actor;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try {
                if (Gift && HumanRecipient) Target.nextUse = _previousUse;
                if (NetSession.Instance.Connection is ElinNetHost host)
                    host.Delta.AddRemote(SocialStateDelta.Capture(Target, ChangedElements(), Tasks.Values.ToArray()));
            } finally {
                player.chara = _previousPlayer;
                Affinity.CC = _previousAffinity;
                _current = _previousContext;
                try { _karma.Dispose(); } finally { _messages.Dispose(); }
            }
        }

        private ElementChangeDelta[] ChangedElements() => _elements.SelectMany(pair =>
            pair.Value.Keys.Union(pair.Key.elements.dict.Keys).Select(id =>
                pair.Key.elements.dict.TryGetValue(id, out var element)
                    ? ElementChangeDelta.Create(pair.Key, element)
                    : new ElementChangeDelta { Owner = pair.Key, Element = id, Value = [0, 0, 0, 0] })
                .Where(e => !pair.Value.TryGetValue(e.Element, out var before) || !before.SequenceEqual(e.Value)))
            .ToArray();
    }
}
