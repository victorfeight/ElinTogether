using ElinTogether.Models;
using Newtonsoft.Json;
public enum CurrencyType { None, Money, Influence, Casino_coin, BranchMoney }
public enum PriceType { Default, CopyShop }
public enum BlessedState { Cursed, Normal }
public class Body { public void Unequip(Thing t) => t.isEquipped = false; }
public enum IDTSource { SuperiorIdentify }
public class EClass {
 public static Game game = new(); public static Player player => game.player; public static Chara pc => player.chara;
 public static Zone _zone = new(); public static Map _map = new(); public static UI ui = new();
}
public class Game { public Player player = new(); public bool UseGrid = true; }
public class Player { public Chara chara = new(); public Flags flags = new(); }
public class Flags { public int landDeedBought, garokkHammerBought; }
public class Zone { public int uid = 7; }
public class Map { public List<Thing> things = []; }
public class UI { public object? currentDrag; public void EndDrag() => currentDrag = null; }
public class DragItemCard { public void OnStartDrag(){} public void OnEndDrag(){} public bool OnDrag(bool execute, bool cancel) => true; public Info from = new(); public class Info { public Thing thing = null!; public int invX, invY; } }
public class Items : List<Thing> {
 public int GridSize = 35;
 public bool Full; public Thing? Find(string id) => this.FirstOrDefault(t => t.id == id);
 public bool IsFull(Thing t) => Full;
}
public class Card {
 public int uid, c_lockLv; public string id = ""; public bool isDestroyed, isStolen;
 [JsonIgnore] public Card? parent;
 public Items things = new(); public Trait trait = new();
 public Card GetRootCard() => parent?.GetRootCard() ?? this;
 public int DeliveryZone; public void SetInt(int id, int value = 0) { if (id == 102) DeliveryZone = value; }
 public Thing AddThing(Thing t, bool stack = true, int destX = -1, int destY = -1) { t.parent?.things.Remove(t); t.parent = this; things.Add(t); return t; }
 public void RemoveCard(Card c) { things.Remove((Thing)c); c.parent = null; }
}
public class Chara : Card {
 public Body body = new(); public bool isDead, IsInActiveMap = true; public int Distance = 1, Money = 10000, Charges, Experience, Discount;
 public int Dist(Card other) => Distance;
 public int GetCurrency(string id = "money") => Money;
 public void ModCurrency(int a, string id = "money") { Money += a; Charges++; }
 public void ModExp(int id, int amount) => Experience += amount;
 public Thing Pick(Thing t, bool message) => AddThing(t);
 private Dictionary<string,string?> saved = [];
 public string? GetStr(string key) => saved.GetValueOrDefault(key);
 public void SetStr(string key, string? value) => saved[key] = value;
}
public class Thing : Card {
 public int invY;
 public int Num = 1, BuyPrice = 100, SellPrice = 40;
 public BlessedState blessedState = BlessedState.Normal; public bool IsIdentified = true, c_isImportant, isEquipped;
 public string Name => id;
 public int GetPrice(CurrencyType currency, bool sell, PriceType type) => (sell ? SellPrice : BuyPrice) - EClass.pc.Discount;
 public void Identify(bool show, IDTSource source) => IsIdentified = true;
 public Thing Split(int n) {
  if (n == Num) return this;
  Num -= n;
  var t = new Thing { uid = CardCache.Next++, id = id, Num = n, BuyPrice = BuyPrice, SellPrice = SellPrice, isStolen = isStolen, IsIdentified = IsIdentified };
  CardCache.Map[t.uid] = t; return t;
 }
}
public class TraitDeliveryChest : Trait { }
public class Trait { public CurrencyType CurrencyType = CurrencyType.Money; public PriceType PriceType; public bool AllowSell = true; }
public class Button { public static implicit operator bool(Button? b) => b is not null; }
public class InvOwner {
 public Card owner, Container; public CurrencyType currency; public PriceType priceType;
 public bool UseHomeResource; public bool AllowSell => owner.trait.AllowSell;
 public InvOwner(Card owner, Card container, CurrencyType currency, PriceType price) { this.owner = owner; Container = container; this.currency = currency; priceType = price; }
 public class ErrorMessage { public Card? card; public string sound = "beep", lang = ""; }
 public class Transaction {
  public DragItemCard.Info? to; public static ErrorMessage error = new(); public Thing thing = null!; public int num; public bool sell, FreeTrade; public InvOwner destInv = null!; public Button button = new();
  public bool Valid = true; public int Quote, ProcessCalls; public bool IsValid() => Valid; public int GetPrice() => Quote; public bool Process(bool drag) { ProcessCalls++; return true; }
 }
}
public class InvOwnerShop(Card owner, Card container, CurrencyType currency, PriceType price) : InvOwner(owner, container, currency, price) { }
public class LayerInventory { public static void SetDirty(Thing t) { } }
public static class Lang { public static string _currency(int price, string id) => price.ToString(); }
public static class SE { public static void Play(string id) { } }
public static class Msg { public static void Say(string text, params object[] args) { } }
public class Guild { public static Guild Thief = new(); public int Credit; public void AddContribution(int n) => Credit += n; }
public static class Extensions { public static void ForeachReverse<T>(this List<T> list, Action<T> fn) { for (int i = list.Count - 1; i >= 0; i--) fn(list[i]); } }
public static class EmpLog { public static void Information(string s, params object[] args) { } public static void Debug(string s, params object[] args){} }
namespace UnityEngine {
 public static class Debug { public static void Log(object? value) { } }
 public static class Mathf { public static float Sqrt(float n) => MathF.Sqrt(n); public static int Abs(int n) => Math.Abs(n); }
}
namespace ElinTogether.Helper { public class Marker { } }
namespace ElinTogether.Models {
 public class RemoteCard {
  public int Uid; public LZ4Bytes? Data;
  public Card? Find() => CardCache.Find(Uid);
  public static RemoteCard Create(Card c, bool withData = false) => new() { Uid = c.uid, Data = withData ? LZ4Bytes.Create(c) : null };
  public static implicit operator RemoteCard(Card c) => Create(c);
 }
 public static class CardCache { public static Dictionary<int,Card> Map = []; public static int Next = 1000; public static Card? Find(int uid) => Map.GetValueOrDefault(uid); }
 public class LZ4Bytes {
  public string Json = "";
  public static LZ4Bytes Create<T>(T value) => new() { Json = JsonConvert.SerializeObject(value) };
  public T Decompress<T>() => JsonConvert.DeserializeObject<T>(Json)!;
 }
 public class ThingRequest { public static Dictionary<(int,int),Card> Dangling = []; public static Card? GetDanglingOrigin(Thing t, int peer) => Dangling.GetValueOrDefault((t.uid,peer)); }
 public static class OwnerProgressionAwards {
  public static List<(Chara actor, Guid id, int skill, int amount)> Awards = [];
  public static void Record(Chara c, Guid id, int skill, int amount) => Awards.Add((c,id,skill,amount));
 }
 public abstract class ElinDelta : EClass {
  public int OriginPeer; protected abstract void OnApply(ElinTogether.Net.ElinNetBase net);
  public void Apply(ElinTogether.Net.ElinNetBase net) => OnApply(net);
  public static IDisposable Simulate() => new Scope(); private class Scope : IDisposable { public void Dispose() { } }
 }
}
namespace ElinTogether.Net {
 public class Queue { public List<ElinDelta> Sent = []; public void AddRemote(ElinDelta d) => Sent.Add(d); }
 public class ElinNetBase { public Queue Delta = new(); }
 public class ElinNetClient : ElinNetBase { }
 public class ElinNetHost : ElinNetBase { public Dictionary<int,Chara> ActiveRemoteCharas = []; public List<ShopTradeReplyDelta> Replies = []; public bool SendDeltaTo(int peer, ElinDelta delta) { Replies.Add((ShopTradeReplyDelta)delta); return true; } }
 public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; }
}
namespace MessagePack { public class MessagePackObjectAttribute : Attribute { } public class KeyAttribute : Attribute { public KeyAttribute(int key) { } } }

namespace HarmonyLib {
 public enum MethodType { Getter }
 [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
 public class HarmonyPatch : Attribute { public HarmonyPatch(params object[] args) { } }
 public class HarmonyPrefix : Attribute { }
 public class HarmonyPostfix : Attribute { }
}

public class Scene { public enum Mode { StartGame, Title } }
public class ElinGameIOPropertyAttribute : Attribute { public ElinGameIOPropertyAttribute(string key) { } }
public class ElinPostSceneInitAttribute : Attribute { }
