using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class CharaStaminaDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Chara { get; init; }

    [Key(1)]
    public required int Stamina { get; init; }

    [Key(2)] public int StaminaRecovery { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        if (Chara.Find() is not Chara { IsPC: false } chara) {
            return;
        }

        // Late human packets must not overwrite an AI-controlled character.
        if (net is ElinNetHost host && (!host.AcceptsPlayerInput(OriginPeer) ||
            !host.ActiveRemoteCharas.TryGetValue(OriginPeer, out var sender) || sender != chara)) return;
        if (StaminaRecovery < 0) return;

        chara.stamina.value = Stamina;
        PersonalStaminaRecovery.Store(chara, StaminaRecovery);
    }
}
