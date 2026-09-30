using UnityEngine;

namespace ElinTogether.Models;

// Owner-only numeric portion of vanilla TraitAltar._OnOffer. Physical sacrifice,
// takeover and item consumption are resolved once by the host. Use the existing
// persisted award/ack path so potential, rounding and level-ups run on the owner.
internal sealed class FaithOfferingProgression : EClass
{
    internal static void Apply(Chara actor, int value, bool contraband)
    {
        var cap = Mathf.Max(actor.Evalue(306), 1);
        var piety = actor.elements.GetOrCreateElement(85);
        var before = piety.Value;
        if (piety.vBase < cap) {
            actor.elements.ModExp(85, value * 2 / 3);
            if (piety.vBase >= cap) actor.elements.SetBase(85, cap);
        }
        var phase = piety.vBase < cap ? Mathf.Clamp(piety.vBase * 100 / cap / 25, 0, 3) : 4;
        if (phase == 4 || piety.Value != before) Msg.Say("piety" + phase, actor, actor.faith.TextGodGender);
        if (piety.Value > cap * 8 / 10) actor.elements.ModExp(306, value / 5);
        actor.RefreshFaithElement();
        if (actor.faith.GetGiftRank() != -1) actor.faith.Talk("like");
        if (contraband) player.ModKarma(-1);
    }
}
