using System;
using System.Collections.Generic;
using System.Linq;
using ElinTogether.Net;
using ElinTogether.Patches;
using ElinTogether.Helper;

namespace ElinTogether.Models;

// One journey for the shared map. Vanilla owns the countdown, destination UI,
// restrictions and MoveZone; this owns the human who requested that journey.
internal static class SharedTravel
{
    internal sealed class Journey(Chara caster, Chara driver, int zone)
    {
        internal readonly Chara Caster = caster;
        internal readonly Chara Driver = driver;
        internal readonly int Zone = zone;
        internal Player.ReturnInfo? Native;
        internal LayerList? Dialog;
    }
    internal static Journey? Current;
    private static readonly HashSet<(int, Guid)> Requests = [];
    internal static bool IsTravel(EffectId effect) => effect is EffectId.Return or EffectId.Evac;
    internal static bool IsTravel(Act act) => act.id > 0 && EClass.sources.elements.map.TryGetValue(act.id, out var row) &&
        row.proc.FirstOrDefault() is "Return" or "Evac";
    internal static bool IsTravel(Card? card) => card?.trait is TraitScrollStatic scroll && IsTravel(scroll.idEffect);

    internal static bool IsHuman(ElinNetHost host, Chara actor) => !actor.isDead && actor.IsInActiveMap &&
        !host.IsCompanionControlled(actor) && (actor == NetSession.Instance.Player ||
        host.ActiveRemoteCharas.Any(p => p.Value == actor && host.AcceptsPlayerInput(p.Key)));

    internal static bool Accept(ElinNetHost host, int peer, Chara actor, Guid request, int zone) =>
        request != Guid.Empty && zone == EClass._zone.uid && host.AcceptsPlayerInput(peer) &&
        host.ActiveRemoteCharas.TryGetValue(peer, out var owner) && owner == actor &&
        IsHuman(host, actor) && Requests.Add((peer, request));

    internal static bool CanBegin(Chara actor)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host || !IsHuman(host, actor)) return false;
        Validate();
        if (Current is { } pending && pending.Caster != actor) {
            Tell(actor, "Another player is already preparing shared travel.");
            return false;
        }
        return true;
    }

    internal static Journey Begin(Chara actor) => Current ??= new(actor, EClass.pc, EClass._zone.uid);

    internal static void ResetNetwork() { Cancel("session ended"); Requests.Clear(); }

    internal static void Adopt(Journey journey)
    {
        if (Current != journey) return;
        journey.Native = EClass.player.returnInfo;
        if (journey.Native == null) { Cancel("cancelled"); return; }
        Announce($"{journey.Caster.NameSimple} started shared travel. " +
            (journey.Native.askDest ? "The host chooses the destination." : "Preparing to depart."));
        EmpLog.Information("Shared travel started: caster {Caster}, driver {Driver}, zone {Zone}, evac {Evac}",
            journey.Caster.uid, journey.Driver.uid, journey.Zone, journey.Native.isEvac);
    }

    // Called at the native turn/UI boundaries, not by a new timer.
    internal static bool Validate()
    {
        if (Current is not { } journey) return false;
        if (NetSession.Instance.Connection is not ElinNetHost host || EClass.pc != journey.Driver ||
            EClass._zone.uid != journey.Zone || !IsHuman(host, journey.Caster) || journey.Driver.isDead) {
            Cancel("caster, control or map changed"); return false;
        }
        if (journey.Native != null && EClass.player.returnInfo == null) {
            Cancel("cancelled"); return false;
        }
        return true;
    }

    internal static void BeforeTick(Chara actor)
    {
        if (Current is not { } journey || actor != journey.Driver || !Validate()) return;
        if (EClass.player.returnInfo is { turns: <= 1 } && journey.Caster.burden.GetPhase() == 4 && !EClass.debug.ignoreWeight)
            Cancel("the caster is too heavily burdened");
        // The driver's overweight and survival restrictions remain in vanilla Tick.
    }

    internal static void Cancel(string reason)
    {
        var journey = Current;
        if (journey == null) return;
        Current = null;
        EClass.player.returnInfo = null;
        if (journey.Dialog != null && journey.Dialog) journey.Dialog.Close();
        Announce($"Shared travel ended: {reason}.");
        EmpLog.Information("Shared travel ended: caster {Caster}, zone {Zone}, reason {Reason}", journey.Caster.uid, journey.Zone, reason);
    }

    internal static void Tell(Chara actor, string text)
    {
        using var messages = MsgRelayContext.RedirectTo(actor);
        Msg.Say(text);
    }

    private static void Announce(string text)
    {
        if (NetSession.Instance.Connection is not ElinNetHost host) return;
        using (MsgRelayContext.RedirectTo(0)) Msg.Say(text);
        host.Delta.AddRemote(new MsgSayDelta { Text = text, R = 1, G = 1, B = 1, A = 1 });
    }

    [ElinPreLoad] private static void Reset(GameIOContext _) { Current = null; Requests.Clear(); }
}
