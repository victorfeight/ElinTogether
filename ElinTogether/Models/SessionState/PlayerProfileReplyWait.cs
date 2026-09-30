using System;
using System.Diagnostics;
using System.Threading;

namespace ElinTogether.Models;

internal static class PlayerProfileReplyWait
{
    // A bounded synchronous SAVE wait, not a recurring capture/update task.
    internal static bool Run(Action receive, Func<bool> complete, Func<bool> valid, int timeoutMilliseconds = 3000)
    {
        var watch = Stopwatch.StartNew();
        while (!complete()) {
            if (!valid() || watch.ElapsedMilliseconds >= timeoutMilliseconds) return false;
            receive();
            if (!complete()) Thread.Sleep(1);
        }
        return valid();
    }
}
