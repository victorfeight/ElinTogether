using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Helper;
using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

public enum BranchBoardOperation { Refresh, Upgrade, Research, Policy, Hire, HireTicket }

[MessagePackObject]
public sealed class BranchBoardCommand : ElinDelta
{
    [Key(0)] public Guid Id { get; set; }
    [Key(1)] public int ZoneUid { get; set; }
    [Key(2)] public BranchBoardOperation Operation { get; set; }
    [Key(3)] public int Target { get; set; }
    [Key(4)] public string Plan { get; set; } = "";
    [Key(5)] public int Expected { get; set; }
    [Key(6)] public int Price { get; set; }
    [Key(7)] public bool Active { get; set; }
    protected override void OnApply(ElinNetBase net) { if (net is ElinNetHost host) BranchBoardManagement.Execute(host, this); }
}

internal sealed class BranchBoardManagement : EClass
{
    private static readonly HashSet<(int, Guid)> Completed = [];
    internal static bool Resolving { get; private set; }
    [ElinPreLoad] private static void Reset(GameIOContext _) => Completed.Clear();

    internal static void Submit(BranchBoardCommand request)
    {
        request.Id = Guid.NewGuid(); if (request.ZoneUid == 0) request.ZoneUid = _zone.uid;
        if (NetSession.Instance.Connection is ElinNetClient client) client.Delta.AddRemote(request);
        else if (NetSession.Instance.Connection is ElinNetHost host) Execute(host, request, pc);
    }

    internal static void Execute(ElinNetHost host, BranchBoardCommand request, Chara? local = null)
    {
        var actor = local;
        if (actor == null && (!host.AcceptsPlayerInput(request.OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(request.OriginPeer, out actor))) return;
        if (request.Id == Guid.Empty || !Completed.Add((local == null ? request.OriginPeer : 0, request.Id))) return;
        void Reject(string reason) {
            if (local != null) Msg.Say(reason);
            else host.SendDeltaTo(request.OriginPeer, new MsgSayDelta { Text = reason, R = 1, G = 1, B = 1, A = 1 });
            EmpLog.Warning("Branch board rejected: actor {Actor}, operation {Operation}, reason {Reason}", actor!.uid, request.Operation, reason);
        }
        if (!Enum.IsDefined(typeof(BranchBoardOperation), request.Operation) || request.ZoneUid != _zone.uid ||
            !_zone.IsPCFaction || Branch == null || !actor!.IsInActiveMap || actor.isDead || actor.IsDisabled) {
            Reject("This settlement board is no longer available here."); return;
        }
        if (request.Operation == BranchBoardOperation.Refresh) {
            if (local == null) host.SendDeltaTo(request.OriginPeer, BranchBoardState.Capture());
            return;
        }
        var previous = pc;
        var changedBuild = false;
        var resolving = Resolving;
        Resolving = true;
        using var simulation = ElinDelta.Simulate();
        using var messages = MsgRelayContext.RedirectTo(actor);
        try {
            player.chara = actor;
            switch (request.Operation) {
                case BranchBoardOperation.Upgrade: {
                    var e = Branch.elements.GetElement(request.Target);
                    if (e == null || e.ValueWithoutLink != request.Expected || e.ValueWithoutLink <= 0 ||
                        e.source.category is "policy" or "landfeat" || e.HasTag("hidden") || e.source.cost.Length == 0 || e.source.cost[0] == 0 ||
                        Branch.GetTechUpgradeCost(e) is var price && (price <= 0 || price != request.Price)) {
                        Reject("The upgrade changed. Review the board again."); return;
                    }
                    if (actor.GetCurrency("money2") < request.Price) { Reject("Not enough currency for this upgrade."); return; }
                    actor.ModCurrency(-request.Price, "money2");
                    Branch.elements.ModBase(e.id, 1);
                    Branch.resources.SetDirty();
                    changedBuild = true;
                    break;
                }
                case BranchBoardOperation.Research: {
                    var p = Branch.researches.plans.FirstOrDefault(p => p.id == request.Plan);
                    if (p == null || p.rank != request.Expected || p.source.tech != request.Price ||
                        request.Price < 0 || !Branch.researches.CanCompletePlan(p)) {
                        Reject("This research changed or there is not enough knowledge."); return;
                    }
                    Branch.resources.knowledge.Mod(-p.source.tech);
                    Branch.researches.CompletePlan(p);
                    changedBuild = true;
                    break;
                }
                case BranchBoardOperation.Policy: {
                    var p = Branch.policies.list.FirstOrDefault(p => p.id == request.Target);
                    if (p == null) { Reject("This policy is not unlocked."); return; }
                    if (request.Active && !p.active && Branch.policies.CurrentAP() + p.Cost > Branch.MaxAP) {
                        Reject("Not enough administration points for this policy."); return;
                    }
                    p.active = request.Active;
                    Branch.policies.RefreshEffects();
                    break;
                }
                case BranchBoardOperation.Hire:
                case BranchBoardOperation.HireTicket: {
                    // Resolve only the host's current advertised candidate, never a client-created NPC.
                    Branch.UpdateReqruits();
                    var c = Branch.listRecruit.FirstOrDefault(i => i.chara.uid == request.Target)?.chara;
                    if (c == null || c.isDead || c.IsHomeMember() || PartyMembership.Protected(c)) {
                        Reject("This candidate is no longer available."); return;
                    }
                    var price = CalcGold.Hire(c);
                    if (price < 0 || price != request.Price) { Reject("The hiring price changed. Review the candidate again."); return; }
                    if (request.Operation == BranchBoardOperation.HireTicket) {
                        var ticket = actor.things.Find("ticket_resident");
                        if (ticket == null || ticket.Num <= 0) { Reject("You no longer have a resident ticket."); return; }
                        ticket.ModNum(-1);
                    } else {
                        if (actor.GetCurrency("money2") < price) { Reject("Not enough currency to hire this resident."); return; }
                        actor.ModCurrency(-price, "money2");
                    }
                    c.SetBool(18, true);
                    Branch.Recruit(c);
                    break;
                }
            }
            EmpLog.Information("Branch board resolved: actor {Actor}, operation {Operation}, target {Target}, plan {Plan}",
                actor.uid, request.Operation, request.Target, request.Plan);
        } finally {
            player.chara = previous;
            Resolving = resolving;
            host.Delta.AddRemote(BranchBoardState.Capture());
            if (changedBuild) BranchBoardUI.RefreshBuildMenu();
            BranchBoardUI.Refresh();
        }
    }

    internal static void Publish(FactionBranch branch)
    {
        if (!Resolving && core.IsGameStarted && !game.isLoading && !ZoneActivateEvent.IsHappening && branch == Branch && _zone.IsPCFaction &&
            NetSession.Instance.Connection is ElinNetHost host) host.Delta.AddRemote(BranchBoardState.Capture());
    }
}

[MessagePackObject]
public sealed class BranchBoardState : ElinDelta
{
    [Key(0)] public int ZoneUid { get; set; }
    [Key(1)] public Dictionary<int, int> Elements { get; set; } = [];
    [Key(2)] public required LZ4Bytes Research { get; set; }
    [Key(3)] public required LZ4Bytes Policies { get; set; }
    [Key(4)] public int Knowledge { get; set; }
    [Key(5)] public ReserveEntry[] Candidates { get; set; } = [];
    [Key(6)] public int[] GlobalPolicies { get; set; } = [];
    [Key(7)] public int LastRecruitUpdate { get; set; }

