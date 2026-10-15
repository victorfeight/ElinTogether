// Combat narration itself is covered by tests/CombatNarration. Existing relay
// behavior must remain unchanged outside a combat presentation scope.
namespace ElinTogether.Models { internal static class CombatNarration { internal static object? Current => null; } }
namespace ElinTogether.Patches { internal static class CombatNarrationMessagePatch { internal static object? Pending => null; } }
