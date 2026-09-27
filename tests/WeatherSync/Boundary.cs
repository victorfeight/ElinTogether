public class EClass {
    public static Zone _zone = new();
    public static World world = new();
    public static Screen screen = new();
}
public class Zone { public int uid = 7; }
public class World { public Weather weather = new(); }
public class Screen { public int Refreshes; public void RefreshSky() => Refreshes++; }
public class Weather {
    public enum Condition { Fine, Cloudy, Rain, RainHeavy, Snow, SnowHeavy, Ether, Blossom, None }
    public Condition _currentCondition, Fixed = Condition.None;
    public int duration, lastRain;
    public Condition CurrentCondition => Fixed == Condition.None ? _currentCondition : Fixed;
    public void SetCondition(Condition value) => throw new Exception("Must not reroll weather or water the map");
}
public static class EmpLog { public static void Debug(string s, params object[] args) { } }
namespace MessagePack {
    public class MessagePackObjectAttribute : Attribute { }
    public class KeyAttribute(int key) : Attribute { public int Key = key; }
}
namespace ElinTogether.Net {
    public class ElinNetClient { }
    public class NetSession { public static NetSession Instance = new(); public object? Connection; }
}
