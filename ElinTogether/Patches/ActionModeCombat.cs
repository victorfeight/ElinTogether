using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using ElinTogether.Elements;
using ElinTogether.Models;
using ElinTogether.Net;
using EModding.Helper;
using HarmonyLib;
using UnityEngine;

namespace ElinTogether.Patches;

[HarmonyPatch]
public class ActionModeCombat
{
    public enum CombatPhase
    {
        Inactive,
        Deciding,
        Executing,
        Advancing,
    }

    // failsafe
    private const float ExecutingTimeout = 30f;
    private const float VisibilityRefreshInterval = 0.5f;

    private static readonly CombatTimeline _timeline = new();
    private static readonly HashSet<int> _due = [];
    private static Dictionary<int, double> _deadlines = new();
    private static double _clock;
    internal static int RoundId { get; private set; }
    private static bool _reportedComplete;
    private static float _lastCompletionCost;
    private static bool _executeLocalTick;
    private static int _localTickDepth;
    internal static bool IsDispatching => _executeLocalTick;
    private static bool _advanceProcessed;
    private static bool _finishingManualStep;
    private static float _executingTimer;
    private static float _visibilityTimer;
    private static int _lastPlayerCount;
    private static bool _pcActedThisRound;

    private static AIAct? _pendingAi;
    private static Vector2 _pendingMoveDir;
    private static bool _applyingPending;
    private static bool _applyPendingQueued;
    private static bool _cancelRequested;
    private static bool? _lastReportedReady;
    private static float _nextWaitTrace;

    // Keep the round id in traces so reports from both machines can be aligned.
    private static void TraceTurn(string reason, string action = "")
    {
        EmpLog.Debug("TurnTrace {Reason}: uid {Uid}, phase {Phase}, action {Action}, acted {Acted}, noGoal {NoGoal}, pending {Pending}, decided {Decided}, done {Done}",
            reason, EClass.pc?.uid ?? -1, Phase, $"epoch={RoundId};time={_clock};due={string.Join(",", _due)};{action}", _pcActedThisRound,
            EClass.pc?.HasNoGoal, _pendingAi?.GetType().Name ?? "none",
            string.Join(",", _timeline.Ready.OrderBy(uid => uid)), string.Join(",", _timeline.Done.OrderBy(uid => uid)));
    }

    internal static Dictionary<int, bool> EnemyVisibility { get; } = [];
    internal static CombatPhase Phase { get; private set; }
    internal static bool Activated => Phase != CombatPhase.Inactive;
    internal static bool Paused => Phase == CombatPhase.Deciding;
    private static bool LocalDue => _due.Contains(EClass.pc.uid);
    internal static bool WaitForSelf { get; private set; }

    internal static bool SelfDecided => _pendingAi is not null ||
        (!EClass.pc.HasNoGoal && EClass.pc.ai.IsRunning) || EClass.pc.WillConsumeTurn() || EClass.pc.isDead;

    // Player ticks are dispatched exactly once by the scheduler, never by wall time.
    internal static bool BlockLocalTick(Chara chara) => Activated && chara.IsPC &&
        (!_executeLocalTick || Phase != CombatPhase.Executing || !LocalDue || _reportedComplete);

    private static void CompleteLocalRound(float cost)
    {
        if (_reportedComplete || Phase != CombatPhase.Executing || !LocalDue) return;
        _reportedComplete = true;
        _lastCompletionCost = cost;
        _deadlines[EClass.pc.uid] = _clock + cost;
        TraceTurn("turn-complete", $"cost={cost}");
        SendCompletion();
    }

    private static void SendCompletion()
    {
        if (NetSession.Instance.Connection is ElinNetHost) {
            OnRoundComplete(EClass.pc.uid, RoundId, _lastCompletionCost);
        } else {
            NetSession.Instance.Connection?.Delta.AddRemote(new CombatTurnCompleteDelta {
                RoundId = RoundId, Cost = _lastCompletionCost,
            });
        }
    }

