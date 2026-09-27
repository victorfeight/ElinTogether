using ElinTogether.Models;
public class EClass {public static Chara pc=new();public static Player player=new();public static Core core=new();}
public class Player {public HotItem currentHotItem=new();} public class HotItem {public Thing? RenderThing;}
public class Core {public Config config=new();} public class Config {public GameConfig game=new();} public class GameConfig {public bool hideWeapons,showOffhand;}
public class Card {public bool isDestroyed;public int uid;public Card? parent;public Card GetRootCard()=>parent?.GetRootCard()??this;}
public class Thing:Card {}
public class Chara:Card {public bool IsPC,isDead;public void SetAI(AIAct a){ai=a;a.SetOwner(this); }public int combatCount;public AIAct ai=new();public Card? held;public Body body=new();public Profile NetProfile=new();public void HoldCard(Card c)=>held=c;public void PickHeld()=>held=null;}
public class Body {public Slot? slotMainHand,slotOffHand;}public class Slot {public Thing? thing;}
public class Profile {public Hand? RemoteMainHand,RemoteOffHand;} public record Hand(RemoteCard? Card,bool Flag);
public class AIAct {public enum Status {Running,Success}public Status status;public AIAct? child;public Chara owner=null!;public bool IsChildRunning=>child?.status==Status.Running;public virtual bool IsIdle=>false;public virtual bool IsNoGoal=>false;public virtual int MaxRestart=>0;public virtual bool CancelWhenDamaged=>true;public virtual bool PushChara=>true;public virtual IEnumerable<Status> Run(){yield break;}public void SetOwner(Chara c)=>owner=c;public void Reset()=>status=Status.Success;public void Tick(){} }
public class NoGoal:AIAct {}
public class BaseTaskHarvest:AIAct {public Card? Captured;public void SetTarget(Chara c)=>Captured=c.held;}
public static class EmpLog {public static void Warning(string s,params object?[] args){}public static void Debug(string s,params object?[] args){}}
namespace MessagePack {public class MessagePackObjectAttribute:Attribute {} public class KeyAttribute(int key):Attribute {}}
namespace ElinTogether.Helper {public class Placeholder {}}
namespace ElinTogether.Net {public class ElinNetBase {public bool IsHost;public bool IsClient=>!IsHost;public Queue Delta=new();}public class Queue {public List<ElinDelta> Items=new();public void AddRemote(ElinDelta d)=>Items.Add(d);}}
namespace ElinTogether.Models {
public class RemoteCard(Card card) {public int Uid=>card.uid;public Card Find()=>card;public static implicit operator RemoteCard(Card c)=>new(c);}
public abstract class ElinDelta:EClass {protected static IDisposable Simulate(bool b)=>new Scope();private class Scope:IDisposable{public void Dispose(){}}protected abstract void OnApply(ElinTogether.Net.ElinNetBase net);public void Apply(ElinTogether.Net.ElinNetBase net)=>OnApply(net);}
public static class PendingUid {public static bool IsPending(int uid)=>false;}
}

namespace ElinTogether.Patches {public static class RemoteCraft{public static bool IsHostRun(AIAct? a)=>false;}}
namespace ElinTogether.Models {
public abstract class TaskArgsBase{public abstract AIAct CreateSubAct();}
public class HarvestArgs:TaskArgsBase{public override AIAct CreateSubAct()=>new BaseTaskHarvest();}
public class IdleArgs:TaskArgsBase{public override AIAct CreateSubAct()=>new NoGoal();}
public static class TaskCache{public static bool Taken;public static int Cancelled;public static object? GetRequiredPos(AIAct? a)=>a is BaseTaskHarvest?new object():null;public static bool IsPosTaken(object p,Chara c)=>Taken;public static void RequestCancel(ElinTogether.Net.ElinNetBase n,RemoteCard c,AIAct a)=>Cancelled++;}
}
