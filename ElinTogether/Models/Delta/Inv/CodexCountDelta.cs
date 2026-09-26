using ElinTogether.Net;
using MessagePack;
using UnityEngine;

namespace ElinTogether.Models;

[MessagePackObject]
public class CodexCountDelta : ElinDelta
{
    [Key(0)] public string Species { get; set; } = "";
    [Key(1)] public int Count { get; set; }
    [Key(2)] public string? Message { get; set; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || Count < 0 || !sources.charas.map.ContainsKey(Species)) return;
        player.codex.GetOrCreate(Species).numCard = Count;
        if (Message is not null) Msg.Say(Message);
        RefreshUI();
    }

    protected override bool OnRefresh()
    {
        // Multiple transactions can queue updates before the next network flush.
        // Send the final authoritative count, including after a direct rejection reply.
        if (NetSession.Instance.Connection is ElinNetHost) {
            Count = player.codex.GetOrCreate(Species).numCard;
        }
        return true;
    }

    internal static void Publish(string species, Chara? actor = null, int change = 0)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host) return;
        var entry = player.codex.GetOrCreate(species);
        string? message = null;
        if (actor is not null && change != 0) {
            var name = actor.c_altName ?? actor.NameSimple;
            var count = System.Math.Abs(change);
            var cards = count == 1 ? "card" : "cards";
            message = change > 0
                ? $"{name} added {count} {entry.Name} {cards} to the shared collection."
                : $"{name} withdrew {count} {entry.Name} {cards} from the shared collection.";
        }
        // Reuse the authoritative count packet: one confirmation on every peer,
        // with no extra message relay or client-side prediction of a successful change.
        host.Delta.AddRemote(new CodexCountDelta {
            Species = species, Count = entry.numCard, Message = message,
        });
        if (message is not null) Msg.Say(message);
        RefreshUI();
    }

    internal static void RefreshUI()
    {
        foreach (var view in Object.FindObjectsOfType<ContentCodex>()) {
            if (view.list.isBuilt) {
                var selected = view.currentCodex;
                view.RefreshList();
                if (selected is { numCard: > 0 }) {
                    view.currentCodex = selected;
                    view.list.Select(selected);
                }
                view.RefreshInfo();
            }
        }
    }
}
