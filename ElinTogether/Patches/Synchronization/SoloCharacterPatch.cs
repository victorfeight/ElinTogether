using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

internal static class SoloCharacterPatch
{
    // Offline selection and saving must survive multiplayer session teardown.
    private static readonly Harmony Harmony = new($"{ModInfo.Guid}.solo-character");

    internal static void Apply()
    {
        if (HarmonyLib.Harmony.HasAnyPatches(Harmony.Id)) return;
        Harmony.Patch(AccessTools.Method(typeof(Game), nameof(Game.OnGameInstantiated)),
            prefix: new HarmonyMethod(typeof(SoloCharacterPatch), nameof(SelectBeforeBinding)));
        Harmony.Patch(AccessTools.Method(typeof(Player), nameof(Player.OnBeforeSave)),
            postfix: new HarmonyMethod(typeof(SoloCharacterPatch), nameof(CaptureBeforeSerialization)));
        Harmony.Patch(AccessTools.Method(typeof(Game), nameof(Game.OnLoad)),
            postfix: new HarmonyMethod(typeof(SoloCharacterPatch), nameof(RestoreAbilityLayout)));
        Harmony.Patch(AccessTools.Method(typeof(ReligionManager), nameof(ReligionManager.OnLoad)),
            postfix: new HarmonyMethod(typeof(SoloCharacterPatch), nameof(RestoreFaithAfterLookup)));
    }

    private static void SelectBeforeBinding() => SoloCharacterSelection.ApplyPending();

    private static void CaptureBeforeSerialization()
    {
        if (FaithTransactions.Actor is not null) return;
        if (NetSession.Instance.Connection is ElinNetClient client) {
            client.CheckpointPersonalProfile();
            return;
        }
        InvPlaceAbilityDelta.StoreLayout(EClass.pc, CardAddThingEvent.CollectLayout());
        SoloCharacterSelection.CaptureCurrent();
    }

    private static void RestoreAbilityLayout()
    {
        if (NetSession.Instance.Connection is null && EClass.pc.GetInt(SoloCharacterSelection.OriginalHostKey) > 0)
            InvPlaceAbilityDelta.RestoreLayout(EClass.pc);
    }

    private static void RestoreFaithAfterLookup()
    {
        if (NetSession.Instance.Connection is null) PersonalFaith.RestoreJoinedPlayer();
    }
}
