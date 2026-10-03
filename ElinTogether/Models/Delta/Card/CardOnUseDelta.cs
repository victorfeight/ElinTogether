using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CardOnUseDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Card { get; init; }

    [Key(1)]
    public required RemoteCard? RootCard { get; init; }

    [Key(2)]
    public required RemoteCard User { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (Card.Find() is not { isDestroyed: false } card || User.Find() is not Chara user) {
            return;
        }

        if (RootCard?.Find() != card.GetRootCard()) {
            return;
        }

        // These shrines create world objects. A client request is fresh host
        // simulation, not outcome replay: generation AND placement must publish.
        // Keep other uses in their existing context (e.g. owner-local enchants).
        if (net.IsHost && card.trait is TraitShrine shrine &&
            shrine.Shrine.id is "knowledge" or "item" or "strife") {
            // OnUse itself does not check isOn. Revalidate queued requests so a
            // second click cannot grant a second reward from an exhausted shrine.
            if (!shrine.CanUse(user)) return;
            net.Delta.AddRemote(this);
            using var simulation = Simulate();
            var used = shrine.OnUse(user);
            EmpLog.Information("Shrine use resolved: shrine {Uid}, kind {Kind}, actor {ActorUid}, result {Result}, active {Active}",
                card.uid, shrine.Shrine.id, user.uid, used, card.isOn);
            return;
        }

        if (net.IsHost) {
            net.Delta.AddRemote(this);
        }

        card.trait.OnUse(user);
    }
}
