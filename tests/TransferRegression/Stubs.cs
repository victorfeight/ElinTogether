using System.Collections;
public static class EmpLog { public static void Debug(string message) {} }
public class UIInventory { public void Sort(bool redraw = true) {} public void CheckDirty() {} }
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type,string name) {} }
 [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPrefix : Attribute {}
 [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyFinalizer : Attribute {}
}
namespace UnityEngine {
 public enum KeyCode {LeftShift,RightShift}
 public static class Input {
  public static bool Left, Shift, RightShift;
  public static bool GetMouseButton(int button) => button==0 && Left;
  public static bool GetKey(KeyCode key) => key==KeyCode.LeftShift ? Shift : RightShift;
 }
}
namespace ElinTogether.Patches { internal static class SynchronizationContext { internal static void AllowDeltaSending() {} } }
namespace ElinTogether.Net {
 internal sealed class NetSession { public static readonly NetSession Instance=new(); public object? Connection; }
 internal sealed class FakeCore { internal bool IsGameStarted; }
 internal sealed class FakeScheduler {
  public int Starts, Stops;
  public void Subscribe(Action action,float hz) {if(hz!=50f) throw new Exception("Unexpected send rate"); Starts++;}
  public void Unsubscribe(Action action) {Stops++;}
 }
 internal partial class ElinNetClient {
  internal readonly FakeCore core=new();
  internal readonly FakeScheduler Scheduler=new();
  private bool _pauseUpdate;
  internal IEnumerator Begin() => StartWorldStateUpdateWhenReady();
  internal bool Paused => _pauseUpdate;
 }
}
