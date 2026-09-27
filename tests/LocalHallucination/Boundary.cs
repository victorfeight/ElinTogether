using System.Runtime.CompilerServices;
using HarmonyLib;
public class Card {
 public bool savedPC,localPC;
 public bool _IsPC { [MethodImpl(MethodImplOptions.NoInlining)] get=>savedPC; }
 public virtual bool IsPC=>localPC;
}
public class Chara:Card {public bool hallucinating; public bool HasCondition<T>()=>hallucinating;}
public class BadCondition {
 public Chara owner=null!;
 public virtual void SetOwner(Chara value,bool onDeserialize=false)=>owner=value;
 public virtual void OnRemoved(){}
}
public class Player {public static int seedHallucination;public Chara chara=new();}
public class Game {public Player player=new();}
public class EClass {public static Game game=new();public static int rnd(int max)=>17;}
public class Scene {public enum Mode{Title,StartGame}}
public class ElinPostSceneInitAttribute:Attribute{}
namespace ElinTogether.Net {
 public class NetSession {public static NetSession Instance=new();public bool HasActiveConnection;}
}
namespace EModding.Helper {
 public static class Validation {
  public static CodeMatcher EnsureValid(this CodeMatcher matcher,string message) {
   if(matcher.IsInvalid)throw new Exception(message);return matcher;
  }
 }
}
