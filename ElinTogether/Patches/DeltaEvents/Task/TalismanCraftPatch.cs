using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(TraitCrafter), nameof(TraitCrafter.Craft))]
internal static class TalismanCraftPatch
{
    internal static Chara? Receiver { get; private set; }

    internal sealed class Context
    {
        internal required Chara? PreviousReceiver;
        internal required Chara PreviousPC;
        internal required Chara Actor;
        internal required Thing Weapon;
    }

    [HarmonyPrefix]
    internal static void Before(TraitCrafter __instance, AI_UseCrafter ai, out Context? __state)
    {
        __state = null;
        if (NetSession.Instance.Connection is not ElinNetHost ||
            __instance.GetSource(ai)?.type != nameof(TraitCrafter.MixType.Talisman) ||
            ai.ings.Count == 0) return;

        __state = new Context {
            PreviousReceiver = Receiver, PreviousPC = EClass.pc,
            Actor = ai.owner, Weapon = ai.ings[0],
        };
        Receiver = ai.owner;
        // Vanilla evaluates talisman feats and formats the confirmation using pc.
        EClass.game.player.chara = ai.owner;
    }

    [HarmonyPostfix]
    internal static void After(Context? __state)
    {
        if (__state is null || NetSession.Instance.Connection is not ElinNetHost host ||
            __state.Weapon is not { isDestroyed: false, ammoData: { } ammo } weapon) return;

        var delta = new CardAmmoDelta { Card = weapon, Ammo = LZ4Bytes.Create(ammo), Count = weapon.c_ammo };
        if (CharaProgressCompleteEvent.ShouldPack(false)) CharaProgressCompleteEvent.Pack(delta);
        else host.Delta.AddRemote(delta);
        EmpLog.Debug("Crafted weapon spell: actor {ActorUid}, weapon {WeaponUid}, spell {Spell}, charges {Charges}",
            __state.Actor.uid, weapon.uid, ammo.refVal, weapon.c_ammo);
    }

    [HarmonyFinalizer]
    internal static void Restore(Context? __state)
    {
        if (__state is null) return;
        EClass.game.player.chara = __state.PreviousPC;
        Receiver = __state.PreviousReceiver;
    }
}
