using System;
using System.Collections.Generic;
using ElinTogether.Net;
using ElinTogether.Helper;
using ElinTogether.Patches;
using HarmonyLib;

namespace ElinTogether.Models;

// Optional TpMagicAppendix adapter. Keep its spell formulas and native effects;
// only change who executes them and publish outcomes through existing MP channels.
internal static class TpSpellBridge
{
    internal const int Return = 222223101;
    internal const int Teleport = 222223102;
    internal const int Sense = 222223103;
    internal const int Brainwash = 222223810;
    private static Type? _mod;
    private static readonly Dictionary<int, string> Aliases = new() {
        [222223001] = "TpDemolition", [222223002] = "TpDrilling", [222223003] = "TpPullingRug",
        [222223004] = "TpCollectAll", [222223005] = "TpGathering",
        [Return] = "TpReturnG", [Teleport] = "TpTeleportG", [Sense] = "TpSenceObject",
        [222223201] = "TpClearSky", [222223202] = "TpBringRain", [222223203] = "TpCreateWind",
        [222223801] = "TpForceOvulation", [222223802] = "TpForceMilking",
        [222223803] = "TpForceSqueeze", [222223804] = "TpCultivate", [Brainwash] = "TpBrainwash",
        [222223999] = "TpEternalForceBlizzard",
    };
    internal static bool IsManagedId(int id) => Aliases.ContainsKey(id);
    internal static bool IsManaged(Act act) => Aliases.TryGetValue(act.id, out var alias) &&
        act.source.alias == alias && (_mod ??= AccessTools.TypeByName("TpMagicAppendix.MagicAppendix")) is not null;
    internal static Guid RequestId;
    internal static Execution? Current { get; private set; }
    internal static bool AllowsHostMovement => Current?.ActId == Teleport &&
        NetSession.Instance.Connection is ElinNetHost;

    internal sealed class Execution : IDisposable
    {
        internal readonly int ActId;
        private readonly Guid _request = RequestId;
        private readonly List<Card> _generated = [];
        internal void ObserveGeneration(Card card) => _generated.Add(card);
        private readonly Chara _actor;
        private readonly Chara _previousPC;
        private readonly Execution? _previous;
        private readonly Map _map;
        private readonly int _zone;
        private readonly IDisposable _messages;
        private readonly Action? _endTerrain;
        private readonly Dictionary<Chara, Point> _positions = [];
        private readonly Dictionary<Chara, int> _masters = [];

        internal Execution(Act act)
        {
            ActId = act.id;
            _actor = Act.CC;
            _previousPC = EClass.player.chara;
            _previous = Current;
            _map = EClass._map;
            _zone = EClass._zone.uid;
            _messages = MsgRelayContext.RedirectTo(_actor);
            if (ActId == Teleport)
                foreach (var chara in _map.charas) _positions[chara] = chara.pos.Copy();
            if (ActId == Brainwash)
                foreach (var chara in _map.charas) _masters[chara] = chara.c_uidMaster;
            if (ActId is 222223001 or 222223002 or 222223003)
                _endTerrain = PathTerrainSync.CaptureMap(_map);
            // Tp's IsPC guards and its native helpers refer to this singleton.
            // This scope is synchronous; Return deliberately leaves returnInfo
            // on the host Player, so shared travel and its dialog remain host-owned.
            EClass.player.chara = _actor;
            Current = this;
        }

        public void Dispose()
        {
            EClass.player.chara = _previousPC;
            Current = _previous;
            _endTerrain?.Invoke();
            _messages.Dispose();
        }

        internal void Publish()
        {
            if (NetSession.Instance.Connection is not ElinNetHost host ||
                EClass._map != _map || EClass._zone.uid != _zone) return;
            var result = new TpSpellResultDelta { Id = _request, ZoneUid = _zone, ActId = ActId, Owner = _actor };
            foreach (var pair in _positions) {
                if (!pair.Key.isDestroyed && !pair.Key.isDead && pair.Key.IsInActiveMap &&
                    !pair.Key.pos.Equals(pair.Value))
                    result.Moves.Add(new TpSpellResultDelta.Move { Card = pair.Key, Pos = pair.Key.pos });
            }
            foreach (var pair in _masters) {
                if (!pair.Key.isDestroyed && pair.Key.c_uidMaster != pair.Value)
                    result.Characters.Add(CharaStateSnapshot.Create(pair.Key));
            }
            if (ActId is 222223201 or 222223202 or 222223203 or 222223999)
                result.Weather = WeatherStateSnapshot.Create();
            if (ActId == Sense) {
                var cells = new List<int>();
                _map.ForeachCell(cell => {
                    if (cell.isSeen && (cell.HasObj || cell.FirstThing != null))
                        cells.Add(cell.z * _map.Size + cell.x);
                });
                host.Delta.AddRemote(new MapRevealDelta { ZoneUid = _zone, Size = _map.Size, Cells = cells.ToArray() });
            }
            host.Delta.AddRemote(result);
            if (_actor != _previousPC && result.Moves.Count != 0) {
                EClass.screen.FocusPC();
                EClass.screen.RefreshPosition();
            }
            EmpLog.Information("TpSpell outcomes: id={Id} act={Act} actor={Actor} zone={Zone} moves={Moves} minions={Minions} products={Products}",
                _request, ActId, _actor.uid, _zone, result.Moves.Count, result.Characters.Count, _generated.Count);
            foreach (var card in _generated)
                EmpLog.Debug("TpSpell product: id={Id} uid={Uid} item={Item} reference={Reference} num={Num} destroyed={Destroyed}",
                    _request, card.uid, card.id, card.c_idRefCard, card.Num, card.isDestroyed);
        }
    }
}
