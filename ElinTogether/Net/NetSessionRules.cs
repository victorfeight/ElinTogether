using MessagePack;

namespace ElinTogether.Net;

[MessagePackObject]
public class NetSessionRules
{
    [Key(0)]
    public required bool UseSharedSpeed { get; set; }

    [Key(1)]
    public required bool UseTurnBasedCombat { get; set; }

    [Key(2)]
    public bool AllowMoongateTheft { get; set; }

    public static NetSessionRules Default => new() {
        AllowMoongateTheft = EmpConfig.Server.AllowMoongateTheft.Value,
        UseSharedSpeed = EmpConfig.Server.SharedAverageSpeed.Value,
        UseTurnBasedCombat = EmpConfig.Server.TurnBasedCombat.Value,
    };
}