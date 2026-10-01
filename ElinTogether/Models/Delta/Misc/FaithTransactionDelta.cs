using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

public enum FaithAction { Pray, Convert, Offer }

[MessagePackObject]
public sealed class FaithVital
{
    [Key(0)] public required RemoteCard Owner { get; init; }
    [Key(1)] public int Hp { get; init; }
    [Key(2)] public int Mana { get; init; }
}

[MessagePackObject]
public sealed class FaithRequestDelta : ElinDelta
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public FaithAction Action { get; set; }
    [Key(2)] public int ZoneUid { get; set; }
    [Key(3)] public RemoteCard? Altar { get; set; }
    [Key(4)] public RemoteCard? Item { get; set; }
    [Key(5)] public int Quantity { get; set; }
    [Key(6)] public string God { get; set; } = "";
    [Key(7)] public bool Passive { get; set; }
    [Key(8)] public Religion.ConvertType Conversion { get; set; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host) FaithTransactions.Resolve(host, OriginPeer, this);
    }
}

[MessagePackObject]
public sealed class FaithResultDelta : ElinDelta
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public required RemoteCard Owner { get; init; }
    [Key(2)] public string FaithId { get; set; } = "";
    [Key(3)] public string PersonalState { get; set; } = "";
    [Key(4)] public ElementChangeDelta[] Elements { get; set; } = [];
    [Key(5)] public string Message { get; set; } = "";
    [Key(6)] public FaithVital[] Vitals { get; set; } = [];
    [Key(7)] public RemoteCard? Altar { get; set; }
    [Key(8)] public string AltarGod { get; set; } = "";
    [Key(9)] public RemoteCard? Remainder { get; set; }
    [Key(10)] public int RemainderBlessing { get; set; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || Owner.Find() is not Chara actor || !FaithTransactions.ReceiveOnce(Id)) return;
        var current = PersonalFaith.Receive(actor, PersonalState);
        if (current && game.religions.Find(FaithId) is { } god) actor.faith = god;
        foreach (var element in Elements) element.Write(actor);
        foreach (var element in Elements) element.RefreshDerived(actor);
        actor.RefreshFaithElement();
        foreach (var state in Vitals) {
            if (state.Owner.Find() is not Chara c) continue;
            c.hp = state.Hp;
            c.mana.value = state.Mana;
        }
        if (Altar?.Find()?.trait is TraitAltar altar) altar.SetDeity(AltarGod);
        if (Remainder?.Find() is Thing { isDestroyed: false } remaining) {
            remaining.SetBlessedState((BlessedState)RemainderBlessing);
            LayerInventory.SetDirty(remaining);
        }
        if (!actor.IsPC) return;
        if (current) PersonalFaith.RestoreJoinedPlayer();
        FaithTransactions.Complete(Id);
        if (Message.Length > 0) Msg.Say(Message);
    }
}

internal sealed class FaithTransactions : EClass
{
    internal static Chara? Actor { get; private set; }
    private static readonly Dictionary<(int Peer, Guid Id), FaithResultDelta> Completed = [];
    private static readonly HashSet<Guid> Received = [];
    private static Guid _pending;
    [ElinPreLoad] private static void OnLoad(GameIOContext context) => Reset();
    internal static void Reset() { Completed.Clear(); Received.Clear(); _pending = default; Actor = null; }
    internal static bool ReceiveOnce(Guid id) => id == Guid.Empty || Received.Add(id);

    private static Dictionary<Chara, (int Hp, int Mana)> CaptureVitals() =>
        _map.charas.ToDictionary(c => c, c => (c.hp, c.mana.value));
    private static FaithVital[] ChangedVitals(Dictionary<Chara, (int Hp, int Mana)> before) =>
        before.Where(p => !p.Key.isDestroyed && (p.Key.hp != p.Value.Hp || p.Key.mana.value != p.Value.Mana))
            .Select(p => new FaithVital { Owner = p.Key, Hp = p.Key.hp, Mana = p.Key.mana.value }).ToArray();

