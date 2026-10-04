using ElinTogether.Models;
using ElinTogether.Patches;
using ElinTogether.Net;

public class Card
{
    public int c_lockLv,c_priceAdd,LV; public bool c_lockedHard,isLostProperty;
    public Trait trait=null!;
    public int uid, encLV; public bool isDestroyed, IsEquipmentOrRangedOrAmmo = true, IsWeapon, IsAmmo;
    public Card? parent;
    public virtual void ModEncLv(int amount) { encLV += amount; }
    public void SetEncLv(int value) => ModEncLv(value - encLV);
    public Thing AddThing(Thing item, bool stack, int x, int y) { item.parent = this; CardEnhancementEvent.AfterTransfer(this, item); return item; }
}
public class Trait {
    public Card owner=null!; public int UnlockCalls;
    public void OnLockOpen(){UnlockCalls++;owner.c_lockLv=0;if(owner.c_lockedHard){owner.c_lockedHard=false;owner.c_priceAdd=0;}owner.isLostProperty=false;CardLockStateEvent.Unlocked(this);}
}
public class TraitChestPractice:Trait { public void OnSimulateHour(){} }
// Native math/rendering are boundaries. The installed DLL call graph is checked
// separately; these counters verify the MP receiver uses that native operation.
public class Thing : Card
{
    public int Armor, Damage, LinkedArmor, MaterialRefreshes, NativeCalls;
    public bool Equipped;
    public override void ModEncLv(int amount)
    {
        CardEnhancementEvent.Before(this, out var before);
        NativeCalls++; MaterialRefreshes += 2; encLV += amount;
        if (IsWeapon || IsAmmo) Damage += amount;
        else { Armor += amount * 2; if (Equipped) LinkedArmor += amount * 2; }
        CardEnhancementEvent.After(this, before);
    }
}
public static class EClass { public static Core core = new(); public static Game game = new(); }
public class Core { public bool IsGameStarted = true; }
public class Game { public bool isLoading; }
public static class LayerInventory { public static List<int> Dirty = []; public static void SetDirty(Thing item) => Dirty.Add(item.uid); }
namespace ElinTogether { public static class EmpLog { public static void Debug(string message, params object?[] args) { } } }
namespace ElinTogether.Models
{
    public abstract class ElinDelta { protected abstract void OnApply(ElinNetBase net); public void Apply(ElinNetBase net) => OnApply(net); }
    public class RemoteCard { public int Uid; public Card? Find() => CardCache.Items.GetValueOrDefault(Uid); public static implicit operator RemoteCard(Card item) => new() { Uid = item.uid }; }
    public static class CardCache { public static Dictionary<int, Card> Items = []; public static bool Contains(Card item) => Items.GetValueOrDefault(item.uid) == item; }
}
namespace ElinTogether.Net
{
    public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; }
    public class ElinNetBase { public bool IsHost => this is ElinNetHost; public Queue Delta = new(); }
    public class ElinNetHost : ElinNetBase { }
    public class ElinNetClient : ElinNetBase { }
    public class Queue { public List<ElinDelta> Items = []; public void AddRemote(ElinDelta delta) => Items.Add(delta); }
}
namespace ElinTogether.Patches
{
    public static class CharaProgressCompleteEvent { public static bool Active; public static List<ElinDelta> Packed = []; public static bool ShouldPack(bool remoteOnly) => Active; public static void Pack(ElinDelta delta) => Packed.Add(delta); }
}
namespace MessagePack { public class MessagePackObjectAttribute : Attribute { } public class KeyAttribute(int key) : Attribute { } }
namespace HarmonyLib {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPatch : Attribute { public HarmonyPatch() { } public HarmonyPatch(Type type, string method, params Type[] args) { } }
    public class HarmonyPrefix : Attribute { } public class HarmonyPostfix : Attribute { }
}
