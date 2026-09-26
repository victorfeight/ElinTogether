using System;
using System.Collections.Generic;
using System.Linq;

namespace ElinTogether.Models;

// Host-owned simulation clock. Costs are final vanilla actTime values, after speed,
// terrain and action modifiers. Network/Unity adapters do not decide who is due.
internal sealed class CombatTimeline
{
    internal const double Epsilon = 0.000001;
    private readonly Dictionary<int, double> _next = new();
    internal double Now { get; private set; }
    internal int Epoch { get; private set; }
    internal HashSet<int> Due { get; } = new();
    internal HashSet<int> Ready { get; } = new();
    internal HashSet<int> Done { get; } = new();
    internal bool AllReady => Due.All(Ready.Contains);
    internal bool AllDone => Due.All(Done.Contains);
    internal IReadOnlyDictionary<int, double> Deadlines => _next;

    internal void Synchronize(IEnumerable<KeyValuePair<int, double>> participants)
    {
        var current = participants.ToDictionary(p => p.Key, p => p.Value);
        foreach (var uid in _next.Keys.Where(uid => !current.ContainsKey(uid)).ToArray()) {
            _next.Remove(uid);
            Due.Remove(uid);
            Ready.Remove(uid);
            Done.Remove(uid);
        }
        foreach (var pair in current) {
            if (!_next.ContainsKey(pair.Key)) _next[pair.Key] = Now + ValidDelay(pair.Value);
        }
    }

    // Open a new decision window without forgetting existing action debts.
    internal void BeginWindow()
    {
        Epoch++;
        Due.Clear();
        Ready.Clear();
        Done.Clear();
        foreach (var pair in _next) {
            if (pair.Value <= Now + Epsilon) Due.Add(pair.Key);
        }
    }

    internal bool SetReady(int uid, int epoch, bool ready)
    {
        if (epoch != Epoch || !Due.Contains(uid) || Done.Contains(uid)) return false;
        if (ready) Ready.Add(uid);
        else Ready.Remove(uid);
        return true;
    }

    internal bool Complete(int uid, int epoch, double cost)
    {
        if (epoch != Epoch || !Due.Contains(uid) || Done.Contains(uid) ||
            double.IsNaN(cost) || double.IsInfinity(cost) || cost < 0) return false;
        Done.Add(uid);
        // Zero means cancellation/invalid decision with NO consumed vanilla turn.
        // It must not charge time or grant enemies a free turn.
        _next[uid] = Now + cost;
        return true;
    }

    internal double Advance()
    {
        if (!AllDone) throw new InvalidOperationException("Unfinished due turns");
        var next = _next.Count == 0 ? Now : Math.Max(Now, _next.Values.Min());
        var elapsed = next - Now;
        Now = next;
        return elapsed;
    }

    internal double Remaining(int uid) => _next.TryGetValue(uid, out var at) ? Math.Max(0, at - Now) : 0;

    internal void Reset(bool resetEpoch = true)
    {
        Now = 0;
        if (resetEpoch) Epoch = 0;
        _next.Clear();
        Due.Clear();
        Ready.Clear();
        Done.Clear();
    }

    private static double ValidDelay(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(0, value);
}
