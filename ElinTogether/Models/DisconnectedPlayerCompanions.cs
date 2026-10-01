using System.Collections.Generic;
using ElinTogether.Common;
using ElinTogether.Elements;
using ElinTogether.Net;

namespace ElinTogether.Models;

// Owned by one host connection. A saved role flag alone never grants MP AI control.
internal sealed class DisconnectedPlayerCompanions : EClass
{
    private readonly HashSet<Chara> _actors = [];
    internal IEnumerable<Chara> Actors => _actors;
    internal bool Contains(Chara actor) => _actors.Contains(actor);

    internal static bool AllowsTakeover(bool enabled, bool ready, bool hosting, string reason) =>
        enabled && ready && hosting && reason is not (
            EmpDisconnectInfo.HostShutdown or EmpDisconnectInfo.HostKick or
            EmpDisconnectInfo.HostReconnectRequest or EmpDisconnectInfo.JoinWhileConnected or
            EmpDisconnectInfo.NetSessionInitialize or EmpDisconnectInfo.NewHost or
            EmpDisconnectInfo.InvalidSource or EmpDisconnectInfo.InvalidZone or
            EmpDisconnectInfo.VersionMismatch or EmpDisconnectInfo.ActMappingMismatch);

    internal bool TryBegin(Chara actor)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host ||
            host.ActiveRemoteCharas.ContainsValue(actor) || actor.IsPC || actor.isDead ||
            actor.party != pc.party || actor.currentZone != pc.currentZone ||
            actor.parent is not Zone || actor.host is not null || actor.ride is not null ||
            actor.parasite is not null) return false;
        StopActions(actor);
        actor.SetBool(SoloCompanions.Key, true);
        _actors.Add(actor);
        actor.RefreshFaithElement();
        actor.ChooseNewGoal();
        EmpLog.Information("Disconnected player {Uid}: human control -> companion AI", actor.uid);
        return true;
    }

    internal void Release(Chara actor)
    {
        if (!_actors.Remove(actor)) return;
        Dismount(actor);
        StopActions(actor);
        actor.SetBool(SoloCompanions.Key, false);
        EmpLog.Information("Disconnected player {Uid}: companion AI stopped", actor.uid);
    }

    internal static void Dismount(Chara actor)
    {
        if (actor.host is { } rider) ActRide.Unride(rider, actor, talk: false);
        if (actor.ride is not null) ActRide.Unride(actor, parasite: false, talk: false);
        if (actor.parasite is not null) ActRide.Unride(actor, parasite: true, talk: false);
    }

    internal static void StopActions(Chara actor)
    {
        if (actor.ai is GoalRemote remote) {
            // Cancel, do not execute a tool switch queued by the departed owner.
            remote.PendingHeld = null;
            remote.HaltChildAct();
        }
        actor.SetNoGoal();
        actor.enemy = null;
    }
}
