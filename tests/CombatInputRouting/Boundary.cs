using System.Reflection;
public class Point { public Point Copy() => new(); }
public class Card { public bool isDestroyed; public Point pos = new(); public Card? root; public Card? GetRootCard() => root; }
public class Thing : Card { public bool CanAutoFire(Chara c, Card? t) => root == c; }
public class Chara : Card { public bool IsPC = true, isDead; public bool HasCooldown(int id) => cooldown; public bool cooldown; public Thing? ranged; public int mana = 10; public bool UseAbility(Act a, Card? tc, Point? pos, bool pt) { mana--; return a.Perform(this,tc,pos); } }
public class Act { public int id, performed; public bool ValidatePerform(Chara c, Card? t, Point? p) => valid; public bool valid = true; public string GetText() => "action"; public bool CanPerform(Chara c, Card? t, Point? p) => valid; public bool Perform(Chara c, Card? t, Point? p) { if (!valid) return false; performed++; return true; } }
public class TraitRod { public Card? owner; }
public class ActZap : Act { public TraitRod? trait; }
public class ActThrow : Act { public Thing? target; }
public class ActRanged : ActThrow {}
public class AIAct {
 public enum Status { Running, Fail, Success }
 public virtual bool CancelWhenDamaged => true;
 public virtual string GetText(string str = "") => str;
 public virtual IEnumerable<Status> Run() { yield return Status.Success; }
 public Status Cancel() => Status.Fail;
 public Status Success() => Status.Success;
}
public class Player { public void EndTurn(bool consume = true) {} }
public class EClass { public static Chara pc = new(); }
public class AM_Adv { public void _OnUpdateInput() {} }
public class ButtonAbility { public static bool TryUse() => true; }
namespace ElinTogether.Models { public class ElinDelta { public static bool IsApplying; } }
namespace ElinTogether.Patches {
 public static class ActionModeCombat {
  public static bool Activated = true, IsDispatching;
  public static AIAct? Pending;
  public static void QueueInput(AIAct g) => Pending = g;
 }
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method,AllowMultiple=true)]
 public class HarmonyPatch : Attribute { public HarmonyPatch(){} public HarmonyPatch(Type t,string m,params Type[] args) {} }
 public class HarmonyPrefix : Attribute {}
 public class HarmonyFinalizer : Attribute {}
 public static class AccessTools { public static MethodInfo Method(Type t,string name) => t.GetMethod(name)!; }
}
