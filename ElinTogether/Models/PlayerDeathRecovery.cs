using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;

namespace ElinTogether.Models;

internal static class PlayerDeathRecovery
{
    internal sealed record DeathSite(Zone Zone, Point Position);
    private static readonly Dictionary<int, DeathSite> DeathSites = [];
    [ElinPreLoad] private static void Reset(GameIOContext context) => DeathSites.Clear();
    internal static void Revived(Chara actor) => DeathSites.Remove(actor.uid);

    internal static DeathSite? Capture(Chara actor) =>
        NetSession.Instance.Connection is ElinNetHost && actor.IsPlayer && !actor.isDead && actor.IsInActiveMap
            ? new(EClass._zone, actor.pos.Copy()) : null;

    internal static void DropOverweight(Chara actor, DeathSite? site)
    {
        if (site == null || !actor.isDead || site.Zone != EClass._zone || !site.Position.IsValid) return;
        // Native NPC-shaped remote players are removed from the zone on death.
        // Retain the death floor for same-floor rescue eligibility.
        DeathSites[actor.uid] = site;
        // Same predicate/traversal as native Chara.Revive. Move the actual items,
        // not copies; its later check naturally finds nothing already recovered.
        var drops = new List<Thing>();
        actor.things.Foreach(t => {
            if (!t.IsContainer && t.SelfWeight > actor.WeightLimit && t.trait.CanBeDropped &&
                (t.parentCard.c_lockLv == 0 || t.parentCard.trait is TraitChestPractice)) drops.Add(t);
        }, onlyAccessible: false);
        using var simulation = ElinDelta.Simulate();
        foreach (var item in drops) {
            item.ignoreAutoPick = true;
            site.Zone.AddCard(item, site.Position);
            Msg.Say("backpack_full_drop", item);
            EmpLog.Information("Death recovery drop: actor {ActorUid}, item {ItemUid}, zone {ZoneUid}, x {X}, z {Z}",
                actor.uid, item.uid, site.Zone.uid, site.Position.x, site.Position.z);
        }
    }

    internal static Chara? RescueTarget(Chara caster, Card target)
    {
        if (caster.isDead || !caster.IsPlayer || !caster.IsInActiveMap ||
            !(target.IsPCFaction || target.IsPCFactionMinion)) return null;
        var candidates = EClass.game.cards.globalCharas.Values.Where(c => c.IsPlayer && c.isDead &&
            !c.isSummon && c.faction == caster.faction &&
            (c.currentZone == EClass._zone || DeathSites.TryGetValue(c.uid, out var site) && site.Zone == EClass._zone)).ToList();
        // Explicit fallen-player target wins; self-cast/scroll rescues a fallen
        // player on this floor. With no human to rescue, use vanilla ally choice.
        return candidates.FirstOrDefault(c => c == target) ??
            (candidates.Count == 0 ? null : candidates[EClass.rnd(candidates.Count)]);
    }
}
