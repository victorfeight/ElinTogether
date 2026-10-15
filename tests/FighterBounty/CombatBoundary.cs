// Rendering is out of scope for bounty tests; track the lifetime of the scope
// surrounding the actual damage replay so cleanup can be checked independently.
namespace ElinTogether.Models {
 internal static class CombatNarration {
  internal sealed class Scope { }
  internal static int Depth;
  internal static Scope Begin() { Depth++; return new(); }
  internal static void End(Scope? scope) { if(scope!=null)Depth--; }
 }
}