    internal static BranchBoardState Capture() => new() {
        ZoneUid = _zone.uid, Elements = Branch.elements.dict.Values.ToDictionary(e => e.id, e => e.vBase),
        // EClass's inherited opt-in JSON contract includes native saved fields,
        // excluding the managers' branch/owner back-references.
        Research = LZ4Bytes.Create(Branch.researches), Policies = LZ4Bytes.Create(Branch.policies),
        Knowledge = Branch.resources.knowledge.value, GlobalPolicies = Home.globalPolicies.ToArray(),
        LastRecruitUpdate = Branch.lastUpdateReqruit,
        Candidates = Branch.listRecruit.Select(i => new ReserveEntry {
            Member = RemoteCard.Create(i.chara, addToCache: true, withData: true), IsNew = i.isNew, Deadline = i.deadline,
        }).ToArray(),
    };

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost || _zone.uid != ZoneUid || Branch == null) return;
        var candidates = Candidates.Select(i => (Entry: i, Chara: i.Member.Find() as Chara)).ToArray();
        if (candidates.Any(i => i.Chara == null)) { net.Delta.DeferLocal(this); return; }
        var research = Research.Decompress<ResearchManager>();
        var policies = Policies.Decompress<PolicyManager>();
        var changedElements = Elements.Any(e => Branch.elements.Base(e.Key) != e.Value) ||
            Branch.elements.dict.Values.Any(e => e.vBase != 0 && !Elements.ContainsKey(e.id));
        // Keep the branch/element container identity; replay no purchases or research rewards.
        foreach (var e in Branch.elements.dict.Values.ToArray())
            if (!Elements.ContainsKey(e.id)) Branch.elements.SetBase(e.id, 0);
        foreach (var e in Elements) Branch.elements.SetBase(e.Key, e.Value);
        Branch.researches = research; research.SetOwner(Branch);
        Branch.policies = policies; policies.SetOwner(Branch);
        Branch.resources.knowledge.value = Knowledge;
        Home.globalPolicies.Clear(); Home.globalPolicies.UnionWith(GlobalPolicies);
        Branch.listRecruit.Clear();
        Branch.listRecruit.AddRange(candidates.Select(i => new HireInfo { chara = i.Chara!, isNew = i.Entry.IsNew, deadline = i.Entry.Deadline }));
        Branch.lastUpdateReqruit = LastRecruitUpdate;
        Branch.resources.SetDirty();
        if (changedElements) BranchBoardUI.RefreshBuildMenu();
        BranchBoardUI.Refresh();
    }
}
