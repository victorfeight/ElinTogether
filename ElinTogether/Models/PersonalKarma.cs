using System;
using System.Linq;
using System.Collections.Generic;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

// The native Player is process-global. Store remote balances on the host's saved
// character; never swap Player.karma or pc to execute another player's penalty.
internal sealed class PersonalKarma : EClass
{
    internal static readonly int ValueKey = "emp_karma".GetHashCode();
    internal static readonly int RevisionKey = "emp_karma_revision".GetHashCode();
    private static Card? _actor;
    private static bool _scoped;
    private static bool _localOnly;
    private static int _nextLocalEvent;
    private static readonly SortedDictionary<int, LocalKarmaOutcomeDelta> PendingLocal = [];
    internal const string SessionKey = "emp_karma_session";
    internal static readonly int SequenceKey = "emp_karma_sequence".GetHashCode();
    private static int _receivedRevision = -1;
    private static int _notifiedRevision = -1;
    internal static Chara HostPlayer => NetSession.Instance.Player ?? pc;

    internal static IDisposable For(Card? actor, bool localOnly = false)
    {
        var previous = _actor;
        var scoped = _scoped;
        var previousLocal = _localOnly;
        _actor = actor;
        _scoped = true;
        _localOnly = localOnly;
        return new Restore(() => { _actor = previous; _scoped = scoped; _localOnly = previousLocal; });
    }

    private sealed class Restore(Action action) : IDisposable { public void Dispose() => action(); }

    internal static void Initialize(Chara actor, int? initial = null)
    {
        if (actor.GetInt(RevisionKey) != 0) return;
        // Legacy saves never stored a remote balance. Preserve their previous
        // join baseline once; new characters explicitly start at vanilla's 30.
        actor.SetInt(ValueKey, Math.Clamp(initial ?? player.karma, -100, 100));
        actor.SetInt(RevisionKey, 1);
    }

    internal static int Get(Chara actor) => actor == HostPlayer && NetSession.Instance.IsHost
        ? player.karma : actor.GetInt(ValueKey, player.karma);

    internal static bool IsHuman(Chara actor) => actor == HostPlayer ||
        NetSession.Instance.Connection is ElinNetHost host && host.ActiveRemoteCharas.Values.Contains(actor);

    internal static bool IsCriminal(Chara actor) => Get(actor) < 0 && !actor.HasCondition<ConIncognito>();

    // Called only by native ModKarma. Native host/global events retain their
    // original implementation; actor-scoped remote events execute here once.
    internal static bool BeforeChange(int amount)
    {
        if (NetSession.Instance.Connection is null) return true;
        if (NetSession.Instance.Connection is ElinNetClient client) {
            // Local UI/dynamic callbacks have no host execution counterpart.
            // Never report native host-result replay or a covered action twice.
            if (amount != 0 && (!_scoped || _localOnly) &&
                (!ElinDelta.IsApplying || ThingRequest.IsReplayingIntent)) {
                var outcome = new LocalKarmaOutcomeDelta {
                    Session = pc.GetStr(SessionKey), Sequence = ++_nextLocalEvent, Amount = amount,
                };
                PendingLocal.Add(outcome.Sequence, outcome);
                client.Delta.AddRemote(outcome);
            }
            return false;
        }
        if (!_scoped) return true;
        var recipient = ActorOwnership.PlayerFor(_actor);
        if (recipient is null) return false; // NPC actions must not charge a human.
        if (recipient == HostPlayer) return true;
        if (_localOnly && ElinDelta.IsRemoteStateLanding) return false;
        Change(recipient, amount);
        return false;
    }

    internal static void Change(Chara actor, int amount)
    {
        if (amount == 0 || NetSession.Instance.Connection is not ElinNetHost host) return;
        Initialize(actor);
        var before = Get(actor);
        var value = (int)Math.Clamp((long)before + amount, -100L, 100L);
        actor.SetInt(ValueKey, value);
        actor.SetInt(RevisionKey, actor.GetInt(RevisionKey) + 1);
        // Quest progress counts the event, even when personal karma is clamped.
        foreach (var quest in game.quests.list.ToArray()) quest.OnModKarma(amount);
        if (before >= 0 && value < 0) actor.pos.TryWitnessCrime(actor);
        if ((before < 0) != (value < 0)) _zone.RefreshCriminal();
        var peer = host.ActiveRemoteCharas.FirstOrDefault(p => p.Value == actor).Key;
        if (peer > 0) host.SendDeltaTo(peer, new PersonalKarmaDelta {
            State = Capture(actor), Amount = amount, Before = before,
        });
        EmpLog.Debug("Personal karma actor {ActorUid}: {Before}->{After}, event {Amount}", actor.uid, before, value, amount);
    }

