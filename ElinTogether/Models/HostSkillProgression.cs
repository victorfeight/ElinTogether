using System;
using ElinTogether.Helper;
using ElinTogether.Net;

namespace ElinTogether.Models;

// For host-resolved actions only. The owner runs vanilla potential, parent
// attribute XP and level-ups, through the existing persisted award/ack channel.
// The scope ends before delivery so receiving an award cannot capture itself.
internal sealed class HostSkillProgression : IDisposable
{
    private static HostSkillProgression? _current;
    private readonly HostSkillProgression? _previous;
    private readonly Chara _actor;
    private readonly bool _host;

    private HostSkillProgression(Chara actor, bool host)
    {
        _previous = _current;
        _actor = actor;
        _host = host;
        _current = this;
    }

    internal static HostSkillProgression? Begin(ElinNetBase net, Card owner) =>
        owner is Chara actor && (!net.IsHost || actor.IsRemotePlayer)
            ? new(actor, net.IsHost) : null;

    internal static bool AllowExperience(ElementContainer container, int skill, float amount, bool chain)
    {
        if (_current is not { } scope || container.Card != scope._actor) return true;

        // Result replay is for presentation, never another XP roll.
        if (!scope._host) return false;
        if (chain || amount <= 0 || scope._actor.isDead ||
            !container.dict.TryGetValue(skill, out var element) || !element.CanGainExp) return true;

        var award = OwnerProgressionAwards.Record(scope._actor, Guid.NewGuid(), skill, amount);
        EmpLog.Debug("Host skill XP awarded: actor {ActorUid}, skill {Skill}, raw {Amount}, award {AwardId}",
            scope._actor.uid, skill, amount, award.Id);
        return false;
    }

    public void Dispose() => _current = _previous;
}
