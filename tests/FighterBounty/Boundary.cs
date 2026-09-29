public class EClass { public static Chara pc { get; set; } = new(); }
public enum AttackSource { None }
public class Card { public int uid; public void ModCurrency(int amount, string currency) {} public void DamageHP(long d,int e,int ep,AttackSource a,Card o,bool s,Thing w,Chara t,int r) {} }
public class Thing : Card {}
public class Chara : Card { public Chara? master; public Chara? resolvedMaster; public Chara? FindMaster() => resolvedMaster; }
public class GuildFighter { public bool HasBounty(Chara c) => true; }
public static class Msg { public static Chara? Receiver; public static bool Throw;
 public static string Say(string id,Card c,string? a,string? b,string? d) { Receiver=ElinTogether.Patches.PersonalMsgSayPatch.BountyReceiver; if(Throw) throw new Exception("test"); return id; } }
public static class EmpLog { public static void Warning(string s) {} }
namespace ElinTogether.Net {
 public class ElinNetClient {}
 public class ElinNetHost { public Dictionary<int,Chara> ActiveRemoteCharas = new(); }
 public class NetSession { public static NetSession Instance = new(); public object? Connection; }
}
namespace ElinTogether.Patches { internal static class PersonalMsgSayPatch { internal static Chara? BountyReceiver {get;set;} } }
