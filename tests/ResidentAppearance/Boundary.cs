using ElinTogether.Models;
public class EClass {
    public static Zone _zone = new(); public static Map _map = new(); public static Core core = new();
    public static Game game = new(); public static Scene scene = new();
}
public class Zone { public int uid = 10; }
public class Map { public List<Chara> Members = []; public Chara? FindChara(int uid) => Members.Find(c => c.uid == uid); }
public class Core { public bool IsGameStarted = true; }
public class Game { public bool isLoading; }
public class Scene { public List<Renderer> syncList = []; }
public class Renderer { public int Enters, Leaves; public void OnEnterScreen() => Enters++; public void OnLeaveScreen() => Leaves++; }
public class Source { public string idActor = ""; }
public class PCCData { public Dictionary<string, string[]> map = []; }
public class Chara {
    public int uid = 42, Hair, Builds; public string? c_altName, c_idPortrait, c_idSpriteReplacer;
    public PCCData? pccData; public Source sourceCard = new(); public Renderer renderer = new();
    public bool isSynced = true, IsAliveInCurrentZone = true, IsInActiveMap = true, Home = true, Guest, Protected, isDead, IsDisabled;
    public bool IsHomeMember() => Home; public bool IsGuest() => Guest;
    public int GetInt(int n) => Hair; public void SetInt(int n, int v) => Hair = v;
    public void _CreateRenderer() { Builds++; renderer = new(); }
}
public static class WidgetRoster { public static int Refreshes; public static void SetDirty() => Refreshes++; }
public static class EmpLog { public static void Warning(string s, params object[] v) { } public static void Information(string s, params object[] v) { } }
public static class Extensions { public static bool IsEmpty(this string s) => string.IsNullOrEmpty(s); }
namespace ElinTogether.Helper { }
namespace ElinTogether.Net {
    public class Queue { public List<ElinDelta> Items = []; public void AddRemote(ElinDelta d) => Items.Add(d); public void DeferLocal(ElinDelta d) => Items.Add(d); }
    public class ElinNetBase { public bool IsHost => this is ElinNetHost; public Queue Delta = new(); }
    public class ElinNetClient : ElinNetBase { }
    public class ElinNetHost : ElinNetBase {
        public Dictionary<int, Chara> ActiveRemoteCharas = []; public List<ElinDelta> Replies = [];
        public bool AcceptInput = true; public bool AcceptsPlayerInput(int n) => AcceptInput && ActiveRemoteCharas.ContainsKey(n);
        public void SendDeltaTo(int p, ElinDelta d) => Replies.Add(d);
    }
    public class NetSession { public static NetSession Instance = new(); public ElinNetBase? Connection; }
}
namespace ElinTogether.Models {
    public abstract class ElinDelta : EClass {
        public int OriginPeer = 1; public static bool IsApplying;
        protected abstract void OnApply(ElinTogether.Net.ElinNetBase net);
        public void Apply(ElinTogether.Net.ElinNetBase net) { IsApplying = true; try { OnApply(net); } finally { IsApplying = false; } }
    }
    public class MsgSayDelta : ElinDelta { public string Text = ""; public int R,G,B,A; protected override void OnApply(ElinTogether.Net.ElinNetBase n) { } }
    public static class PartyMembership { public static bool Protected(Chara c) => c.Protected; }
    public static class ReserveManagement { public static void RefreshList() { } }
}
namespace MessagePack { public class MessagePackObjectAttribute : Attribute { } public class KeyAttribute(int n) : Attribute { public int N = n; } }