    internal static PersonalKarmaState Capture(Chara actor) => new() {
        ActorUid = actor.uid, Value = Get(actor), Revision = actor.GetInt(RevisionKey),
        AcknowledgedSequence = actor.GetInt(SequenceKey),
    };

    internal static PersonalKarmaState[] CaptureAll() => NetSession.Instance.Connection is ElinNetHost host
        ? host.ActiveRemoteCharas.Values.Select(Capture).ToArray() : [];

    internal static void BeginSession(Chara actor)
    {
        Initialize(actor);
        actor.SetStr(SessionKey, Guid.NewGuid().ToString("N"));
        actor.SetInt(SequenceKey, 0);
        _zone.RefreshCriminal();
    }

    internal static void Join(Chara actor)
    {
        _nextLocalEvent = 0;
        PendingLocal.Clear();
        _receivedRevision = actor.GetInt(RevisionKey);
        _notifiedRevision = _receivedRevision;
        player.karma = actor.GetInt(ValueKey, player.karma);
    }

    internal static void Receive(PersonalKarmaState state, int? amount = null, int before = 0)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client || state.ActorUid != pc.uid) return;
        foreach (var sequence in PendingLocal.Keys.Where(n => n <= state.AcknowledgedSequence).ToArray()) PendingLocal.Remove(sequence);
        foreach (var pending in PendingLocal.Values) client.Delta.AddRemote(pending);
        if (state.Revision >= _receivedRevision) {
            _receivedRevision = state.Revision;
            player.karma = state.Value;
            pc.SetInt(ValueKey, state.Value);
            pc.SetInt(RevisionKey, state.Revision);
        }
        if (amount is not { } change || state.Revision <= _notifiedRevision) return;
        _notifiedRevision = state.Revision;
        Msg.Say(change > 0 ? "karmaUp" : "karmaDown", change.ToString());
        if (change < 0) Tutorial.Reserve("karma");
        if (before >= 0 && state.Value < 0) {
            Msg.Say("becomeCriminal");
            Tutorial.Reserve("criminal");
            Steam.GetAchievement(ID_Achievement.NERUN);
        } else if (before < 0 && state.Value >= 0) Msg.Say("becomeNonCriminal");
    }
}

[MessagePackObject]
public sealed class PersonalKarmaState
{
    [Key(0)] public int ActorUid { get; set; }
    [Key(1)] public int Value { get; set; }
    [Key(2)] public int Revision { get; set; }
    [Key(3)] public int AcknowledgedSequence { get; set; }
}

[MessagePackObject]
public sealed class PersonalKarmaDelta : ElinDelta
{
    [Key(0)] public required PersonalKarmaState State { get; init; }
    [Key(1)] public int Amount { get; init; }
    [Key(2)] public int Before { get; init; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsClient) PersonalKarma.Receive(State, Amount, Before);
    }
}

// Like existing client-owned UI actions, this reports the local native outcome;
// it is not proof of a crime. Only the authenticated peer's personal balance can
// change. The session/sequence prevents replay and the host computes the balance.
[MessagePackObject]
public sealed class LocalKarmaOutcomeDelta : ElinDelta
{
    [Key(0)] public string? Session { get; init; }
    [Key(1)] public int Sequence { get; init; }
    [Key(2)] public int Amount { get; init; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net is not ElinNetHost host ||
            !host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var actor) ||
            string.IsNullOrEmpty(Session) || Session != actor.GetStr(PersonalKarma.SessionKey) ||
            Sequence <= actor.GetInt(PersonalKarma.SequenceKey)) return;
        if (Sequence != actor.GetInt(PersonalKarma.SequenceKey) + 1) {
            // Reliable ordered transport normally makes this unnecessary. Keep
            // the existing delta deferral path for batches with a missing event.
            host.Delta.DeferLocal(this);
            return;
        }
        actor.SetInt(PersonalKarma.SequenceKey, Sequence);
        PersonalKarma.Change(actor, Amount);
    }
}
