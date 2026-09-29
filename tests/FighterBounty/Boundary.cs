public class EClass { public static Chara pc { get; set; } = new(); }
public enum AttackSource { None }
public class Card { public int uid; public int hp; public void ModCurrency(int amount, string currency) {} public void DamageHP(long d,int e,int ep,AttackSource a,Card o,bool s,Thing w,Chara t,int r) {} }
public class Thing : Card {}
public class Chara : Card { public bool isDead; public Chara? master; public Chara? resolvedMaster; public Chara? FindMaster() => resolvedMaster; }
public class GuildFighter { public bool HasBounty(Chara c) => true; }
public static class Msg { public static Chara? Receiver; public static bool Throw;
 public static string Say(string id,Card c,string? a,string? b,string? d) { Receiver=ElinTogether.Patches.PersonalMsgSayPatch.BountyReceiver; if(Throw) throw new Exception("test"); return id; } }
public static class EmpLog { public static void Warning(string s) {} }
namespace ElinTogether.Net {
 public class ElinNetBase { public virtual bool IsHost => false; }
 public class ElinNetClient : ElinNetBase {}
 public class ElinNetHost : ElinNetBase { public override bool IsHost => true; public Dictionary<int,Chara> ActiveRemoteCharas = new(); }
 public class NetSession { public static NetSession Instance = new(); public object? Connection; public Chara? Player; }
}
namespace ElinTogether.Patches { internal static class PersonalMsgSayPatch { internal static Chara? BountyReceiver {get;set;} } }

namespace MessagePack {
 [AttributeUsage(AttributeTargets.Class)] public class MessagePackObjectAttribute : Attribute {}
 [AttributeUsage(AttributeTargets.Property)] public class KeyAttribute(int n) : Attribute { public int Number { get; } = n; }
}
namespace ElinTogether.Models {
 public class RemoteCard(Card card) {
  public Card Find()=>card;
  public static implicit operator Card?(RemoteCard? r)=>r?.Find();
  public static implicit operator Thing?(RemoteCard? r)=>r?.Find() as Thing;
  public static implicit operator Chara?(RemoteCard? r)=>r?.Find() as Chara;
 }
 public class ElinDelta {
  protected virtual void OnApply(ElinTogether.Net.ElinNetBase net) {}
  public void Apply(ElinTogether.Net.ElinNetBase net)=>OnApply(net);
  protected static IDisposable Simulate(bool active)=>new EmptyScope();
  private class EmptyScope : IDisposable { public void Dispose() {} }
 }
}
namespace ElinTogether.Patches {
 public static class DamageReplayBoundary {
  public static Action? Replay;
  public static void Stub_DamageHP(this Card card,long d,int e,int ep,AttackSource a,Card? o,bool s,Thing? w,Chara? t,int r)=>Replay?.Invoke();
 }
}
