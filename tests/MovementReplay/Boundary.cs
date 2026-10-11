using UnityEngine;
namespace HarmonyLib { class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string s){}} class HarmonyPrefix:Attribute{} class HarmonyFinalizer:Attribute{} }
namespace ElinTogether.Patches { public static class ActionModeCombat {public static bool Activated;} }
namespace UnityEngine {
public struct Vector3(float x,float y,float z) {public float x=x,y=y,z=z; public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z); public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z); public static Vector3 operator *(Vector3 a,float f)=>new(a.x*f,a.y*f,a.z*f); public static bool operator ==(Vector3 a,Vector3 b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;public static bool operator !=(Vector3 a,Vector3 b)=>!(a==b);public override bool Equals(object o)=>o is Vector3 b&&this==b;public override int GetHashCode()=>HashCode.Combine(x,y,z);public static float Distance(Vector3 a,Vector3 b)=>MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));public static Vector3 MoveTowards(Vector3 a,Vector3 b,float d){var n=Distance(a,b);return n<=d?b:a+(b-a)*(d/n);}public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;}
public class AnimationCurve {public float Evaluate(float f)=>f;}
}
public class Point(int x=0,int z=0){public int X=x,Z=z;public Point Copy()=>new(X,Z);public void Set(Point p){X=p.X;Z=p.Z;}public int Distance(Point p)=>Math.Max(Math.Abs(X-p.X),Math.Abs(Z-p.Z));public Vector3 PositionCenter()=>new(X,Z,0);public override bool Equals(object o)=>o is Point p&&p.X==X&&p.Z==Z;public override int GetHashCode()=>HashCode.Combine(X,Z);}
public class Zone{}
public class Chara {public Chara host;public bool IsPC,IsPCParty=true,IsDeadOrSleeping;public float actTime=.3f,idleTimer;public Point pos=new();public Zone currentZone=new();public CharaRenderer renderer;public Cell Cell=new();public object ai;public Chara(){renderer=new(){owner=this};}}
public class Cell{public CellDetail detail;}public class CellDetail{public object anime;}public class AI_Trolley{}
public class RenderParam {public float x,y,z,shadowFix;}
public static class Scene{public static bool skipAnime;}
public static class Game{public static bool isPaused;}
public static class Core{public static float delta=1f/60;}
public static class ActionMode{public static float[] GameSpeeds=[1,2,4];}
public static class EClass{public static Chara pc=new();public static Settings game=new();public static Player player=new();public static Root core=new();}
public class Settings{public int gameSpeedIndex;}
public class Player{public Vector3 position;public float renderExtraTime;}
public class Root{public Config config=new();}public class Config{public Graphic graphic=new();}public class Graphic{public int spriteFrameMode;}
public class Anime {public float nextFrameInterval=.1f,animeExtraTimeParty,animeExtraTime,destRadius=.001f,slowSpeed=1,idleTime=.3f,fixedMove=1,hopStrength;public AnimationCurve gradientZForward=new(),gradientZBack=new();}
public static class RenderObject{public static float gameSpeed=1,gameDelta=1f/60;public static Anime animeSetting=new();}
public class Data{public bool animate=true;}public class Actor{public void IdleFrame(){}public void NextFrame(){}}
public partial class CharaRenderer{public Chara owner;public static int _animeFramePC=12,_animeFramePCC=12,_animeFrame=12;public static bool smoothmove=true;public object pccData=new();public bool first,hasActor,isMoving;public bool IsMoving=>isMoving;public Data data=new();public Point movePoint=new();public Vector3 position,orgPos;public int step;private float moveTimer,nextframeTimer,actTime=.3f,lastShadowFix,lastShadowFix2;public Actor actor=new();public AnimationCurve hopCurve;public void RefreshSprite(){}public void IdleFrame(){}public void SetFirst(bool f,Vector3 p){first=f;position=p;movePoint.Set(owner.pos);step=9999999;isMoving=false;moveTimer=0;}}
