using ElinTogether.Net;
using ElinTogether.Patches;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CharaSleepDelta : ElinDelta
{
    [Key(0)]
    public required int Power { get; init; }

    [Key(1)]
    public required int Days { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (net.IsHost) {
            return;
        }

        // A watching/resuming client is a mirror. The host's party sleep already
        // handles its companion; local rewards here would never reach that profile.
        if (net is ElinNetClient { ControlMode: not PlayerControlMode.Human }) {
            SleepSynchronizationContext.CloseSleepLayerIfOpen();
            return;
        }

        if (pc.isDead) {
            return;
        }

        EmpLog.Debug("Applying host sleep {SleepPower}", Power);

        using var _ = Simulate();
        pc.OnSleep(Power, Days, pc.pos.IsSunLit);
        player.DreamSpell();

        pc.conSleep?.Kill();
        SleepSynchronizationContext.CloseSleepLayerIfOpen();
    }
}