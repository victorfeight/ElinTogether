using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

public enum ReserveOperation { Refresh, Recruit, Release, Store }

[MessagePackObject]
public class ReserveCommandDelta : ElinDelta
{
    [Key(0)] public int ZoneUid { get; init; }
    [Key(1)] public int MemberUid { get; init; }
    [Key(2)] public ReserveOperation Operation { get; init; }
    protected override void OnApply(ElinNetBase net)
    {
        if (net is ElinNetHost host) ReserveManagement.Execute(host, this);
    }
}

[MessagePackObject]
public class ReserveEntry
{
    [Key(0)] public required RemoteCard Member { get; init; }
    [Key(1)] public bool IsNew { get; init; }
    [Key(2)] public int Deadline { get; init; }
}

[MessagePackObject]
public class ReserveStateDelta : ElinDelta
{
    [Key(0)] public ReserveEntry[] Entries { get; set; } = [];
    [Key(1)] public RemoteCard? Member { get; set; }
    [Key(2)] public int BranchUid { get; set; }
    [Key(3)] public int HomeUid { get; set; }
    [Key(4)] public int ZoneUid { get; set; }
    [Key(5)] public Position? Pos { get; set; }
    [Key(6)] public string? FactionUid { get; set; }
    [Key(7)] public bool Global { get; set; }
    [Key(8)] public bool Restrained { get; set; }
    [Key(9)] public FactionMemberType MemberType { get; set; }
    [Key(10)] public Hostility Hostility { get; set; }
    [Key(11)] public Hostility OriginalHostility { get; set; }
    [Key(12)] public int LivestockTimer { get; set; }
    internal Chara? CaptureSource { get; init; }

    internal static ReserveStateDelta Capture() { var result = new ReserveStateDelta(); result.OnRefresh(); return result; }

    protected override bool OnRefresh()
    {
        Entries = Home.listReserve.Select(i => new ReserveEntry {
            Member = RemoteCard.Create(i.chara, addToCache: true, withData: true), IsNew = i.isNew, Deadline = i.deadline,
        }).ToArray();
        if (CaptureSource is not { } c) return true;
        Member = RemoteCard.Create(c, addToCache: true, withData: true);
        BranchUid = Home.GetChildren().FirstOrDefault(b => b.members.Contains(c))?.owner.uid ?? 0;
        HomeUid = c.homeZone?.uid ?? 0;
        ZoneUid = c.currentZone?.uid ?? 0;
        Pos = c.pos;
        FactionUid = c.faction?.uid;
        Global = c.IsGlobal;
        Restrained = c.isRestrained;
        MemberType = c.memberType;
        Hostility = c.hostility;
        OriginalHostility = c.c_originalHostility;
        LivestockTimer = c.GetInt(36);
        return true;
    }

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost) return;
        // Preserve existing objects: replacing a character would split references
        // between the map, party, branch and reserve UI.
        Chara? Resolve(RemoteCard card) => Home.listReserve.FirstOrDefault(i => i.chara.uid == card.Uid)?.chara
            ?? card.Find() as Chara;
        var changed = Member == null ? null : Resolve(Member);
        var entries = Entries.Select(e => (Entry: e, Chara: Resolve(e.Member))).ToArray();
        if (entries.Any(e => e.Chara == null) || Member != null && changed == null) {
            net.Delta.DeferLocal(this);
            return;
        }
        Home.listReserve.Clear();
        foreach (var item in entries) Home.listReserve.Add(new HireInfo {
            chara = item.Chara!, isNew = item.Entry.IsNew, deadline = item.Entry.Deadline,
        });
        if (changed is { IsPlayer: false } c) {
            if (Entries.Any(e => e.Member.Uid == c.uid) && c.party is { } party && party.members.Contains(c))
                party.Stub_RemoveMember(c);
            var branches = Home.GetChildren();
            var oldBranch = branches.FirstOrDefault(b => b.members.Contains(c));
            var newBranch = branches.FirstOrDefault(b => b.owner.uid == BranchUid);
            if (oldBranch != newBranch && oldBranch != null) Home.charaElements.OnRemoveMember(c);
            foreach (var branch in branches) {
                var belongs = branch.owner.uid == BranchUid;
                if (belongs && !branch.members.Contains(c)) branch.members.Add(c);
                if (!belongs) branch.members.Remove(c);
                branch.RemoveRecruit(c);
                if (!belongs && branch.uidMaid == c.uid) branch.uidMaid = 0;
                if (belongs && branch.uidMaid == 0 && c.id == "maid") branch.uidMaid = c.uid;
            }
            if (FactionUid != null && game.factions.dictAll.TryGetValue(FactionUid, out var faction)) c.SetFaction(faction);
            c.homeZone = game.spatials.Find(HomeUid) as Zone;
            if (Global) c.SetGlobal(); else c.RemoveGlobal();
            c.isRestrained = Restrained;
            c.memberType = MemberType;
            c.SetInt(36, LivestockTimer);
            c.hostility = Hostility;
            c.c_originalHostility = OriginalHostility;
            if (newBranch != null) { c.enemy = null; c.orgPos = null; }
            if (oldBranch != newBranch && newBranch != null) Home.charaElements.OnAddMemeber(c);
            foreach (var branch in branches) branch.RefreshEfficiency();
            c.RefreshWorkElements();
            var dest = game.spatials.Find(ZoneUid) as Zone;
            if (c.currentZone != dest || dest == _zone && c.parent != dest) {
                if (c.parent is Zone previous) previous.RemoveCard(c);
                if (dest == _zone && Pos is { } p) dest.AddCard(c, p.X, p.Z);
                else { c.currentZone = dest; if (Pos is { } q) c.pos.Set(q.X, q.Z); }
            }
            EmpLog.Debug("Reserve state applied: member {Member}, branch {Branch}, zone {Zone}, reserves {Count}",
                c.uid, BranchUid, ZoneUid, Entries.Length);
        }
        ReserveManagement.RefreshList();
    }
}
