using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ElinTogether.Net;

// No Unity/Steam references. The watchdog reads only managed metadata recorded
// by the main thread, so it can report a stuck native call or packet handler.
internal sealed class NetworkFlightRecorder : IDisposable
{
    private sealed class Lane
    {
        internal long Count;
        internal double Last;
        internal string Detail = "none";
        internal string? Active;
        internal double Started;
        internal bool Expected;
        internal readonly Queue<string> Recent = new();
    }
    private readonly object _gate = new();
    private readonly Dictionary<string, Lane> _lanes = new();
    private readonly Func<double> _now;
    private readonly Action<string> _write;
    private readonly System.Threading.Timer? _timer;
    private readonly string _id = Guid.NewGuid().ToString("N");
    private string _context = "initializing";
    private double _contextAt;
    private double _lastDump = double.NegativeInfinity;
    private bool _disposed;
    private double _lastFaultReport = double.NegativeInfinity;

    internal NetworkFlightRecorder(Action<string> write, Func<double>? now = null, bool watchdog = true)
    {
        _write = write;
        _now = now ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
        _contextAt = _now();
        if (watchdog) _timer = new System.Threading.Timer(_ => Report("heartbeat"), null, 10000, 10000);
    }

    internal void Context(string context)
    {
        lock (_gate) { _context = context; _contextAt = _now(); }
    }

    internal void Expect(string name, bool expected)
    {
        lock (_gate) Get(name).Expected = expected;
    }

    private Lane Get(string name)
    {
        if (!_lanes.TryGetValue(name, out var lane)) _lanes.Add(name, lane = new() { Last = _now() });
        return lane;
    }

    internal void Mark(string name, string detail)
    {
        lock (_gate) {
            var lane = Get(name);
            lane.Last = _now(); lane.Count++; lane.Detail = detail;
            if (lane.Recent.Count == 16) lane.Recent.Dequeue();
            lane.Recent.Enqueue($"{lane.Last:F3} {detail}");
        }
    }

    internal Scope Enter(string name, string operation)
    {
        lock (_gate) {
            var lane = Get(name);
            var scope = new Scope(this, name, lane.Active, lane.Started);
            lane.Active = operation; lane.Started = lane.Last = _now(); lane.Count++;
            lane.Detail = operation;
            return scope;
        }
    }

    internal readonly struct Scope(NetworkFlightRecorder owner, string name, string? previous, double started) : IDisposable
    {
        public void Dispose()
        {
            if (owner is null) return;
            lock (owner._gate) {
                var lane = owner.Get(name);
                lane.Active = previous; lane.Started = started; lane.Last = owner._now();
            }
        }
    }

    internal void Fault(string detail)
    {
        Mark("fault", detail);
        lock (_gate) {
            if (_now() - _lastFaultReport < 5) return;
            _lastFaultReport = _now();
        }
        Report(detail, true);
    }

    internal void Report(string reason, bool dump = false)
    {
        string message;
        lock (_gate) {
            if (_disposed) return;
            var now = _now();
            var stalled = _lanes.Any(p => p.Value.Expected && now - p.Value.Last > 15 ||
                p.Value.Active is not null && now - p.Value.Started > 15);
            var includeHistory = (dump || stalled) && now - _lastDump >= (dump ? 5 : 60);
            message = $"trace={_id} reason={reason} contextAge={now - _contextAt:F1}s {_context}\n" +
                string.Join("\n", _lanes.Select(p => $"{p.Key}: count={p.Value.Count} age={now - p.Value.Last:F1}s last=[{p.Value.Detail}] active=[{p.Value.Active ?? "idle"}] activeAge={(p.Value.Active is null ? 0 : now - p.Value.Started):F1}s"));
            if (includeHistory) {
                _lastDump = now;
                message += "\nRECENT\n" + string.Join("\n", _lanes.SelectMany(p => p.Value.Recent.Select(s => p.Key + " " + s)));
            }
        }
        // Diagnostic output must never abort a game/network operation.
        try { _write(message); } catch { }
    }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
        _timer?.Dispose();
    }
}
