using System;
using System.Collections.Generic;
using System.Reflection;
using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch]
internal static class SharedTravelPatch
{
    internal static bool Choosing;
    internal sealed class EffectScope : IDisposable
    {
        private readonly Chara _pc = EClass.pc;
        private readonly SharedTravel.Journey _journey;
        internal EffectScope(Chara actor) { _journey = SharedTravel.Begin(actor); EClass.player.chara = actor; }
        public void Dispose() { EClass.player.chara = _pc; SharedTravel.Adopt(_journey); }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(ActEffect), nameof(ActEffect.Proc), typeof(EffectId), typeof(int),
        typeof(BlessedState), typeof(Card), typeof(Card), typeof(ActRef))]
    internal static bool Effect(EffectId id, Card cc, out EffectScope? __state)
    {
        __state = null;
        if (!SharedTravel.IsTravel(id) || NetSession.Instance.Connection == null) return true;
        if (NetSession.Instance.Connection is ElinNetClient) return false;
        if (cc is not Chara actor) return true;
        if (!SharedTravel.IsHuman((ElinNetHost)NetSession.Instance.Connection, actor))
            return !actor.IsPC; // Ordinary NPCs retain fallback; a resting host must not initiate travel.
        if (!SharedTravel.CanBegin(actor)) return false;
        __state = new(actor); // Only the synchronous effect sees this PC, never Tick or the dialog.
        return true;
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(ActEffect), nameof(ActEffect.Proc), typeof(EffectId), typeof(int),
        typeof(BlessedState), typeof(Card), typeof(Card), typeof(ActRef))]
    internal static void EndEffect(EffectScope? __state, Exception? __exception)
    {
        __state?.Dispose();
        if (__state != null && __exception != null) SharedTravel.Cancel("effect failed");
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TraitScrollStatic), nameof(TraitScrollStatic.OnRead))]
    internal static bool Read(TraitScrollStatic __instance, Chara c, out IDisposable? __state)
    {
        __state = null;
        if (!SharedTravel.IsTravel(__instance.idEffect) || NetSession.Instance.Connection == null) return true;
        // The existing AI_Read task executes on the host. Do not roll failure,
        // consume a scroll or give Literacy XP again on a predicting/replaying client.
        if (NetSession.Instance.Connection is ElinNetClient) return false;
        if (!SharedTravel.IsHuman((ElinNetHost)NetSession.Instance.Connection, c)) return !c.IsPC;
        if (!SharedTravel.CanBegin(c)) return false;
        __state = ElinDelta.Simulate();
        return true;
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(TraitScrollStatic), nameof(TraitScrollStatic.OnRead))]
    internal static void EndRead(IDisposable? __state) => __state?.Dispose();

    [HarmonyPrefix, HarmonyPatch(typeof(Chara), nameof(Chara.Tick))]
    internal static void Tick(Chara __instance) => SharedTravel.BeforeTick(__instance);

    [HarmonyPrefix, HarmonyPatch(typeof(AM_Adv), nameof(AM_Adv._OnUpdateInput))]
    internal static void BeforeInput(out bool __state)
    {
        __state = Choosing;
        Choosing = SharedTravel.Current != null && SharedTravel.Validate() && EClass.player.returnInfo is { askDest: true };
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(AM_Adv), nameof(AM_Adv._OnUpdateInput))]
    internal static void AfterInput(bool __state) => Choosing = __state;

    [HarmonyPrefix, HarmonyPatch(typeof(Chara), nameof(Chara.MoveZone), typeof(Zone), typeof(ZoneTransition))]
    internal static void Moving(Chara __instance)
    {
        if (SharedTravel.Current is { } journey && __instance == journey.Driver)
            SharedTravel.Cancel(EClass.player.returnInfo is { turns: <= 0 } ? "departing" : "map changed");
    }
}

// Keep the native Return list (and BetterReturn's list provider), guarding only
// its deferred callbacks against a cancelled/replaced multiplayer journey.
[HarmonyPatch]
internal static class SharedTravelDialogPatch
{
    internal static MethodBase TargetMethod() => AccessTools.Method(typeof(LayerList), nameof(LayerList.SetList2)).MakeGenericMethod(typeof(Zone));
    [HarmonyPrefix]
    internal static void Bind(LayerList __instance, ref Action<Zone, ItemGeneral> onClick)
    {
        if (!SharedTravelPatch.Choosing || SharedTravel.Current is not { Native: { isEvac: false, uidDest: 0 }, Dialog: null } journey) return;
        journey.Dialog = __instance;
        var original = onClick;
        onClick = (zone, item) => {
            if (SharedTravel.Current != journey || !SharedTravel.Validate()) return;
            original(zone, item);
            journey.Native = EClass.player.returnInfo;
        };
    }
}

[HarmonyPatch(typeof(Layer), nameof(Layer.SetOnKill))]
internal static class SharedTravelDialogClosePatch
{
    [HarmonyPrefix]
    internal static void Bind(Layer __instance, ref Action action)
    {
        if (SharedTravel.Current is not { } journey || journey.Dialog != __instance) return;
        var original = action;
        action = () => {
            if (SharedTravel.Current != journey) return;
            original();
            journey.Dialog = null;
            SharedTravel.Validate();
        };
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.Save))]
internal static class SharedTravelSavePatch
{
    internal sealed record Saved(SharedTravel.Journey Journey, Player Player, Player.ReturnInfo? Info);
    [HarmonyPrefix]
    internal static void Before(out Saved? __state)
    {
        __state = null;
        if (SharedTravel.Current is not { } journey) return;
        __state = new(journey, EClass.player, EClass.player.returnInfo);
        // Preserve the live journey but never persist a session-owned countdown.
        EClass.player.returnInfo = null;
    }
    [HarmonyFinalizer]
    internal static void After(Saved? __state)
    {
        if (__state != null && SharedTravel.Current == __state.Journey && EClass.player == __state.Player)
            EClass.player.returnInfo = __state.Info;
    }
}
