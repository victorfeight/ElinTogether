using ElinTogether.Models;
using ElinTogether.Net;
using HarmonyLib;

namespace ElinTogether.Patches;

[HarmonyPatch(typeof(ActEffect), nameof(ActEffect.ProcAt))]
internal static class ClientWandSummonEvent
{
    // Keep local rod animation, charge prediction and experience; creatures arrive
    // exclusively through the host's existing generation/placement deltas.
    [HarmonyPrefix]
    internal static bool Before(EffectId id, Card cc, ActRef actRef)
    {
        return id != EffectId.Summon || !cc.IsPC ||
               actRef.refThing?.trait is not TraitRod ||
               NetSession.Instance.Connection is not ElinNetClient || ElinDelta.IsApplying;
    }
}
