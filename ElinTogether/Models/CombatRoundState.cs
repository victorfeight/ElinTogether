using System.Collections.Generic;

namespace ElinTogether.Models;

// The host's round ledger is independent of AI task lifetime. Transport handlers
// authenticate the sender before touching it; stale/duplicate reports do nothing.
internal sealed class CombatRoundState
{
    internal int Id { get; private set; }
    internal HashSet<int> Ready { get; } = [];
    internal HashSet<int> Done { get; } = [];

    internal bool Advance(int id)
    {
        if (id < Id) return false;
        if (id > Id) {
            Ready.Clear();
            Done.Clear();
            Id = id;
        }
        return true;
    }

    internal void SetReady(int uid, int round, bool ready)
    {
        if (round != Id) return;
        if (ready) Ready.Add(uid);
        else Ready.Remove(uid);
    }

    internal bool Complete(int uid, int round) => round == Id && Done.Add(uid);

    internal void Reset()
    {
        Id = 0;
        Ready.Clear();
        Done.Clear();
    }
}
