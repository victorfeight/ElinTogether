using ElinTogether.Models;
public class EClass {public static Zone _zone=new(){uid=7};}
public class Zone {public int uid;}
public class Chara {public void TryDropBossLoot(){}}
public class Point(int x,int z) {public int x=x,z=z;public static Dictionary<(int,int),(int block,int obj)> Tiles=new();public void SetBlock(int mat=0,int id=0){var t=Tiles[(x,z)];Tiles[(x,z)]=(id,t.obj);}public void SetObj(int id=0,int value=1,int dir=0){var t=Tiles[(x,z)];Tiles[(x,z)]=(t.block,id);}}
public static class EmpLog{public static void Debug(string s,params object[] args){}}
namespace ElinTogether.Models {
public class Position{public int X,Z;public bool IsInActiveMapBounds=>X>=0&&Z>=0&&X<10&&Z<10;public static implicit operator Position(Point p)=>new(){X=p.x,Z=p.z};public static implicit operator Point(Position p)=>new(p.X,p.Z);}
public abstract class ElinDelta{protected abstract void OnApply(ElinTogether.Net.ElinNetBase n);public void Apply(ElinTogether.Net.ElinNetBase n)=>OnApply(n);}
}
namespace ElinTogether.Net{public class ElinNetBase{public bool IsHost=>this is ElinNetHost;}public class ElinNetHost:ElinNetBase{public Queue Delta=new();}public class ElinNetClient:ElinNetBase{}public class Queue{public List<ElinDelta> Items=new();public void AddRemote(ElinDelta d)=>Items.Add(d);}public class NetSession{public static NetSession Instance=new();public ElinNetBase? Connection;}}
namespace ElinTogether.Patches{public static class CharaProgressCompleteEvent{public static bool Packing;public static List<ElinDelta> Packed=new();public static bool ShouldPack(bool _) => Packing;public static void Pack(ElinDelta d)=>Packed.Add(d);}}
namespace MessagePack{public class MessagePackObjectAttribute:Attribute{}public class KeyAttribute(int value):Attribute{}}
