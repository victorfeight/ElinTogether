using System.Collections.Generic;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CharaStateSnapshot : EClass
{
    [Key(0)]
    public required RemoteCard Owner { get; init; }

    [Key(1)]
    public required Position Pos { get; init; }

    [Key(2)]
    public required int CurrentZoneUid { get; init; }

    [Key(3)]
    public required int Hp { get; set; }

    // player provided
    [Key(4)]
    public PlayerCharaStateSnapshot? State { get; set; }

    [Key(5)]
    public required bool IsDead { get; init; }

    [Key(6)]
    public required Hostility Hostility { get; init; }

    [Key(7)]
    public required Hostility OriginalHostility { get; init; }

    [Key(8)]
    public required int UidMaster { get; init; }

    [Key(9)]
    public required MinionType MinionType { get; init; }

    [Key(10)] public int Investment { get; init; }

    [Key(11)] public int? Hunger { get; init; }
    [Key(12)] public CompanionVitalsSnapshot? CompanionVitals { get; init; }

    public static CharaStateSnapshot Create(Chara chara)
    {
        return new() {
            Owner = chara,
            Pos = chara.pos,
            CurrentZoneUid = chara.currentZone.uid,
            Hp = chara.hp,
            IsDead = chara.isDead,
            Hostility = chara.hostility,
            OriginalHostility = chara.c_originalHostility,
            UidMaster = chara.c_uidMaster,
            MinionType = chara.c_minionType,
            Investment = chara.c_invest,
            Hunger = chara.hunger.value,
            CompanionVitals = NetSession.Instance.Connection is ElinNetHost host &&
                host.IsCompanionControlled(chara) ? CompanionVitalsSnapshot.Capture(chara) : null,
        };
    }

    public static CharaStateSnapshot CreateSelf()
    {
        return new() {
            Owner = pc,
            Pos = pc.pos,
            CurrentZoneUid = pc.currentZone.uid,
            Hp = pc.hp,
            IsDead = pc.isDead,
            Hostility = pc.hostility,
            OriginalHostility = pc.c_originalHostility,
            UidMaster = pc.c_uidMaster,
            MinionType = pc.c_minionType,
            Hunger = pc.hunger.value,
            State = new() {
                LastAct = PlayerActivity.ReportAct(pc),
                LastReceivedTick = NetSession.Instance.Tick,
                Speed = pc.Stub_get_Speed(),
            },
        };
    }

    /// <summary>
    ///     This only applies to client characters
    ///     We assume host is always right
    /// </summary>
    public void ApplyReconciliation(Chara? remoteChara = null)
    {
        var chara = remoteChara ?? Owner.Find() as Chara;
        if (chara is null) {
            return;
        }

        // Client reports have already passed the host's peer/control checks.
        // A local human keeps authority; a spectator follows the host only while
        // both ends agree it is companion-controlled. Never replay eating here.
        // IsPC is deliberately false during spectator replay; UI ownership is
        // still the actual local character reference.
        var isLocalCharacter = ReferenceEquals(chara, pc);
        if (Hunger is { } hunger && (remoteChara is not null || !isLocalCharacter ||
            (CompanionVitals is not null && PlayerControl.WatchingOwnCharacter))) {
            chara.hunger.value = hunger;
        }
        // Only host snapshots can carry companion vitals. Client reports never
        // override them; late break snapshots cannot overwrite a resumed PC.
        if (remoteChara is null && (!isLocalCharacter || PlayerControl.WatchingOwnCharacter)) {
            CompanionVitals?.Apply(chara);
        }

        // this is received from host side
        if (remoteChara is null) {
            chara.hp = Hp;
            if (!chara.IsPlayer) chara.c_invest = Investment;
        }

        if (chara.IsPC) {
            return;
        }

        // hostility checks
        if (remoteChara is null && !chara.IsRemotePlayer) {
            chara.hostility = Hostility;
            chara.c_originalHostility = OriginalHostility;
            if (chara.c_uidMaster != UidMaster) {
                chara.c_uidMaster = UidMaster;
                chara.master = null;
                chara.FindMaster();
            }

            if (chara.c_minionType != MinionType) {
                chara.c_minionType = MinionType;
            }
        }

        // one more check for lingering death events
        if (!Pos.IsInActiveMapBounds) {
            return;
        }

        // fixes should only be applied to other remote charas
        if (remoteChara is null) {
            if (IsDead) {
                return;
            }

            if (chara.isDead) {
                if (!chara.pos.IsValid) {
                    chara.pos.Set(Pos.X, Pos.Z);
                }

                chara.Revive(Pos, true);
            }
        } else if (chara.isDead || IsDead) {
            return;
        }

        if (chara.currentZone?.uid != CurrentZoneUid ||
            (chara.currentZone?.map is { } map && !map.charas.Contains(chara))) {
            // chara hasn't been brought to the same map yet
            if (CurrentZoneUid == NetSession.Instance.CurrentZone?.uid) {
                var zone = NetSession.Instance.CurrentZone;
                if (zone.map?.charas.Find(c => c.uid == chara.uid) is { } inMap) {
                    if (inMap != chara) {
                        CardCache.Set(inMap);
                    }
                } else {
                    chara.pos.Set(Pos.X, Pos.Z);
                    zone.AddCard(chara, Pos);
                }
            }

            return;
        }

        // somehow we are riding it
        if (chara.host is null) {
            // only apply position fix if on the same map
            if (NetSession.Instance.CurrentZone?.map is not null && chara.pos != Pos) {
                var dist = chara.pos.Distance(Pos);
                if (dist > 2 && (dist > 10 || !CharaMoveDelta.HasRecentMove(chara))) {
                    EmpLog.Debug("Reconcile force move chara {Uid} from {@FromPos} to {@Pos}",
                        chara.uid, chara.pos.Copy(), (Point)Pos);
                    var from = chara.pos.Copy();
                    if (chara.Stub_Move(Pos, Card.MoveType.Force) == Card.MoveResult.Success) {
                        CombatPresentation.SnapCorrection(chara, from);
                    }
                }
            }
        }
    }
}
