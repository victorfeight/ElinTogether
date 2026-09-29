using System.Collections.Generic;
using System.Linq;
using ElinTogether.Net;

namespace ElinTogether.Models;

internal sealed class ActorOwnership : EClass
{
    // Resolve a human actor before following its master. Remote players are also
    // party members, but their party leader must not receive their personal rewards.
    internal static Chara? PlayerFor(Card? origin)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host) return origin == pc ? pc : null;
        var hostPlayer = NetSession.Instance.Player ?? pc;
        var actor = origin as Chara;
        var visited = new HashSet<int>();
        while (actor is not null && visited.Add(actor.uid)) {
            if (actor == hostPlayer || host.ActiveRemoteCharas.Values.Contains(actor)) return actor;
            actor = actor.master ?? actor.FindMaster();
        }
        return null;
    }
}