    internal static ScopeExit? BeginLocal(Chara actor, TraitAltar? altar = null, Thing? item = null)
    {
        if (Actor is not null || !actor.IsPC || NetSession.Instance.Connection is not ElinNetHost host) return default;
        var vitals = CaptureVitals();
        Actor = actor;
        return new() { OnExit = () => {
            Actor = null;
            PersonalFaith.CaptureLocal();
            host.Delta.AddRemote(new FaithResultDelta {
                Id = Guid.NewGuid(), Owner = actor, FaithId = actor.faith.id,
                PersonalState = actor.GetStr(PersonalFaith.SaveKey) ?? "", Vitals = ChangedVitals(vitals),
                Altar = altar?.owner, AltarGod = altar?.idDeity ?? "",
                Remainder = item is { isDestroyed: false } ? RemoteCard.Create(item, withData: true) : null,
                RemainderBlessing = (int)(item?.blessedState ?? BlessedState.Normal),
            });
        } };
    }

    internal static void Complete(Guid id) { if (_pending == id) _pending = default; }
    internal static void Submit(FaithAction action, string god = "", Card? altar = null, Thing? item = null, bool passive = false,
        Religion.ConvertType conversion = Religion.ConvertType.Default)
    {
        if (NetSession.Instance.Connection is not ElinNetClient client || _pending != Guid.Empty) return;
        var remote = PendingSplit.Split(item);
        if (remote is not null && PendingUid.IsPending(remote.Uid)) {
            Msg.Say("The offering is still synchronizing. Please try again.");
            return;
        }
        _pending = Guid.NewGuid();
        client.Delta.AddRemote(new FaithRequestDelta {
            Id = _pending, Action = action, God = god, ZoneUid = _zone.uid,
            Altar = altar, Item = remote, Quantity = item?.Num ?? 0, Passive = passive, Conversion = conversion,
        });
    }

