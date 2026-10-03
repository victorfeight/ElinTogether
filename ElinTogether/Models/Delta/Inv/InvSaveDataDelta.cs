using ElinTogether.Net;
using MessagePack;

namespace ElinTogether.Models;

[MessagePackObject]
public class InvSaveDataDelta : ElinDelta
{
    [Key(0)]
    public required RemoteCard Container { get; init; }

    [Key(1)]
    public required LZ4Bytes Data { get; init; }

    protected override void OnApply(ElinNetBase net)
    {
        // Floating windows share a prefab ID. The item, not that UI ID, owns
        // vanilla's saved container settings (including while its UI is closed).
        if (Container.Find() is not { isChara: false, isDestroyed: false } container ||
            container.trait is TraitChestMerchant ||
            Data.Decompress<Window.SaveData>() is not { } data) return;

        var saved = container.c_windowSaveData;
        if (saved is null) {
            // No local layout exists yet; use the sender's native initialized
            // defaults once. Later settings updates never move/resize it.
            saved = IO.DeepCopy(data);
            saved.open = false;
            container.c_windowSaveData = saved;
        } else {
            ContainerWindowSettings.CopyShared(saved, data);
        }

        foreach (var layer in LayerInventory.listInv) {
            foreach (var inv in layer.invs) {
                if (inv.owner.Container != container ||
                    !ReferenceEquals(inv.window.saveData, saved)) continue;
                inv.RefreshWindow();
                var shared = saved.sharedType == ContainerSharedType.Shared;
                inv.window.buttonShared.image.sprite = shared ? EMono.core.refs.icons.shared : EMono.core.refs.icons.personal;
                inv.window.buttonShared.tooltip.lang = shared ? "hintShared" : "hintPrivate";
            }
        }

        // The host persists the actual item's native field and relays the
        // accepted update to other clients, without re-running UI actions.
        if (net is ElinNetHost) net.Delta.AddRemote(this);
    }
}
