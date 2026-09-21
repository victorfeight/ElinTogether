using System.Linq;
using BepInEx;
using HarmonyLib;

namespace TpHugeShippingContainer;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class ModHugeShippingContainer : BaseUnityPlugin
{
    public const string PluginGuid = "net.kuronekotei.huge_shipping_container";
    public const string PluginName = "HugeShippingContainer";
    public const string PluginVersion = "1.0.1";

    private void Awake()
    {
        new Harmony(PluginGuid).PatchAll();
    }
}

internal static class ShippingContainerSize
{
    internal const int Width = 16;
    internal const int Height = 10;

    internal static void Resize(Thing? thing)
    {
        thing?.things?.SetSize(Width, Height);
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.OnGameInstantiated))]
internal static class GameOnGameInstantiatedPatch
{
    [HarmonyPrefix]
    private static void UpdateSourceSize()
    {
        var row = EClass.sources.things.rows?.FirstOrDefault(row => row.id == "container_shipping");
        var trait = row?.trait;
        if (trait is null) {
            return;
        }

        for (var i = 0; i + 2 < trait.Length; i++) {
            if (trait[i] != "ShippingChest") {
                continue;
            }

            trait[i + 1] = ShippingContainerSize.Width.ToString();
            trait[i + 2] = ShippingContainerSize.Height.ToString();
            return;
        }
    }

    [HarmonyPostfix]
    private static void ResizeSharedContainer()
    {
        ShippingContainerSize.Resize(EClass.game?.cards?.container_shipping);
    }
}

[HarmonyPatch(typeof(LayerInventory), nameof(LayerInventory.CreateContainer), typeof(Card))]
internal static class LayerInventoryCreateContainerPatch
{
    [HarmonyPrefix]
    private static void ResizePlacedShippingChest(Card owner)
    {
        if (owner is Thing thing && thing.id == "container_shipping" && thing.trait is TraitShippingChest) {
            ShippingContainerSize.Resize(thing);
        }
    }
}