    internal static void OnRoundComplete(int uid, int round, float cost)
    {
        if (Phase != CombatPhase.Executing || !_timeline.Complete(uid, round, cost)) {
            EmpLog.Debug("Combat completion ignored uid {Uid}, report round {ReportRound}, current {RoundId}, phase {Phase}",
                uid, round, RoundId, Phase);
            return;
        }
        EmpLog.Debug("Combat completion accepted uid {Uid}, round {RoundId}, cost {Cost}, next {NextTime}",
            uid, round, cost, _timeline.Deadlines[uid]);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Game), nameof(Game.OnUpdate))]
    internal static void CheckIfPauseNeeded()
    {
        if (NetSession.Instance.Connection is not { } connection) {
            ChangePhaseLocal(CombatPhase.Inactive);
            _timeline.Reset();
            RoundId = 0;
            _due.Clear();
            _deadlines.Clear();
            EnemyVisibility.Clear();
            _pendingAi = null;
            _applyPendingQueued = false;
            _cancelRequested = false;
            return;
        }

        var players = NetSession.Instance.CurrentPlayers.ToList();
        var keysToRemove = EnemyVisibility
            .Where(kv => players.All(p => p.CharaUid != kv.Key))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in keysToRemove) {
            EnemyVisibility.Remove(key);
        }

        RefreshSelfVisibility(connection);
        UpdatePendingDecision(connection);

        if (connection.IsHost) {
            HostPhaseUpdate(connection, players);
        }

        UpdateDecideMessage();
    }

    private static void RefreshSelfVisibility(ElinNetBase net)
    {
        _visibilityTimer += Time.deltaTime;
        if (_visibilityTimer < VisibilityRefreshInterval) {
            return;
        }
        _visibilityTimer = 0f;

        // HasNoEnemyInSight count null map as false
        if (EClass.game?.activeZone?.map is null) {
            return;
        }

        var visible = !EClass._zone.IsRegion && !CharaVisibilityChangeEvent.HasNoEnemyInSight();
        if (EnemyVisibility.GetValueOrDefault(EClass.pc.uid) == visible) {
            return;
        }

        EnemyVisibility[EClass.pc.uid] = visible;
        EmpLog.Debug("Self enemy visibility changed to {CombatVisible}", visible);
        net.Delta.AddRemote(new EnemyVisibilityDelta {
            PlayerId = EClass.pc.uid,
            Visible = visible,
        });
    }

    private static void UpdatePendingDecision(ElinNetBase net)
    {
        // Damage or an authoritative task result may finish a goal between turns.
        // Clear that ended goal so vanilla keyboard input sees HasNoGoal again.
        if (Activated && !EClass.pc.HasNoGoal && !EClass.pc.ai.IsRunning) {
            _finishingManualStep = true;
            try { EClass.pc.SetNoGoal(); }
            finally { _finishingManualStep = false; }
        }
        if (_applyPendingQueued && Phase == CombatPhase.Inactive) {
            _applyPendingQueued = false;
            ApplyPendingDecision(net);
        }
        if (Phase == CombatPhase.Executing && LocalDue && !_reportedComplete) {
            // Do not overlap movement animations. Keep both the decision and debt.
            if (EClass.pc.renderer is CharaRenderer { IsMoving: true }) return;
            _applyPendingQueued = false;
            _executeLocalTick = true;
            try {
                ApplyPendingDecision(net);
                if (!EClass.pc.isDead && !(_reportedComplete || EClass.pc.HasNoGoal && !EClass.pc.WillConsumeTurn())) {
                    EClass.pc.Tick();
                }
                if (!_reportedComplete) CompleteLocalRound(0f);
            } finally {
                _executeLocalTick = false;
            }
        }
        if (Phase != CombatPhase.Deciding) return;

        if (_cancelRequested) {
            _cancelRequested = false;
            if (!EClass.pc.HasNoGoal && EClass.pc.ai.Current is not AIProgress) EClass.pc.SetAI(new NoGoal());
        }
        if (!LocalDue) return;
        if (net.IsClient) {
            var ready = SelfDecided;
            if (_lastReportedReady != ready) {
                _lastReportedReady = ready;
                EmpLog.Debug("Combat ready report {CombatDecided}, epoch {Epoch}", ready, RoundId);
                net.Delta.AddRemote(new CombatReadyDelta { Ready = ready, RoundId = RoundId });
            }
        }
    }

    private static void ApplyPendingDecision(ElinNetBase net)
    {
        var pending = _pendingAi;
        _pendingAi = null;

        if (pending is not null && !EClass.pc.isDead) {
            _applyingPending = true;
            try {
                EmpLog.Debug("Combat applying pending decision {ActType}", pending.GetType().Name);
                Act.CC = EClass.pc;
                if (pending is GoalManualMove) {
                    EClass.player.nextMove = _pendingMoveDir;
                }

                // SetAIImmediate would invoke Tick before the scheduler's dispatch.
                EClass.pc.SetAI(pending);
            } finally {
                _applyingPending = false;
            }
        }

    }

    internal static void OnRemoteReady(int uid, int round, bool ready)
    {
        if (Phase != CombatPhase.Deciding || !_timeline.SetReady(uid, round, ready)) return;
        EmpLog.Debug("Combat readiness uid {Uid}, round {RoundId}, ready {Ready}", uid, round, ready);
    }

    internal static void ChangePhaseLocal(CombatPhase phase, int round = -1, int[]? due = null,
        double clock = 0, Dictionary<int, double>? deadlines = null)
    {
        if (round >= 0 && round < RoundId) return;
        if (Phase == phase && (round < 0 || round == RoundId)) {
            // A host retry must resend the acknowledgement, NEVER replay the action.
            if (phase == CombatPhase.Executing && _reportedComplete && LocalDue) SendCompletion();
            return;
        }
        var prev = Phase;
        if (round < 0) clock = _clock;
        if (round >= 0) RoundId = round;
        if (due is not null) { _due.Clear(); _due.UnionWith(due); }
        if (deadlines is not null) _deadlines = new(deadlines);
        _clock = clock;
        Phase = phase;
        _executingTimer = 0f;
        _lastReportedReady = null;
        _applyPendingQueued = phase == CombatPhase.Executing && LocalDue;
        TraceTurn("phase-transition", $"{prev}->{phase}");
        EmpLog.Debug("Combat phase changed to {CombatPhase}, round {RoundId}, time {Clock}", phase, RoundId, clock);
        switch (phase) {
            case CombatPhase.Inactive:
                // Rejoin vanilla scheduling with the outstanding action debt intact.
                if (prev != CombatPhase.Inactive && _deadlines.TryGetValue(EClass.pc.uid, out var at)) {
                    EClass.pc.roundTimer = EClass.pc.actTime - (float)System.Math.Max(0, at - clock);
                }
                _due.Clear();
                _reportedComplete = false;
                _pcActedThisRound = false;
                _cancelRequested = false;
                WaitForSelf = false;
                _applyPendingQueued = _pendingAi is not null;
                Msg.SayGod("emp_ui_combat_exit".lang());
                break;
            case CombatPhase.Deciding:
                if (prev == CombatPhase.Inactive) Msg.SayGod("emp_ui_combat_enter".lang());
                // Keep continuing goal enumerators intact.
                break;
            case CombatPhase.Executing:
                _pcActedThisRound = false;
                _reportedComplete = false;
                break;
            case CombatPhase.Advancing:
                _advanceProcessed = false;
                break;
        }
    }

    private static void HostPhaseUpdate(ElinNetBase net, List<NetPeerState> players)
    {
        var active = NetSession.Instance.Rules.UseTurnBasedCombat &&
                     EnemyVisibility.Values.Any(v => v) && players.Count >= 2;
        var participants = players.Select(p => p.FindChara())
            .Where(c => c is not null && !c.isDead).Select(c => new KeyValuePair<int, double>(
                c!.uid, System.Math.Max(0, c.actTime - c.roundTimer))).ToArray();
        _timeline.Synchronize(participants);
        active &= participants.Length > 0;

        // Let dispatched actions acknowledge their final cost before leaving combat.
        if (!active && Phase != CombatPhase.Executing) {
            if (Activated) PublishPhase(net, CombatPhase.Inactive);
            return;
        }
        if (Activated && players.Count != _lastPlayerCount) PublishPhase(net, Phase);
        _lastPlayerCount = players.Count;
        if (Time.unscaledTime >= _nextWaitTrace) {
            _nextWaitTrace = Time.unscaledTime + 2f;
            TraceTurn("host-wait", string.Join(";", _timeline.Deadlines.Select(p => $"{p.Key}:next={p.Value}")));
        }

        switch (Phase) {
            case CombatPhase.Inactive:
                _timeline.Reset(false);
                _timeline.Synchronize(participants);
                // Existing timer debt matters on entry; don't grant everyone a free action.
                BeginAdvance(net);
                break;
            case CombatPhase.Deciding:
                if (_timeline.Due.Contains(EClass.pc.uid)) _timeline.SetReady(EClass.pc.uid, RoundId, SelfDecided);
                if (_timeline.AllReady) PublishPhase(net, CombatPhase.Executing);
                break;
            case CombatPhase.Executing:
                _executingTimer += Time.unscaledDeltaTime;
                if (_timeline.AllDone) {
                    if (!active) PublishPhase(net, CombatPhase.Inactive);
                    else BeginAdvance(net);
                } else if (_executingTimer > ExecutingTimeout) {
                    // Retry the same epoch. Never forgive missing cost or execute twice.
                    EmpLog.Warning("Combat completion delayed; retrying epoch {Epoch}", RoundId);
                    _executingTimer = 0f;
                    PublishPhase(net, CombatPhase.Executing);
                }
                break;
            case CombatPhase.Advancing:
                if (_advanceProcessed) {
                    _timeline.BeginWindow();
                    PublishPhase(net, CombatPhase.Deciding);
                }
                break;
        }
    }

    private static void BeginAdvance(ElinNetBase net)
    {
        var elapsed = _timeline.Advance();
        PublishPhase(net, CombatPhase.Advancing);
        foreach (var chara in EClass._map.charas) {
            if (!chara.IsPC && chara.ai is not GoalRemote) chara.roundTimer += (float)elapsed;
        }
        EmpLog.Debug("Combat timeline advanced {Elapsed} to {Clock}", elapsed, _timeline.Now);
    }

    private static void PublishPhase(ElinNetBase net, CombatPhase phase)
    {
        var packet = new CombatPhaseDelta {
            Phase = phase, RoundId = _timeline.Epoch,
            DuePlayers = phase is CombatPhase.Deciding or CombatPhase.Executing ? _timeline.Due.ToArray() : [],
            Clock = _timeline.Now, Deadlines = _timeline.Deadlines.ToDictionary(p => p.Key, p => p.Value),
        };
        net.Delta.AddRemote(packet);
        ChangePhaseLocal(packet.Phase, packet.RoundId, packet.DuePlayers, packet.Clock, packet.Deadlines);
        if (phase == CombatPhase.Inactive && net is ElinNetHost host) {
            foreach (var chara in host.ActiveRemoteCharas.Values) {
                if (packet.Deadlines.TryGetValue(chara.uid, out var at)) {
                    chara.roundTimer = chara.actTime - (float)System.Math.Max(0, at - packet.Clock);
                }
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameUpdater.CharaUpdater), nameof(GameUpdater.CharaUpdater.FixedUpdate))]
    private static void AfterCharaUpdate()
    {
        if (Phase == CombatPhase.Advancing && NetSession.Instance.IsHost && !Game.isPaused) _advanceProcessed = true;
    }

    private static void UpdateDecideMessage()
    {
        if (Phase != CombatPhase.Deciding) {
            WaitForSelf = false;
            return;
        }

        if (LocalDue && !SelfDecided) {
            if (!WaitForSelf) {
                WaitForSelf = true;
                Msg.SayGod("emp_ui_combat_decide".lang());
            }
        } else if (WaitForSelf) {
            WaitForSelf = false;
            Msg.SayGod("emp_ui_combat_wait".lang());
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.Tick))]
    [HarmonyPriority(Priority.First)]
    private static bool CapturePcTurnCount(Chara __instance, out int __state)
    {
        var blocked = BlockLocalTick(__instance) || Activated && __instance.IsPC && _localTickDepth > 0;
        __state = !blocked && Activated && __instance.IsPC ? EClass.player.stats.turns : -1;
        if (__state >= 0) _localTickDepth++;
        return !blocked;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.Tick))]
    private static void ReportPcTurnConsumed(Chara __instance, int __state)
    {
        if (__state >= 0) _localTickDepth--;
        if (__state < 0 || Phase != CombatPhase.Executing) {
            return;
        }

        // idle stats.turns++
        if (EClass.player.stats.turns == __state) {
            return;
        }

        _pcActedThisRound = true;

        __instance.roundTimer = 0f;
        CompleteLocalRound(__instance.actTime);
        // A manual step is complete; don't auto-select another step after key release.
        // Continuing combat/path/task goals stay attached with their enumerators intact.
        if (__instance.ai is GoalManualMove or GoalEndTurn || !__instance.ai.IsRunning) {
            EClass.player.nextMove = Vector2.zero;
            _finishingManualStep = true;
            try { __instance.SetNoGoal(); }
            finally { _finishingManualStep = false; }
        }
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Chara), nameof(Chara.Tick))]
    private static void FinishExceptionalTick(Chara __instance, int __state, System.Exception? __exception)
    {
        if (__exception is null || __state < 0) return;
        _localTickDepth = 0;
        EmpLog.Warning("Combat tick failed for {Uid}, epoch {Epoch}: {Error}", __instance.uid, RoundId, __exception.Message);
        // Never retry an action whose vanilla turn was already consumed.
        if (EClass.player.stats.turns != __state) CompleteLocalRound(__instance.actTime);
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameUpdater.CharaUpdater), nameof(GameUpdater.CharaUpdater.FixedUpdate))]
    private static IEnumerable<CodeInstruction> OnCharaUpdaterAccumulate(
        IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(
                new OperandContains(OpCodes.Stfld, nameof(Card.roundTimer)))
            .EnsureValid("CharaUpdater.FixedUpdate accumulate roundTimer")
            .SetInstructionAndAdvance(
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ActionModeCombat), nameof(AccumulateRoundTimer))))
            .MatchStartForward(
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Card), nameof(Card.roundTimer))),
                new CodeMatch(ci => ci.IsLdloc()),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Chara), nameof(Chara.actTime))),
                new CodeMatch(ci => ci.opcode == OpCodes.Bgt || ci.opcode == OpCodes.Bgt_S))
            .EnsureValid("combat round tick loop condition")
            .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(ActionModeCombat), nameof(ReadAvailableRoundTimer))))
            .InstructionEnumeration();
    }

    private static float ReadAvailableRoundTimer(Chara chara)
    {
        if (!Activated) return chara.roundTimer;
        if (chara.IsPC || chara.ai is GoalRemote || Phase != CombatPhase.Advancing) return float.NegativeInfinity;
        // The timeline visits exact deadlines; vanilla's strict > would defer ties.
        return chara.roundTimer + (float)CombatTimeline.Epsilon;
    }

    private static void AccumulateRoundTimer(Chara chara, float value)
    {
        if (Activated) return; // No character receives wall-clock time inside the timeline.
        chara.roundTimer = value;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AIAct), nameof(AIAct.Tick))]
    private static bool PreventImmediateAITick(AIAct __instance)
    {
        return !Paused && !(__instance.owner is { IsPC: true } owner && BlockLocalTick(owner));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.SetAIImmediate))]
    private static bool CaptureScheduledDecision(Chara __instance, AIAct g)
    {
        if (!Activated || _applyingPending || _executeLocalTick || !__instance.IsPC || g.IsNoGoal) {
            return true;
        }

        QueueInput(g);
        return false;
    }

    internal static void QueueInput(AIAct goal)
    {
        TraceTurn("queue-decision", goal.GetType().Name);
        _pendingAi = goal;
        if (goal is GoalManualMove) _pendingMoveDir = EClass.player.nextMove;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AIAct), nameof(AIAct.Cancel))]
    private static void RevokeDecisionOnCancel(AIAct __instance)
    {
        if (!Activated || _applyingPending || _executeLocalTick || _finishingManualStep || __instance.owner is not { IsPC: true }) {
            return;
        }

        if (_pendingAi is not null) {
            TraceTurn("cancel-pending", _pendingAi.GetType().Name);
            _pendingAi = null;
            EmpLog.Debug("Combat pending decision revoked");
        }

        _cancelRequested = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AM_Adv), nameof(AM_Adv.CanAct))]
    private static void CanChooseDuringRound(ref bool __result)
    {
        if (Activated && (Paused || !LocalDue || _reportedComplete)) __result = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActPlan.Item), nameof(ActPlan.Item.Perform))]
    private static bool QueueActInput(ActPlan.Item __instance, ref bool __result)
    {
        if (!Activated || _executeLocalTick) {
            return true;
        }

        var act = __instance.act;
        var pos = __instance.pos;
        if (!act.IsAct || pos is null) {
            return true;
        }

        // Buffer execution-phase clicks through SetAIImmediate for the next round.
        var cc = __instance.cc;
        var dist = cc.pos.Distance(pos);
        var canInteractNeighbor = dist == 1 && cc.CanInteractTo(pos);
        if (act.PerformDistance != -1 && (dist > act.PerformDistance || (dist == 1 && !canInteractNeighbor))) {
            // wrap DynamicAIAct
            return true;
        }

        var tc = __instance.tc;
        var tp = pos.Copy();
        Act.CC = cc;
        // no pos
        cc.SetAIImmediate(new DynamicAIAct(act.GetText(), () => act.Perform(cc, tc, tp)));

        __result = false;
        return false;
    }

}
