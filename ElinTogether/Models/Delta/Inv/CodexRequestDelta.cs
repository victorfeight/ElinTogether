using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Net;
using ElinTogether.Patches;
using ElinTogether.Helper;
using MessagePack;

namespace ElinTogether.Models;

// The book belongs to the host save. Requests never carry a client-authored count.
[MessagePackObject]
public class CodexRequestDelta : ElinDelta
{
    public enum Operation : byte { Withdraw, Collect, CollectInventory }
    [Key(0)] public Guid RequestId { get; set; } = Guid.NewGuid();
    [Key(1)] public Operation Action { get; set; }
    [Key(2)] public string Species { get; set; } = "";
    [Key(3)] public int ThingUid { get; set; }

    private static readonly HashSet<Guid> Completed = [];

    [ElinPreLoad]
    private static void Reset(GameIOContext context) => Completed.Clear();

    internal void Submit()
    {
        if (NetSession.Instance.Connection is ElinNetHost host) {
            Execute(host, pc);
        } else {
            NetSession.Instance.Connection?.Delta.AddRemote(this);
        }
    }

    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host && host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var receiver)) {
            Execute(host, receiver);
        }
    }

    private void Execute(ElinNetHost host, Chara receiver)
    {
        if (!Completed.Add(RequestId)) return;
        using var scope = Simulate();
        using var messages = PersonalMsgSayPatch.ForCollector(receiver);
        switch (Action) {
            case Operation.Withdraw:
                Withdraw(host, receiver);
                break;
            case Operation.Collect:
                if (CardCache.Find(ThingUid) is Thing { isDestroyed: false, trait: TraitCard } card &&
                    (card.GetRootCard() == receiver ||
                     (card.parent == _zone && receiver.IsInActiveMap && receiver.Dist(card.pos) <= 1))) {
                    ContentCodex.Collect(card);
                }
                break;
            case Operation.CollectInventory:
                foreach (var item in receiver.things.List(t => t.id == "figure3", onlyAccessible: true)) {
                    ContentCodex.Collect(item);
                }
                break;
        }
    }

    private void Withdraw(ElinNetHost host, Chara receiver)
    {
        if (!sources.charas.map.ContainsKey(Species)) return;
        var entry = player.codex.GetOrCreate(Species);
        if (entry.numCard <= 0) {
            Reply(host, "No cards remaining.");
            return;
        }

        var card = ThingGen.Create("figure3");
        card.MakeFigureFrom(Species);
        var delivered = false;
        try {
            var dest = receiver.things.GetDest(card);
            // Remote inventories can have no grid (IsFull counts ALL entries), or an
            // NPC grid containing hotbar/equipment. Neither measures PC backpack space.
            // Keep vanilla bag/stack routing first; only fall back to an actually free
            // main-backpack slot. This does not change general pickup/overflow behavior.
            var used = receiver.things.Count(t => t.invY != 1 && !t.isEquipped);
            var capacity = receiver.things.GridSize;
            if (!dest.IsValid && receiver != pc && used < capacity) {
                dest.container = receiver;
                EmpLog.Information("Codex capacity fallback for {Uid}: backpack {Used}/{Capacity}, total {Total}",
                    receiver.uid, used, capacity, receiver.things.Count);
            }
            if (!dest.IsValid) {
                EmpLog.Information("Codex withdrawal rejected for {Uid}: backpack {Used}/{Capacity}, total {Total}",
                    receiver.uid, used, capacity, receiver.things.Count);
                Reply(host, $"Inventory full on host ({used}/{capacity} backpack slots). The card remains in the shared book.");
                return;
            }
            // Use the selected destination directly: Pick would auto-collect the new card.
            if (dest.stack is { } stack) {
                delivered = card.TryStackTo(stack);
            } else {
                var stored = dest.container.AddThing(card, false);
                delivered = !stored.isDestroyed && stored.GetRootCard() == receiver;
            }
            if (!delivered) {
                Reply(host, "Could not deliver the card. The book was not charged.");
                return;
            }
            entry.numCard--;
            CodexCountDelta.Publish(Species, receiver, -1);
            EmpLog.Information("Codex withdrawal {RequestId}: species {Species}, receiver {Uid}, remaining {Count}",
                RequestId, Species, receiver.uid, entry.numCard);
        } finally {
            if (!delivered && !card.isDestroyed) card.Destroy();
        }
    }

    private void Reply(ElinNetHost host, string message)
    {
        var response = new CodexCountDelta {
            Species = Species, Count = player.codex.GetOrCreate(Species).numCard, Message = message,
        };
        if (OriginPeer == 0) {
            Msg.Say(message);
            CodexCountDelta.RefreshUI();
        } else {
            host.SendDeltaTo(OriginPeer, response);
        }
    }
}
