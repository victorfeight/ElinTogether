using System.Linq;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

public enum ResidentOperation { MemberType, Maid }

[MessagePackObject]
public class ResidentCommandDelta : ElinDelta
{
    [Key(0)] public int ZoneUid { get; init; }
    [Key(1)] public int MemberUid { get; init; }
    [Key(2)] public ResidentOperation Operation { get; init; }
    [Key(3)] public FactionMemberType MemberType { get; init; }
    [Key(4)] public bool Maid { get; init; }
    protected override void OnApply(ElinNetBase net) { if (net is ElinNetHost host) ResidentManagement.Execute(host, this); }
}

[MessagePackObject]
public class ResidentStateDelta : ElinDelta
{
    [Key(0)] public int ZoneUid { get; init; }
    [Key(1)] public int MemberUid { get; init; }
    [Key(2)] public FactionMemberType MemberType { get; init; }
    [Key(3)] public int LivestockTimer { get; init; }
    [Key(4)] public bool WasInParty { get; init; }
    [Key(5)] public int MaidUid { get; init; }
    [Key(6)] public bool ClearBed { get; init; }

    internal static ResidentStateDelta Capture(FactionBranch branch, Chara c, bool clearBed = false) => new() {
        ZoneUid = branch.owner.uid, MemberUid = c.uid, MemberType = c.memberType,
        LivestockTimer = c.GetInt(36), WasInParty = c.c_wasInPcParty, MaidUid = branch.uidMaid, ClearBed = clearBed,
    };

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || ZoneUid != _zone.uid || Branch == null) return;
        var c = Branch.members.FirstOrDefault(c => c.uid == MemberUid);
        if (c == null) { net.Delta.DeferLocal(this); return; }
        if (PartyMembership.Protected(c)) return;
        // Apply state to the existing resident; do not replay costs, timers or UI callbacks.
        if (ClearBed) c.ClearBed();
        c.memberType = MemberType;
        c.SetInt(36, LivestockTimer);
        c.c_wasInPcParty = WasInParty;
        Branch.uidMaid = MaidUid;
        Branch.RefreshEfficiency();
        c.RefreshWorkElements(Branch.elements);
        ReserveManagement.RefreshList();
    }
}
