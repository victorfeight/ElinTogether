using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class PartyMemberDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Member { get; init; }

    [Key(1)]
    public int DestZoneUid { get; set; }

    [Key(2)]
    public Position? DestPos { get; set; }

    [Key(3)]
    public bool Joined { get; set; }

    internal Chara? CaptureSource { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost) {
            ApplyHost();
        } else {
            ApplyClient();
        }
    }

    protected override bool OnRefresh()
    {
        if (CaptureSource != null) {
            DestZoneUid = CaptureSource.currentZone?.uid ?? 0;
            DestPos = CaptureSource.pos;
            Joined = CaptureSource.party == pc.party && pc.party.members.Contains(CaptureSource);
        }

        return true;
    }

    private void ApplyHost()
    {
        // Membership results are host-authoritative. Legacy removal requests
        // still originate from non-dialogue client RemoveMember callers.
        if (Joined) return;
        if (Member.Find() is not Chara { isDead: false, IsPlayer: false } chara ||
            chara.party is not { } party || party != pc.party) {
            return;
        }

        using var _ = Simulate();
        party.RemoveMember(chara);

        // _leaveParty
        if (chara.homeZone is { } home && home != game.activeZone) {
            chara.MoveZone(home);
        }
    }

    private void ApplyClient()
    {
        if (Member.Find() is not Chara { IsPlayer: false } chara) {
            return;
        }

        if (Joined) pc.party.AddMemeber(chara);
        else if (chara.party is { } party && party == pc.party && party.members.Contains(chara)) {
            party.Stub_RemoveMember(chara);
        }

        EmpLog.Debug("Party membership applied: member {Uid}, joined {Joined}, zone {ZoneUid}", chara.uid, Joined, DestZoneUid);

        if (DestZoneUid == 0 || (chara.currentZone?.uid == DestZoneUid &&
                (DestZoneUid != _zone.uid || chara.parent == _zone)) ||
            game.spatials.Find(DestZoneUid) is not { } dest) {
            return;
        }

        if (chara.IsInActiveMap) {
            _zone.RemoveCard(chara);
        }

        if (dest == _zone && DestPos is { } activePos) {
            dest.AddCard(chara, activePos.X, activePos.Z);
        } else {
            chara.currentZone = dest;
        }
        if (DestPos is { } pos && dest != _zone) {
            chara.pos.Set(pos.X, pos.Z);
        }
    }
}
