namespace ElinTogether.Patches { public static class ActionModeCombat { public static bool Activated; } }
public static class RenderObject { public static float gameSpeed,gameDelta; }
public static class Game { public static bool isPaused; }
public static class Core { public static float delta,gameDelta; }
public static class ActionMode { public static float[] GameSpeeds=[1,2,4]; }
public static class EClass { public static Settings game=new(); }
public class Settings { public int gameSpeedIndex; }
public class Point(int x,int z) { public int X=x,Z=z; public Point Copy()=>new(X,Z); public int Distance(Point p)=>Math.Max(Math.Abs(X-p.X),Math.Abs(Z-p.Z)); public Point PositionCenter()=>this; public override bool Equals(object? o)=>o is Point p&&p.X==X&&p.Z==Z; public override int GetHashCode()=>HashCode.Combine(X,Z); }
public class CharaRenderer { public Chara owner=null!; public Point movePoint=new(-1,-1); public bool IsMoving; public int Snaps; public Point? Position; public void SetFirst(bool first,Point p) { Snaps++;Position=p; } public void UpdatePosition() {} }
public class Zone {}
public class Chara { public float actTime; public Zone currentZone=new(); public Point pos=new(0,0); public CharaRenderer renderer; public Chara(){renderer=new(){owner=this};} }
