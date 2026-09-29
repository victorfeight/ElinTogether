public class EClass { public static Map _map = new(); public static Zone _zone = new(); }
public class Map { public List<Thing> things = []; }
public class Zone { public int uid = 7; }
public class Thing {
    public int uid; public bool isDestroyed, isHidden = true;
    public Point pos = new();
    public object trait = new TraitTrap(); public int Refreshes;
    public void SetHidden(bool hide) { isHidden = hide; Refreshes++; }
}
public class TraitTrap { }
public class Point { public bool IsValid = true; }
namespace ElinTogether.Helper { }
public class Chara { public bool IsInActiveMap = true; }
public static class EmpLog { public static void Debug(string s, params object[] args) { } }
namespace MessagePack {
    public class MessagePackObjectAttribute : Attribute { }
    public class KeyAttribute(int key) : Attribute { }
}
namespace ElinTogether.Net {
    public class ElinNetBase { public DeltaQueue Delta = new(); }
    public class ElinNetClient : ElinNetBase { }
    public class ElinNetHost : ElinNetBase { public Dictionary<int, Chara> ActiveRemoteCharas = []; }
    public class DeltaQueue { public List<ElinTogether.Models.ElinDelta> Sent = []; public void AddRemote(ElinTogether.Models.ElinDelta d) => Sent.Add(d); }
    public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; }
}
namespace ElinTogether.Models {
    public abstract class ElinDelta {
        public int OriginPeer; protected abstract void OnApply(ElinTogether.Net.ElinNetBase net);
        public void Apply(ElinTogether.Net.ElinNetBase net) => OnApply(net);
    }
}
