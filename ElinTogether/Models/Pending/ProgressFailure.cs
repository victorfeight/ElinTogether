using System;
using System.Collections.Generic;
using ElinTogether.Net;
using ElinTogether.Helper;
using ElinTogether.Patches;

namespace ElinTogether.Models;

// Only used after host completion fails. Deferred pickups are not completed state.
internal static class ProgressFailure
{
    internal static void StopTask(Chara owner, AIAct task)
    {
        var attached = false;
        for (var current = owner.ai; current is not null; current = current.child) {
            if (ReferenceEquals(current, task)) { attached = true; break; }
        }
        if (!attached) return;
        // Native Cancel calls PickHeld for NPCs. A human's remote mirror must not
        // independently stack/drop the selected tool (especially during overflow).
        var held = owner.held;
        var preserveHeld = owner.IsRemotePlayer;
        if (preserveHeld) owner.held = null;
        using var simulation = ElinDelta.Simulate(NetSession.Instance.Connection is ElinNetHost);
        try {
            if (AutoActTaskBridge.IsController(owner.ai)) AutoActTaskBridge.Stop(owner, owner.ai);
            else task.Stub_Cancel();
        } finally {
            if (preserveHeld && owner.held is null && held is { isDestroyed: false } && held.GetRootCard() == owner)
                owner.held = held;
        }
    }

    internal static List<ElinDelta> CaptureResults(Chara owner, AIAct action, List<ElinDelta> captured)
    {
        var results = new List<ElinDelta>();
        var locations = new Dictionary<int, RemoteCard>();
        void Remember(RemoteCard? card) { if (card is not null) locations[card.Uid] = card; }
        foreach (var delta in captured) {
            switch (delta) {
                case CharaPickThingDelta pick: Remember(pick.Thing); break;
                case ThingDelta product: Remember(product.Thing); break;
                // Zone.AddCard is captured by a prefix: reconcile what really landed,
                // not the requested location if that operation was interrupted.
                case ZoneAddCardDelta ground: Remember(ground.Card); break;
                default: results.Add(delta); break;
            }
        }
        if (action is TaskBuild build) {
            Remember(build.held);
            Remember(build.target);
        }
        foreach (var remote in locations.Values) {
            try {
                if (remote.Find() is not { } card) continue;
                if (card.isDestroyed) {
                    results.Add(new CardModNumDelta { Card = remote, Num = 0 });
                    continue;
                }
                // A generated product may have no parent because its pickup was
                // deferred. Keep that existing object recoverable on the host floor.
                if (card is Thing && card.parent is null) EClass._zone.AddCard(card, owner.pos);
                var actual = RemoteCard.Create(card, withData: true)!;
                results.Add(new CardModNumDelta { Card = actual, Num = card.Num });
                if (card is Thing thing && card.parent is Card parent) {
                    results.Add(new CardAddThingDelta {
                        Thing = actual, Parent = parent, TryStack = false,
                        DestInvX = thing.invX, DestInvY = thing.invY,
                    });
                } else if (card.parent is Zone zone) {
                    results.Add(new ZoneAddCardDelta { Card = actual, ZoneUid = zone.uid, Pos = card.pos });
                    results.Add(new CardPlacedDelta {
                        Owner = actual, PlaceState = card.placeState, Dir = card.dir, ByPlayer = false,
                    });
                }
            } catch (Exception ex) {
                EmpLog.Error(ex, "Failed to reconcile progress item {ItemUid} for {OwnerUid}", remote.Uid, owner.uid);
                NetSession.Instance.Connection?.ReportDesync(ex.ToString());
            }
        }
        return results;
    }
}
