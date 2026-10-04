using ElinTogether.Helper;
using ElinTogether.Net;
using System.Linq;
using System.Collections.Generic;

namespace ElinTogether.Models;

// Shared native mutations for dialogue and roster controls. Recruitment here
// never calls MakeAlly: existing residents must keep their home and faction.
internal sealed class PartyMembership : EClass
{
    // EClass.pc temporarily becomes the requesting client while validating CHA.
    // Session identities remain stable, including humans currently taking a break.
    internal static bool Protected(Chara c) => c.IsPlayer || c == NetSession.Instance.Player ||
        NetSession.Instance.CurrentPlayers.Any(p => p.CharaUid == c.uid) ||
        c.GetBool("remote_chara") || SoloCompanions.IsCompanion(c);

    internal static bool ManagedBy(Chara c, Chara actor)
    {
        if (Protected(c)) return false;
        var visited = new HashSet<Chara> { c };
        for (var rider = c.host; rider != null && rider != actor; rider = rider.host)
            if (!visited.Add(rider) || Protected(rider)) return false;
        visited.Clear();
        var pending = new Queue<Chara>(); pending.Enqueue(c);
        while (pending.Count > 0) {
            var current = pending.Dequeue();
            if (!visited.Add(current)) return false;
            foreach (var mount in new[] { current.ride, current.parasite }) {
                if (mount == null) continue;
                if (Protected(mount)) return false;
                pending.Enqueue(mount);
            }
        }
        return true;
    }

    internal static void Join(Chara c, Chara actor, bool recall, bool showMsg)
    {
        if (recall && c.currentZone != _zone) {
            c.MoveZone(_zone);
            c.MoveImmediate(actor.pos.GetNearestPoint(allowBlock: false, allowChara: false) ?? actor.pos);
        }
        actor.party.AddMemeber(c, showMsg);
    }

    internal static void Dismiss(Chara c, Chara actor, bool showMsg)
    {
        if (c.party == actor.party) actor.party.RemoveMember(c);
        if (c.homeZone is { } home && home != _zone) {
            if (showMsg) actor.Say("tame_send", c, home.Name);
            c.MoveZone(home);
        }
    }
}
