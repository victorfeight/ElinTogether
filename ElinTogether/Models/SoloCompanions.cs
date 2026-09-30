using System;
using System.Linq;
using ElinTogether.Net;

namespace ElinTogether.Models;

// An offline role of an existing saved human, never a copied NPC or a new account.
internal sealed class SoloCompanions : EClass
{
    internal const string Key = "emp_solo_companion";
    internal static bool IsCompanion(Chara actor) => !actor.IsPC && actor.GetBool(Key);
    internal static bool KeepOnOfflineLoad(Chara actor) => NetSession.Instance.Connection is null &&
        IsCompanion(actor) && actor.party == pc.party;

    internal static string? CannotRecruit(Chara actor)
    {
        var reason = SoloCharacterSelection.CannotSelect(actor);
        if (reason is not null) return reason;
        if (IsCompanion(actor)) return "This character is already a solo companion.";
        if (actor.parent is not null && actor.parent is not Zone)
            return "Release this character before bringing them as a companion.";
        return null;
    }

    internal static void Recruit(Chara actor)
    {
        var reason = CannotRecruit(actor);
        if (reason is not null) throw new InvalidOperationException(reason);
        SoloCharacterSelection.Backup(actor);
        var party = pc.party!;
        var zone = pc.currentZone!;
        // Live recruitment only: world/branch initialization has already finished.
        // Both humans already belong to this faction; MakeAlly would additionally
        // change home, summon/minion flags and faction progression unnecessarily.
        if (actor.party != party) {
            actor.party?.RemoveMember(actor);
            party.AddMemeber(actor);
        }
        actor.SetBool(Key, true);
        actor.SetBool("remote_chara", true);
        actor.SetBool(CINT.IsPC, false);
        actor.enemy = null;
        actor.global.transition = null;
        // MoveZone returns early for a parked human whose currentZone is already
        // here. AddCard handles both reparenting and active-map registration.
        zone.AddCard(actor, pc.pos.GetNearestPoint(allowBlock: false,
            allowChara: false, allowInstalled: true, ignoreCenter: true));
        actor.RefreshFaithElement();
        actor.ChooseNewGoal();
        EmpLog.Information("Saved character {Uid} recruited as offline companion", actor.uid);
    }

    internal static string? CannotDismiss(Chara actor)
    {
        if (!core.IsGameStarted || game.isLoading || NetSession.Instance.Connection is not null)
            return "Dismiss solo companions while playing offline.";
        if (!IsCompanion(actor)) return "Choose a solo companion.";
        return CannotPark(actor);
    }

    private static string? CannotPark(Chara actor) =>
        actor.host is not null || actor.ride is not null || actor.parasite is not null ||
        actor.parent is not null && actor.parent is not Zone
            ? "Dismount and release solo companions before dismissing them or hosting multiplayer." : null;

    internal static void Dismiss(Chara actor)
    {
        var reason = CannotDismiss(actor);
        if (reason is not null) throw new InvalidOperationException(reason);
        Park(actor);
    }

    internal static string? PrepareHosting()
    {
        var companions = game.cards.globalCharas.Values.Where(IsCompanion).ToArray();
        // Validate all before changing any character. Never take AI control away
        // from a mount while its rider is still attached.
        foreach (var actor in companions) {
            var reason = CannotPark(actor);
            if (reason is not null) return reason;
        }
        foreach (var actor in companions) Park(actor);
        return null;
    }

    private static void Park(Chara actor)
    {
        actor.SetNoGoal();
        actor.enemy = null;
        actor.party?.RemoveMember(actor);
        actor.parent?.RemoveCard(actor);
        actor.currentZone = null;
        actor.global.transition = null;
        actor.SetBool(Key, false);
        actor.SetBool("remote_chara", true);
        EmpLog.Information("Saved character {Uid} returned to dormant player state", actor.uid);
    }
}
