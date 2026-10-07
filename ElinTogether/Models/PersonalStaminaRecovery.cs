using System;

namespace ElinTogether.Models;

// Player owns the live human value. A character-owned copy survives AI control
// and save probes; the existing profile checkpoint transports it to the host.
internal static class PersonalStaminaRecovery
{
    internal const string Key = "emp_stamina_recovery";

    internal static int Saved(Chara actor, int fallback = 0) =>
        actor.isDead ? 0 : Math.Max(0, actor.GetInt(Key, fallback));

    internal static void Store(Chara actor, int value) => actor.SetInt(Key, Math.Max(0, value));

    internal static int Current(Chara actor) => ReferenceEquals(actor, EClass.pc) && !PlayerControl.WatchingOwnCharacter
        ? Math.Max(0, EClass.player.staminaRecovery) : Saved(actor);

    internal static void Mirror(Chara actor, int value)
    {
        Store(actor, value);
        if (ReferenceEquals(actor, EClass.pc)) EClass.player.staminaRecovery = Math.Max(0, value);
    }

    internal static void RecoveredByCompanion(Chara actor, int gained)
    {
        var credit = Saved(actor);
        if (credit > 0 && gained > 0) Store(actor, credit - Math.Min(credit, gained));
    }
}
