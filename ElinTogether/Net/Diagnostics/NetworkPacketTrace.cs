using System.Linq;
using ElinTogether.Models;

namespace ElinTogether.Net;

internal static class NetworkPacketTrace
{
    // Metadata only. Never log profile JSON, save contents or raw packet payloads.
    internal static string Describe(object packet) => packet switch {
        PlayerProfileRequest p => $"profile-request uid={p.OwnerUid} session={p.Session} request={p.RequestId}",
        PlayerProfileCheckpoint p => $"profile-checkpoint uid={p.OwnerUid} session={p.Session} revision={p.Revision} request={p.RequestId} chars={p.Json.Length}",
        PlayerProfileReceipt p => $"profile-receipt uid={p.OwnerUid} session={p.Session} revision={p.Revision} request={p.RequestId}",
        WorldStateSnapshot p => $"world-snapshot tick={p.ServerTick} zone={p.ZoneUid}",
        WorldStateDeltaList p => $"world-deltas count={p.DeltaList.Count} types={string.Join(",", p.DeltaList.Take(8).Select(d => d?.GetType().Name))}",
        _ => packet.GetType().Name,
    };
}