    internal static void Resolve(ElinNetHost host, int peer, FaithRequestDelta request)
    {
        if (request.Id == Guid.Empty || !host.ActiveRemoteCharas.TryGetValue(peer, out var actor)) return;
        if (Completed.TryGetValue((peer, request.Id), out var completed)) {
            host.SendDeltaTo(peer, completed);
            return;
        }
        var response = new FaithResultDelta { Id = request.Id, Owner = actor };
        Completed.Add((peer, request.Id), response);
        var altar = request.Altar?.Find();
        var item = request.Item?.Find() as Thing;
        var god = game.religions.Find(request.God);
        var origin = item?.parent as Card ?? (item is null ? null : ThingRequest.GetDanglingOrigin(item, peer));
        var valid = !actor.isDead && actor.IsInActiveMap && request.ZoneUid == _zone.uid &&
            OwnerProgressionAwards.Read(actor).Count == 0 && (request.Action switch {
                FaithAction.Pray => !request.Passive || actor.HasElement(FEAT.featModelBeliever),
                FaithAction.Convert => god is { CanJoin: true } && (request.Conversion switch {
                    // Vanilla's only campaign conversion is DramaOutcome.convert_Jure.
                    // As with other dialogue intents, the client owns reaching the
                    // callback; the host executes its resolved character outcome.
                    Religion.ConvertType.Campaign => god == game.religions.Healing,
                    Religion.ConvertType.Default => god != actor.faith && _map.things.Any(t => !t.isDestroyed &&
                        t.trait is TraitAltar a && !a.IsBranchAltar && a.Deity == god && actor.Dist(t) <= 3),
                    _ => false,
                }),
                FaithAction.Offer => altar is { isDestroyed: false } && altar.ExistsOnMap && actor.Dist(altar) <= 3 &&
                    altar.trait is TraitAltar && item is { isDestroyed: false } &&
                    origin?.GetRootCard() == actor && request.Quantity > 0 && request.Quantity <= item.Num,
                _ => false,
            });
        if (!valid) {
            response.Message = "Worship could not complete. Wait for synchronization and try again near the altar.";
            Send();
            return;
        }

        PersonalFaith.CaptureLocal();
        PersonalFaith.AdvanceActive(actor);
        var previous = pc;
        var previousPrayed = player.prayed;
        var previousFeat = player.totalFeat;
        var previousCC = Act.CC;
        var previousTC = Act.TC;
        var previousTP = Act.TP;
        var previousGods = game.religions.list.Select(g => (God:g, g.giftRank, g.mood, g.relation)).ToArray();
        var state = PersonalFaith.Read(actor);
        var before = actor.elements.dict.Values.ToDictionary(e => e.id, e => ElementChangeDelta.Create(actor, e).Value);
        var vitals = CaptureVitals();
        Actor = actor;
        try {
            player.chara = actor;
            Act.CC = actor;
            Act.TC = actor;
            Act.TP = actor.pos;
            PersonalFaith.Restore(actor, state);
            using var simulate = ElinDelta.Simulate();
            using var karma = PersonalKarma.For(actor);
            using var messages = MsgRelayContext.RedirectTo(actor);
            switch (request.Action) {
                case FaithAction.Convert:
                    god!.JoinFaith(actor, request.Conversion);
                    break;
                case FaithAction.Pray:
                    ActPray.TryPray(actor, request.Passive);
                    break;
                case FaithAction.Offer:
                    var offering = item!.Split(request.Quantity);
                    ((TraitAltar)altar!.trait).OnOffer(actor, offering);
                    // Water and failed offerings are not consumed; restore a
                    // detached/split remainder through the normal item protocol.
                    if (!offering.isDestroyed && offering.GetRootCard() != actor) actor.AddThing(offering, false);
                    response.Altar = altar;
                    response.AltarGod = ((TraitAltar)altar.trait).idDeity;
                    if (!offering.isDestroyed) {
                        response.Remainder = RemoteCard.Create(offering, withData: true);
                        response.RemainderBlessing = (int)offering.blessedState;
                    }
                    break;
            }
        } catch (Exception ex) {
            EmpLog.Error(ex, "Worship transaction failed for {ActorUid}, request {RequestId}", actor.uid, request.Id);
            response.Message = "Worship stopped unexpectedly; its resolved state has been synchronized.";
        } finally {
            try {
                PersonalFaith.Capture(actor, state);
                PersonalFaith.Save(actor, state);
            } finally {
                player.chara = previous;
                player.prayed = previousPrayed;
                player.totalFeat = previousFeat;
                Act.CC = previousCC;
                Act.TC = previousTC;
                Act.TP = previousTP;
                foreach (var entry in previousGods) {
                    entry.God.giftRank = entry.giftRank; entry.God.mood = entry.mood; entry.God.relation = entry.relation;
                }
                Actor = null;
            }
        }
        response.Elements = actor.elements.dict.Keys.Union(before.Keys).Select(id => actor.elements.dict.TryGetValue(id, out var e)
            ? ElementChangeDelta.Create(actor, e) : new ElementChangeDelta { Owner = actor, Element = id, Value = [0,0,0,0] })
            .Where(e => !before.TryGetValue(e.Element, out var old) || !old.SequenceEqual(e.Value)).ToArray();
        response.Vitals = ChangedVitals(vitals);
        Send();
        host.Delta.AddRemote(response);
        EmpLog.Information("Worship resolved: actor {ActorUid}, action {Action}, request {RequestId}", actor.uid, request.Action, request.Id);

        void Send() {
            response.FaithId = actor.faith.id;
            response.PersonalState = actor.GetStr(PersonalFaith.SaveKey) ?? "";
            host.SendDeltaTo(peer, response);
        }
    }
}
