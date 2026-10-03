// Standalone boundaries for the actual delta buffer, not a replacement queue.
public class EClass { public static Core core = new(); }
public class Core { public bool IsGameStarted = true; }
namespace ElinTogether {
 public static class EmpLog {
  public static void Warning(string text, params object?[] values) {}
  public static void Debug(string text, params object?[] values) {}
  public static void Debug(Exception ex, string text, params object?[] values) {}
 }
}
namespace ElinTogether.Net {
 public class ElinNetBase { internal readonly NetworkFlightRecorder Trace = new(_ => {}, watchdog: false); public void ReportDesync(string text) {} }
 public class ElinNetHost:ElinNetBase { public Dictionary<int,object> ActiveRemoteCharas=[]; public HashSet<int> Watching=[]; public bool AcceptsPlayerInput(int peer) => ActiveRemoteCharas.ContainsKey(peer) && !Watching.Contains(peer); }
}
namespace ElinTogether.Models {
 public class ElinDelta {
  public int OriginPeer;
  public int DeferCount;
  public enum OverrideOrder { Stack, First, Last }
  public virtual OverrideOrder Order => OverrideOrder.Stack;
  public bool RequiresGameStarted => true;
  public virtual void Apply(ElinTogether.Net.ElinNetBase net) {}
  public bool Refresh() => true;
 }
 public class CardGenDelta { public static void ClearRecordedUids() {} }
 public class QuestCreateDelta { public static void ClearRecordedUids() {} }
 public class ProbeDelta(string label) : ElinDelta { public string Label => label; }
 public class CallbackDelta(Action action):ElinDelta {public override void Apply(ElinTogether.Net.ElinNetBase net)=>action();}
}

namespace ElinTogether.Models {
 // Scheduler tests have no pending progression awards. The active-award send
 // boundary is exercised against the production implementation in ShopInvestment.
 internal static class OwnerProgressionAwards {
  internal static void PrepareOutgoing(List<ElinDelta> batch, params List<ElinDelta>[] queued) { }
 }
}

namespace ElinTogether.Models { public class GameDelta:ElinDelta {} }
