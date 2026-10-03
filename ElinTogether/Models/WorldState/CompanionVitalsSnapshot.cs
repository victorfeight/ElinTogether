using MessagePack;

namespace ElinTogether.Models;

/// <summary>Host-simulated needs while a connected player takes a break.</summary>
[MessagePackObject]
public sealed class CompanionVitalsSnapshot
{
    [Key(0)] public int Stamina { get; init; }
    [Key(1)] public int Mana { get; init; }
    [Key(2)] public int Sleepiness { get; init; }
    [Key(3)] public int Sanity { get; init; }
    [Key(4)] public int Depression { get; init; }
    [Key(5)] public int Bladder { get; init; }
    [Key(6)] public int Hygiene { get; init; }

    public static CompanionVitalsSnapshot Capture(Chara actor) => new() {
        Stamina = actor.stamina.value,
        Mana = actor.mana.value,
        Sleepiness = actor.sleepiness.value,
        Sanity = actor.SAN.value,
        Depression = actor.depression.value,
        Bladder = actor.bladder.value,
        Hygiene = actor.hygiene.value,
    };

    public void Apply(Chara actor)
    {
        // Assign resolved values, not Mod(): that would repeat gameplay effects
        // such as mana backfire and hunger/need transition messages.
        actor.stamina.value = Stamina;
        actor.mana.value = Mana;
        actor.sleepiness.value = Sleepiness;
        actor.SAN.value = Sanity;
        actor.depression.value = Depression;
        actor.bladder.value = Bladder;
        actor.hygiene.value = Hygiene;
    }
}
