using System;
using System.Collections.Generic;
using ElinTogether.Helper;
using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CharaActPerformDelta : ElinDelta
{
    // builtin acts that are not instantiated
    private static readonly Dictionary<int, Act> _builtInMapping = new() {
        [ABILITY.ActWait] = ACT.Wait,
        [ABILITY.ActMelee] = ACT.Melee,
        [ABILITY.ActThrow] = ACT.Throw,
        [ABILITY.ActRanged] = ACT.Ranged,
        [ABILITY.ActKick] = ACT.Kick,
        [ABILITY.ActChat] = ACT.Chat,
        [ABILITY.ActPick] = ACT.Pick,
        [ABILITY.ActItem] = ACT.Item,
    };
    private static bool _staticMapped;
    private static readonly HashSet<Guid> CompletedZaps = [];

    [ElinPreLoad]
    private static void ResetZaps(GameIOContext context) => CompletedZaps.Clear();

    [Key(0)]
    public required int ActId { get; init; }

    [Key(1)]
    public required RemoteCard Owner { get; init; }

    [Key(2)]
    public required RemoteCard? TargetCard { get; init; }

    [Key(3)]
    public required Position? Pos { get; init; }

    // A zap is an item-bound action, not reconstructible from its ability ID alone.
    [Key(4)] public RemoteCard? Wand { get; init; }
    [Key(5)] public Guid ZapId { get; init; }
    [Key(6)] public int ZoneUid { get; init; }

    // Diagnostics only. Carry the sender's runtime type when the ID cannot identify it.
    [Key(7)] public string? DiagnosticActType { get; init; }
    [Key(8)] public Guid TravelRequest { get; init; }

    public static CharaActPerformDelta Create(Act act)
    {
        ApplyBuiltInMapping();

        return new() {
            ActId = act is ActZap ? ABILITY.ActZap : act.id,
            Owner = Act.CC,
            TargetCard = Act.TC,
            Pos = Act.TP,
            Wand = (act as ActZap)?.trait?.owner,
            ZapId = act is ActZap ? Guid.NewGuid() : Guid.Empty,
            ZoneUid = EClass._zone.uid,
            DiagnosticActType = act.id == 0 ? act.GetType().FullName : null,
            TravelRequest = SharedTravel.IsTravel(act) ? Guid.NewGuid() : Guid.Empty,
        };
    }

    protected override void OnApply(ElinNetBase net)
    {
        ApplyBuiltInMapping();

        if (TpSpellBridge.IsManagedId(ActId)) return; // Never replay Tp effects on peers.

        if (ActId == 0) {
            EmpLog.Warning("ActionTrace receiving zero-ID action: origin {OriginPeer}, actor {ActorUid}, target {TargetUid}, senderType {SenderType}, zone {ZoneUid}",
                OriginPeer, Owner.Uid, TargetCard?.Uid, DiagnosticActType ?? "unreported", ZoneUid);
            // Zero is not an ability identity. Native nested reactions arrive via
            // authoritative outcome deltas; never feed an invalid ID to ACT.Create.
            return;
        }

        if (ActId == ABILITY.ActZap) {
            ApplyZap(net);
            return;
        }

        // we do not apply to ourselves
        if (Owner.Find() is not Chara { IsPC: false } chara) {
            return;
        }

        // reperform act
        var act = _builtInMapping.GetValueOrDefault(ActId);
        act ??= chara.elements.GetElement(ActId)?.act ?? ACT.Create(ActId);
        act.id = ActId;

        if (SharedTravel.IsTravel(act) && (net is not ElinNetHost travelHost ||
            !SharedTravel.Accept(travelHost, OriginPeer, chara, TravelRequest, ZoneUid))) return;

        // pos compensation if high rtt
        var target = TargetCard?.Find();
        var pos = Pos;
        if (target is Chara { isDead: false, IsInActiveMap: true } targetChara &&
            pos is not null && targetChara.pos.Distance(pos) <= 2) {
            pos = targetChara.pos;
        }

        act.Perform(chara, target, pos);
    }

    private void ApplyZap(ElinNetBase net)
    {
        // Local effects are predicted by the acting client; other clients receive
        // authoritative results, not another cast (which would create fake summons).
        if (net is not ElinNetHost host) return;
        if (!host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var caster) ||
            caster.uid != Owner.Uid || caster.isDead || !caster.IsInActiveMap ||
            ZoneUid != EClass._zone.uid || Pos is not { IsInActiveMapBounds: true } ||
            Wand?.Find() is not Thing { isDestroyed: false, trait: TraitRod rod } wand ||
            wand.GetRootCard() != caster || ZapId == Guid.Empty) {
            EmpLog.Warning("Zap rejected peer {Peer}, caster {Caster}, wand {Wand}, zone {Zone}",
                OriginPeer, Owner.Uid, Wand?.Uid, ZoneUid);
            return;
        }
        if (!CompletedZaps.Add(ZapId)) return;

        var charges = wand.c_charges;
        var before = EClass._map.charas.Count;
        // This is a fresh host simulation. Let existing generation, placement,
        // charge and condition hooks publish its results through the normal stream.
        // In particular ZoneAddCardEvent must not suppress newly summoned actors.
        using var simulation = Simulate();
        var result = new ActZap { id = ABILITY.ActZap, trait = rod }.Perform(caster, null, Pos);
        EmpLog.Information("Zap applied {ZapId}: caster {Caster}, wand {Wand}, effect {Effect}/{Kind}, aim {X},{Z}, charges {Before}->{After}, charas {OldCount}->{NewCount}, result {Result}",
            ZapId, caster.uid, wand.uid, rod.IdEffect, rod.N1, Pos.X, Pos.Z,
            charges, wand.c_charges, before, EClass._map.charas.Count, result);
    }

    private static void ApplyBuiltInMapping()
    {
        if (_staticMapped) {
            return;
        }

        foreach (var (k, v) in _builtInMapping) {
            v.id = k;
        }
        _staticMapped = true;
    }
}
