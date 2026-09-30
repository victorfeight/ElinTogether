using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Net;
using MessagePack;
using Newtonsoft.Json;

namespace ElinTogether.Models;

[MessagePackObject]
public sealed class OwnerProgressionAward
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public int OwnerUid { get; set; }
    [Key(2)] public int Skill { get; set; }
    [Key(3)] public int Amount { get; set; }
    [Key(4)] public bool Offering { get; set; }
    [Key(5)] public bool Contraband { get; set; }
}

// Absolute client-owned progression, acknowledged together with the award IDs.
// Never run ModExp on a remote player: its rounding and level-up path differ.
[MessagePackObject]
public sealed class OwnerProgressionCommitDelta : ElinDelta
{
    [Key(0)] public required RemoteCard Owner { get; init; }
    [Key(1)] public Guid[] Awards { get; set; } = [];
    [Key(2)] public ElementChangeDelta[] Elements { get; set; } = [];
    [Key(3)] public int Level { get; init; }
    [Key(4)] public int Exp { get; init; }
    [Key(5)] public int Feat { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (Owner.Find() is not Chara { IsPC: false } actor || Level < 1 || Exp < 0 || Feat < 0 ||
            Awards.Length == 0 || Awards.Distinct().Count() != Awards.Length ||
            Elements.Any(e => e.Owner.Uid != actor.uid || e.Value.Length != 4 || !sources.elements.map.ContainsKey(e.Element)) ||
            Elements.Select(e => e.Element).Distinct().Count() != Elements.Length) return;

        List<OwnerProgressionAward>? remaining = null;
        if (net is ElinNetHost host) {
            if (!host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var owned) || owned != actor) return;
            remaining = OwnerProgressionAwards.Read(actor);
            if (Awards.Any(id => !remaining.Any(a => a.Id == id))) return;
            remaining.RemoveAll(a => Awards.Contains(a.Id));
        }

        // Write the saved values and retire the award before derived callbacks.
        // A callback failure must not make a saved award payable a second time.
        foreach (var element in Elements) element.Write(actor);
        actor.LV = Level;
        actor.exp = Exp;
        actor.feat = Feat;
        if (remaining is not null) {
            OwnerProgressionAwards.Save(actor, remaining);
            net.Delta.AddRemote(this);
            EmpLog.Information("Progression acknowledged: actor {ActorUid}, awards {Awards}", actor.uid, Awards);
        }
        foreach (var element in Elements) element.RefreshDerived(actor);
    }
}

internal sealed class OwnerProgressionAwards : EClass
{
    private const string PendingKey = "emp_pending_progression";
    private static readonly HashSet<Guid> Applied = [];
    private static readonly List<Guid> Uncommitted = [];
    private static readonly HashSet<int> ElementIds = [];

    internal static void Reset() { Applied.Clear(); Uncommitted.Clear(); ElementIds.Clear(); }

    internal static List<OwnerProgressionAward> Read(Chara actor)
    {
        var saved = actor.GetStr(PendingKey);
        return string.IsNullOrEmpty(saved) ? [] : JsonConvert.DeserializeObject<List<OwnerProgressionAward>>(saved)!;
    }

    internal static void Save(Chara actor, List<OwnerProgressionAward> awards) =>
        actor.SetStr(PendingKey, awards.Count == 0 ? null : JsonConvert.SerializeObject(awards));

    internal static OwnerProgressionAward Record(Chara actor, Guid id, int skill, int amount)
    {
        var pending = Read(actor);
        var award = new OwnerProgressionAward { Id = id, OwnerUid = actor.uid, Skill = skill, Amount = amount };
        pending.Add(award);
        Save(actor, pending);
        return award;
    }

    internal static OwnerProgressionAward[] CapturePending() =>
        NetSession.Instance.Connection is ElinNetHost host
            ? host.ActiveRemoteCharas.Values.SelectMany(Read).ToArray() : [];

    internal static void Receive(OwnerProgressionAward[]? awards)
    {
        if (awards is null || NetSession.Instance.Connection is not ElinNetClient client || pc.isDead) return;
        foreach (var award in awards) {
            if (award.OwnerUid != pc.uid || award.Id == Guid.Empty || award.Amount <= 0 || !Applied.Add(award.Id)) continue;
            ElementIds.UnionWith(pc.elements.dict.Keys);
            // This allows ordinary client progression hooks to run. The send
            // boundary below replaces their packets with one acknowledged state.
            using (ElinDelta.Simulate()) {
                if (award.Offering) FaithOfferingProgression.Apply(pc, award.Amount, award.Contraband);
                else pc.ModExp(award.Skill, award.Amount);
            }
            Uncommitted.Add(award.Id);
            client.Delta.AddRemoteImmediate(new OwnerProgressionCommitDelta { Owner = pc });
        }
    }

    internal static void PrepareOutgoing(List<ElinDelta> batch, params List<ElinDelta>[] queued)
    {
        if (Uncommitted.Count == 0 || NetSession.Instance.Connection is not ElinNetClient) return;
        bool Absorb(ElinDelta delta)
        {
            switch (delta) {
                case ElementChangeDelta element when element.Owner.Uid == pc.uid:
                    ElementIds.Add(element.Element);
                    return true;
                case CharaLevelDelta level when level.Owner.Uid == pc.uid:
                case CharaFeatPointDelta feat when feat.Owner.Uid == pc.uid:
                case OwnerProgressionCommitDelta:
                    return true;
                default: return false;
            }
        }
        batch.RemoveAll(Absorb);
        foreach (var queue in queued) queue.RemoveAll(Absorb);
        ElementIds.UnionWith(pc.elements.dict.Keys);
        var elements = ElementIds.Select(id => pc.elements.dict.TryGetValue(id, out var element)
            ? ElementChangeDelta.Create(pc, element)
            : new ElementChangeDelta { Owner = pc, Element = id, Value = [0, 0, 0, 0] }).ToArray();
        // Before a subsequent quick-invest request: its quote may depend on the
        // skill gained here. Reliable transport preserves this order on the host.
        batch.Insert(0, new OwnerProgressionCommitDelta {
            Owner = pc, Awards = Uncommitted.ToArray(), Elements = elements,
            Level = pc.LV, Exp = pc.exp, Feat = pc.feat,
        });
        Uncommitted.Clear();
        ElementIds.Clear();
    }
}
