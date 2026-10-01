using System;
using System.Collections.Generic;
using ElinTogether.Net;

namespace ElinTogether.Models;

// A failed result must not prevent later authoritative results from landing.
internal static class DeltaReplay
{
    internal static bool ApplyResults(ElinNetBase net, IEnumerable<ElinDelta> results, int ownerUid)
    {
        var succeeded = true;
        foreach (var delta in results) {
            try {
                delta.Apply(net);
            } catch (Exception ex) {
                succeeded = false;
                EmpLog.Error(ex, "Authoritative result failed for {OwnerUid}: {DeltaType}",
                    ownerUid, delta.GetType().Name);
                net.ReportDesync(ex.ToString());
            }
        }
        return succeeded;
    }
}
