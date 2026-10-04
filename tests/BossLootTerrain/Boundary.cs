using ElinTogether.Models;
public class EClass {public static Zone _zone=new(){uid=7};public static Map _map=new();public static Chara pc=new();public static Settings setting=new();public static Core core=new();}
public class Zone {public int uid;}
public class Card {public Point pos=new(0,0);public Trait trait=new();public void Explode(Point p,Card origin){}}
public class Chara:Card {public void TryDropBossLoot(){} public void DestroyPath(Point pos){}}
public class Trait {public ThrowType ThrowType;}
public enum ThrowType {None,Explosive}
public enum EffectId {Explosive,Ball}
public struct ActRef {public Card? refThing;}
public class Element {public int id;public Source source=new();public static Element Create(int id,int power)=>new(){id=id,source=new(){alias=id==0?"eleVoid":"eleImpact"}};}
public class Source {public string alias="eleImpact";}
public class Settings {public Dictionary<string,ElementRef> elements=new(){{"eleImpact",new()},{"eleVoid",new()}};}
public class ElementRef {public int colorTrail=42;}
public class Core {public Config config=new();}
public class Config {public Graphics graphic=new();public Graphics test=new();}
public class Graphics {public bool disableShake,disableShake2;}
public class Shaker {public static int Count;public static void ShakeCam(string id)=>Count++;}
public class Effect {
 public static List<(string Id,int X,int Z,float Delay)> Played=new();public string Id="";public float Delay;
 public static Effect Get(string id)=>new(){Id=id};public void SetStartDelay(float delay)=>Delay=delay;
 public Effect SetParticleColor(int color,bool changeMaterial,string property)=>this;
 public Effect Play(Point p){Played.Add((Id,p.x,p.z,Delay));return this;}public void Flip(bool flip){}
}
public class Cell {public byte _blockMat,_block,_floorMat,_floor,obj,objVal;public int blockDir,objDir,floorDir;}
public class ActEffect {public static void DamageEle(){}}
public class Task {}
public class Map {
 public int Mines; public bool FailMining;
 public void MineBlock(Point p,bool recoverBlock=false,Chara c=null,bool mineObj=true){Mines++;if(FailMining)throw new InvalidOperationException();SetBlock(p.x,p.z,0,0,0);ElinTogether.Patches.PathTerrainSync.Block(this,p.x,p.z);}
 public void MineObj(Point p,Task task=null,Chara c=null){Mines++;if(FailMining)throw new InvalidOperationException();cells[p.x,p.z].obj=0;ElinTogether.Patches.PathTerrainSync.Object(this,p.x,p.z);}
 public Cell[,] cells=new Cell[10,10];public int Shadows,Fovs;
 public Map(){for(int x=0;x<10;x++)for(int z=0;z<10;z++)cells[x,z]=new();}
 public void SetBlock(int x,int z,int mat,int id,int dir){new Point(x,z).SetBlock(mat,id);cells[x,z]._block=(byte)id;cells[x,z]._blockMat=(byte)mat;cells[x,z].blockDir=dir;}
 public void SetFloor(int x,int z,int mat,int id,int dir){cells[x,z]._floor=(byte)id;cells[x,z]._floorMat=(byte)mat;cells[x,z].floorDir=dir;}
 public void SetObj(int x,int z,int mat,int id,int value,int dir,bool ignoreRandomMat=false){}
 public void RefreshShadow(int x,int z)=>Shadows++;public void RefreshFOV(int x,int z)=>Fovs++;
}
public class Point(int x,int z) {public int x=x,z=z;public static int Sounds;public void PlaySound(string id)=>Sounds++;public int Distance(Point p)=>Math.Max(Math.Abs(x-p.x),Math.Abs(z-p.z));public static Dictionary<(int,int),(int block,int obj)> Tiles=new();public void SetBlock(int mat=0,int id=0){var t=Tiles[(x,z)];Tiles[(x,z)]=(id,t.obj);}public void SetObj(int id=0,int value=1,int dir=0){var t=Tiles[(x,z)];Tiles[(x,z)]=(t.block,id);}}
public static class EmpLog{public static void Debug(string s,params object[] args){}}
namespace ElinTogether.Models {
public class Position{public int X,Z;public bool IsInActiveMapBounds=>X>=0&&Z>=0&&X<10&&Z<10;public static implicit operator Position(Point p)=>new(){X=p.x,Z=p.z};public static implicit operator Point(Position p)=>new(p.X,p.Z);}
public abstract class ElinDelta{protected abstract void OnApply(ElinTogether.Net.ElinNetBase n);public void Apply(ElinTogether.Net.ElinNetBase n)=>OnApply(n);}
}
namespace ElinTogether.Net{public class ElinNetBase{public bool IsHost=>this is ElinNetHost;}public class ElinNetHost:ElinNetBase{public Queue Delta=new();}public class ElinNetClient:ElinNetBase{}public class Queue{public List<ElinDelta> Items=new();public void AddRemote(ElinDelta d)=>Items.Add(d);}public class NetSession{public static NetSession Instance=new();public ElinNetBase? Connection;}}
namespace ElinTogether.Patches{public static class CharaProgressCompleteEvent{public static bool Packing;public static List<ElinDelta> Packed=new();public static bool ShouldPack(bool _) => Packing;public static void Pack(ElinDelta d)=>Packed.Add(d);}}
namespace MessagePack{public class MessagePackObjectAttribute:Attribute{}public class KeyAttribute(int value):Attribute{}}
