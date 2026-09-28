public class EClass { public static Player player = new(); }
public class Player { public HashSet<int> knownBGMs = []; }
public class LayerEditPlaylist { public int Refreshes; public void Refresh() => Refreshes++; }
public static class EmpLog { public static void Debug(string message, params object[] values) { } }
namespace ElinTogether.Net {
    public class ElinNetClient { }
    public class ElinNetHost { }
    public class NetSession {
        public static NetSession Instance = new();
        public object? Connection;
    }
}
namespace UnityEngine {
    public class Object {
        public static LayerEditPlaylist[] Views = [];
        public static T[] FindObjectsOfType<T>() => Views.Cast<T>().ToArray();
    }
}
