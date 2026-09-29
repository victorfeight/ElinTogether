using System.Reflection;
public class EClass { public static Map _map = new(); public static Zone _zone = new(); }
public class Zone { public int uid = 7; }
public class Map {
    public int Size = 4; public HashSet<int> Seen = [];
    public void SetSeen(int x,int z,bool seen=true,bool refresh=true) { if(seen) Seen.Add(z*Size+x); }
    public void Reveal() {} public void RevealAll() {}
}
public class Chara { public bool IsInActiveMap = true; }
public class Card { public int c_charges; public bool isDestroyed; }
public static class WidgetMinimap { public static int Refreshes; public static void UpdateMap() => Refreshes++; }
public static class EmpLog { public static void Debug(string s, params object[] a) {} }
namespace ElinTogether.Helper { }
namespace MessagePack {
    public class MessagePackObjectAttribute : Attribute {}
    public class KeyAttribute(int key) : Attribute { public int Key = key; }
}
namespace HarmonyLib {
    public enum MethodType { Setter }
    public class HarmonyPatch : Attribute { public HarmonyPatch() {} public HarmonyPatch(Type t,string n) {} public HarmonyPatch(Type t,string n,MethodType m) {} }
    public class HarmonyPrefix : Attribute {} public class HarmonyPostfix : Attribute {} public class HarmonyFinalizer : Attribute {}
    public static class AccessTools { public static MethodInfo Method(Type t,string n) => t.GetMethod(n)!; }
}
namespace ElinTogether.Net {
    public class ElinNetBase { public virtual bool IsHost => false; public DeltaQueue Delta = new(); }
    public class ElinNetClient : ElinNetBase {}
    public class ElinNetHost : ElinNetBase { public override bool IsHost => true; public Dictionary<int,Chara> ActiveRemoteCharas = []; }
    public class DeltaQueue { public List<ElinTogether.Models.ElinDelta> Sent=[]; public void AddRemote(ElinTogether.Models.ElinDelta d) => Sent.Add(d); }
    public class NetSession { public static NetSession Instance=new(); public ElinNetBase? Connection; }
}
namespace ElinTogether.Models {
    public abstract class ElinDelta : EClass { public int OriginPeer; protected virtual void OnApply(ElinTogether.Net.ElinNetBase n) {} public void Apply(ElinTogether.Net.ElinNetBase n) => OnApply(n); }
    public class CardChargeDelta : ElinDelta { public Card Card=null!; public int Charges; }
    public static class CardCache { public static bool Cached=true; public static bool Contains(Card c)=>Cached; }
}
