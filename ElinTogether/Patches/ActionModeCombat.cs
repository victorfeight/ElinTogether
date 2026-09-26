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
    }

    // failsafe
    private const float ExecutingTimeout = 30f;
    private const float VisibilityRefreshInterval = 0.5f;

    private static readonly CombatRoundState _round = new();
    private static HashSet<int> _decided => _round.Ready;
    private static HashSet<int> _done => _round.Done;
    internal static int RoundId => _round.Id;
    private static bool _reportedComplete;
    private static bool _settlingRound;
    private static bool _finishingManualStep;
    private static float _executingTimer;
    private static float _visibilityTimer;
    private static int _lastPlayerCount;

    private static readonly Dictionary<int, float> _turnBuffer = [];
    private static float _turnBuffered;
    private static bool _pcActedThisRound;

    private static AIAct? _pendingAi;
    private static Vector2 _pendingMoveDir;
    private static bool _applyingPending;
    private static bool _applyPendingQueued;
    private static bool _cancelRequested;
    private static bool? _lastReportedReady;
    private static float _nextWaitTrace;
    private static float _nextMoveBlockTrace;

    // Keep the round id in traces so reports from both machines can be aligned.
    private static void TraceTurn(string reason, string action = "")
    {
        EmpLog.Debug("TurnTrace {Reason}: uid {Uid}, phase {Phase}, action {Action}, acted {Acted}, noGoal {NoGoal}, pending {Pending}, decided {Decided}, done {Done}",
            reason, EClass.pc?.uid ?? -1, Phase, $"round={RoundId};{action}", _pcActedThisRound,
            EClass.pc?.HasNoGoal, _pendingAi?.GetType().Name ?? "none",
            string.Join(",", _decided.OrderBy(uid => uid)), string.Join(",", _done.OrderBy(uid => uid)));
    }

    internal static Dictionary<int, bool> EnemyVisibility { get; } = [];
    internal static CombatPhase Phase { get; private set; }
    internal static bool Activated => Phase != CombatPhase.Inactive;
    internal static bool Paused => Phase == CombatPhase.Deciding;
    internal static bool WaitForSelf { get; private set; }

    internal static bool SelfDecided => _pendingAi is not null ||
        (!EClass.pc.HasNoGoal && EClass.pc.ai.IsRunning) || EClass.pc.WillConsumeTurn() || EClass.pc.isDead;

    internal static bool BlockLocalTick(Chara chara) => Activated && chara.IsPC &&
        (Paused || _applyPendingQueued || _reportedComplete || _pcActedThisRound);

    private static void CompleteLocalRound(float cost)
    {
        if (_reportedComplete || Phase != CombatPhase.Executing) return;
        _reportedComplete = true;
        TraceTurn("turn-complete", $"round={RoundId},cost={cost}");
        if (NetSession.Instance.Connection is ElinNetHost) {
            OnRoundComplete(EClass.pc.uid, RoundId, cost);
        } else {
            NetSession.Instance.Connection?.Delta.AddRemote(new CombatTurnCompleteDelta {
                RoundId = RoundId, Cost = cost,
            });
        }
    }

    internal static void OnRoundComplete(int uid, int round, float cost)
    {
        if (Phase != CombatPhase.Executing || !_round.Complete(uid, round)) {
            EmpLog.Debug("Combat completion ignored uid {Uid}, report round {ReportRound}, current {RoundId}, phase {Phase}",
                uid, round, RoundId, Phase);
            return;
        }
        EmpLog.Debug("Combat completion accepted uid {Uid}, round {RoundId}, cost {Cost}", uid, round, cost);
        if (!float.IsNaN(cost) && !float.IsInfinity(cost) && cost > 0f) ReportPlayerTurn(uid, cost);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Game), nameof(Game.OnUpdate))]
    internal static void CheckIfPauseNeeded()
    {
        if (NetSession.Instance.Connection is not { } connection) {
            ChangePhaseLocal(CombatPhase.Inactive);
            _round.Reset();
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
            ApplyTurnBudget();
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
        if (_applyPendingQueued && Phase != CombatPhase.Deciding) {
            _applyPendingQueued = false;
            ApplyPendingDecision(net);
        }

        if (Phase == CombatPhase.Executing && !_applyPendingQueued &&
            (EClass.pc.isDead || ((EClass.pc.HasNoGoal || !EClass.pc.ai.IsRunning) && !EClass.pc.WillConsumeTurn()))) {
            CompleteLocalRound(0f);
        }
        if (Phase != CombatPhase.Deciding) return;

        if (_cancelRequested) {
            _cancelRequested = false;
            // progress
            if (!EClass.pc.HasNoGoal && EClass.pc.ai.Current is not AIProgress) {
                EClass.pc.SetAI(new NoGoal());
            }
        }

        if (net.IsClient) {
            var ready = SelfDecided;
            if (_lastReportedReady != ready) {
                _lastReportedReady = ready;
                EmpLog.Debug("Combat ready report {CombatDecided}", ready);
                net.Delta.AddRemote(new CombatReadyDelta {
                    Ready = ready, RoundId = RoundId,
                });
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

                EClass.pc.SetAIImmediate(pending);
            } finally {
                _applyingPending = false;
            }
        }

        // An invalid/cancelled decision must still release this round.
        if ((EClass.pc.HasNoGoal || !EClass.pc.ai.IsRunning) && !EClass.pc.WillConsumeTurn()) {
            CompleteLocalRound(0f);
        }
    }

    internal static void OnRemoteReady(int uid, int round, bool ready)
    {
        if (Phase != CombatPhase.Deciding || round != RoundId) return;
        _round.SetReady(uid, round, ready);
        EmpLog.Debug("Combat readiness uid {Uid}, round {RoundId}, ready {Ready}", uid, round, ready);
    }

    internal static void ChangePhaseLocal(CombatPhase phase, int round = -1)
    {
        if (round >= 0 && round < RoundId) return;
        if (Phase == phase && (round < 0 || round == RoundId)) return;
        if (round >= 0 && !_round.Advance(round)) return;

        TraceTurn("phase-transition", $"{Phase}->{phase}");
        var prev = Phase;
        Phase = phase;
        _executingTimer = 0f;
        _lastReportedReady = null;
        // entering Executing keeps the pending decision for the deferred apply
        _applyPendingQueued = phase == CombatPhase.Executing;
        EmpLog.Debug("Combat phase changed to {CombatPhase}, round {RoundId}", phase, RoundId);
        switch (phase) {
            case CombatPhase.Inactive:
                _decided.Clear();
                _done.Clear();
                _turnBuffer.Clear();
                _turnBuffered = 0f;
                _reportedComplete = false;
                _pcActedThisRound = false;
                _cancelRequested = false;
                WaitForSelf = false;
                // Keep accepted input: apply it once after leaving the barrier.
                _applyPendingQueued = _pendingAi is not null;
                Msg.SayGod("emp_ui_combat_exit".lang());
                break;
            case CombatPhase.Deciding:
                _decided.Clear();
                _done.Clear();
                if (prev == CombatPhase.Inactive) Msg.SayGod("emp_ui_combat_enter".lang());
                // Do not SetAI/Cancel here: that destroys a continuing goal's enumerator.
                break;
            case CombatPhase.Executing:
                _settlingRound = false;
                _done.Clear();
                _turnBuffer.Clear();
                _turnBuffered = 0f;
                _pcActedThisRound = false;
                _reportedComplete = false;
                break;
        }
    }

    private static void HostPhaseUpdate(ElinNetBase net, List<NetPeerState> players)
    {
        var active = NetSession.Instance.Rules.UseTurnBasedCombat &&
                     EnemyVisibility.Values.Any(v => v) &&
                     players.Count >= 2;

        if (!active) {
            ChangePhase(net, CombatPhase.Inactive);
            return;
        }

        // joined mid combat
        if (Activated && players.Count != _lastPlayerCount) {
            net.Delta.AddRemote(new CombatPhaseDelta {
                Phase = Phase, RoundId = RoundId,
            });
        }
        _lastPlayerCount = players.Count;
        if (Time.unscaledTime >= _nextWaitTrace) {
            _nextWaitTrace = Time.unscaledTime + 2f;
            TraceTurn("host-wait", string.Join(";", players.Select(p => {
                var c = p.FindChara();
                return $"{p.CharaUid}:dead={c?.isDead},goal={c?.ai?.GetType().Name},sleep={c?.conSleep is not null}";
            })));
        }

        switch (Phase) {
            case CombatPhase.Inactive:
                ChangePhase(net, CombatPhase.Deciding);
                break;

            case CombatPhase.Deciding: {
                var allDecided = SelfDecided && players.All(p =>
                    p.CharaUid == EClass.pc.uid ||
                    (p.FindChara() is { } chara && (chara.isDead || _decided.Contains(chara.uid))));
                if (allDecided) {
                    ChangePhase(net, CombatPhase.Executing);
                }
                break;
            }

            case CombatPhase.Executing: {
                _executingTimer += Time.deltaTime;
                // disconnected mid combat
                var allDone = (_reportedComplete || EClass.pc.isDead) && players.All(p =>
                    p.CharaUid == EClass.pc.uid ||
                    p.FindChara() is not { } chara ||
                    chara.isDead || _done.Contains(chara.uid));
                if (allDone) {
                    // Give the updater one executing frame to spend the granted NPC
                    // budget before pausing for decisions (otherwise enemies lag a round).
                    if (_settlingRound) ChangePhase(net, CombatPhase.Deciding);
                    else _settlingRound = true;
                } else if (_executingTimer > ExecutingTimeout) {
                    EmpLog.Warning("Combat executing phase timed out, forcing next round");
                    ChangePhase(net, CombatPhase.Deciding);
                }
                break;
            }
        }
    }

    private static void ChangePhase(ElinNetBase net, CombatPhase phase)
    {
        if (Phase == phase) {
            return;
        }

        var round = phase == CombatPhase.Deciding ? RoundId + 1 : RoundId;
        net.Delta.AddRemote(new CombatPhaseDelta {
            Phase = phase, RoundId = round,
        });
        ChangePhaseLocal(phase, round);
    }

    private static void UpdateDecideMessage()
    {
        if (Phase != CombatPhase.Deciding) {
            WaitForSelf = false;
            return;
        }

        if (!SelfDecided) {
            if (!WaitForSelf) {
                WaitForSelf = true;
                Msg.SayGod("emp_ui_combat_decide".lang());
            }
        } else if (WaitForSelf) {
            WaitForSelf = false;
            Msg.SayGod("emp_ui_combat_wait".lang());
        }
    }

    private static void ReportPlayerTurn(int uid, float actTime)
    {
        _turnBuffer[uid] = _turnBuffer.GetValueOrDefault(uid) + Mathf.Max(actTime, 0.01f);
    }

    private static void ApplyTurnBudget()
    {
        if (!Activated || _turnBuffer.Count == 0) {
            return;
        }

        if (EClass.game?.activeZone?.map is null) {
            return;
        }

        var target = _turnBuffer.Values.Max();
        var grant = target - _turnBuffered;
        if (grant <= 0f) {
            return;
        }

        _turnBuffered = target;
        foreach (var chara in EClass._map.charas) {
            if (chara.IsPC || chara.ai is GoalRemote) {
                continue;
            }

            chara.roundTimer += grant;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.Tick))]
    [HarmonyPriority(Priority.First)]
    private static bool CapturePcTurnCount(Chara __instance, out int __state)
    {
        __state = !BlockLocalTick(__instance) && Activated && __instance.IsPC ? EClass.player.stats.turns : -1;
        return !BlockLocalTick(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.Tick))]
    private static void ReportPcTurnConsumed(Chara __instance, int __state)
    {
        if (__state < 0 || Phase != CombatPhase.Executing) {
            return;
        }

        // idle stats.turns++
        if (EClass.player.stats.turns == __state) {
            return;
        }

        _pcActedThisRound = true;

        CompleteLocalRound(__instance.actTime);
        // A manual step is complete; don't auto-select another step after key release.
        // Continuing combat/path/task goals stay attached with their enumerators intact.
        if (__instance.ai is GoalManualMove || !__instance.ai.IsRunning) {
            EClass.player.nextMove = Vector2.zero;
            _finishingManualStep = true;
            try { __instance.SetNoGoal(); }
            finally { _finishingManualStep = false; }
        }
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

    private static float ReadAvailableRoundTimer(Chara chara) =>
        BlockLocalTick(chara) ? float.NegativeInfinity : chara.roundTimer;

    private static void AccumulateRoundTimer(Chara chara, float value)
    {
        if (BlockLocalTick(chara)) return;
        // non remote
        if (Activated && NetSession.Instance.Connection is ElinNetHost && chara is { IsPC: false, ai: not GoalRemote }) {
            return;
        }

        chara.roundTimer = value;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AIAct), nameof(AIAct.Tick))]
    private static bool PreventImmediateAITick(AIAct __instance)
    {
        return !Paused && !(__instance.owner is { IsPC: true } && _reportedComplete && Activated);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chara), nameof(Chara.SetAIImmediate))]
    private static bool CaptureDecisionWhileDeciding(Chara __instance, AIAct g)
    {
        if (!Activated || _applyingPending || !__instance.IsPC || g.IsNoGoal) {
            return true;
        }

        if (_pendingAi?.GetType() != g.GetType()) {
            EmpLog.Debug("Combat pending decision {ActType}", g.GetType().Name);
        }

        if (_pendingAi?.GetType() != g.GetType() || g is not GoalManualMove) {
            TraceTurn("queue-decision", g.GetType().Name);
        }
        _pendingAi = g;
        if (g is GoalManualMove) {
            _pendingMoveDir = EClass.player.nextMove;
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AIAct), nameof(AIAct.Cancel))]
    private static void RevokeDecisionOnCancel(AIAct __instance)
    {
        if (!Activated || _applyingPending || _finishingManualStep || __instance.owner is not { IsPC: true }) {
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
        if (Activated && (Paused || _reportedComplete)) __result = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActPlan.Item), nameof(ActPlan.Item.Perform))]
    private static bool QueueActWhileDeciding(ActPlan.Item __instance, ref bool __result)
    {
        if (!Activated) {
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

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GoalManualMove), nameof(GoalManualMove.TryMove))]
    private static bool LimitManualMoveStep(ref bool __result)
    {
        return GateManualMove(ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GoalManualMove), nameof(GoalManualMove.TryAltMove))]
    private static bool LimitManualAltMoveStep(ref bool __result)
    {
        return GateManualMove(ref __result);
    }

    private static bool GateManualMove(ref bool __result)
    {
        if (Phase != CombatPhase.Executing || !_pcActedThisRound) {
            return true;
        }

        if (Time.unscaledTime >= _nextMoveBlockTrace) {
            _nextMoveBlockTrace = Time.unscaledTime + 0.25f;
            TraceTurn("reject-move-already-acted", EClass.player.nextMove.ToString());
        }
        EClass.player.nextMove = Vector2.zero;
        __result = false;
        return false;
    }
}