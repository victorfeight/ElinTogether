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
 public class ElinNetBase { public void ReportDesync(string text) {} }
}
namespace ElinTogether.Models {
 public class ElinDelta {
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
}
