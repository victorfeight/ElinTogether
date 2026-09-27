using ElinTogether.Patches;

public class Card
{
    public int uid, Num = 1, invY;
    public string id = "money";
    public bool IsHostOwned = true, isDestroyed;
    public object? parent;
    public Card GetRootCard() => this;
    public bool CanStackTo(Thing to) => this != to && !isDestroyed && !to.isDestroyed && id == to.id;
    public bool TryStackTo(Thing to)
    {
        bool result = false;
        if (CardTryStackEvent.OnCardTryStackTo(this, to, ref result)) {
            if (CanStackTo(to)) {
                to.Num += Num;
                isDestroyed = true;
                result = true;
            }
        }
        return result;
    }
}
public class Thing : Card { }
public class UIInventory
{
    public List<Thing> Items = new();
    public int Visits;
    public void Sort()
    {
        // Vanilla UIInventory.Sort's retry-until-no-merges loop. The cap makes
        // the old infinite-loop regression fail a test instead of hanging it.
        InvSortEvent.Begin(out var state);
        try {
            bool retry = true;
            while (retry) {
                retry = false;
                foreach (var source in Items.Where(t => !t.isDestroyed)) {
                    if (source.invY == 1) continue;
                    foreach (var target in Items.Where(t => !t.isDestroyed)) {
                        if (++Visits > 10000) throw new Exception("Sort did not terminate");
                        if (source != target && target.invY != 1 && source.TryStackTo(target)) {
                            retry = true;
                            break;
                        }
                    }
                    if (retry) break;
                }
            }
        } finally { InvSortEvent.End(state); }
    }
}
public static class EmpLog { public static void Debug(string s, params object?[] args) { } }
namespace HarmonyLib {
    [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch(Type t, string name) : Attribute { }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPostfix : Attribute { }
    public class HarmonyFinalizer : Attribute { }
}
namespace ElinTogether.Net {
    public class ElinNetBase { }
    public class ElinNetClient : ElinNetBase { public DeltaQueue Delta = new(); }
    public class DeltaQueue {
        public List<ElinTogether.Models.CardTryStackToDelta> Items = new();
        public void AddRemote(ElinTogether.Models.CardTryStackToDelta d) => Items.Add(d);
    }
    public class NetSession {
        public static NetSession Instance = new();
        public ElinNetBase? Connection;
        public bool IsHost => Connection is not null and not ElinNetClient;
    }
}
namespace ElinTogether.Models {
    public static class ElinDelta { public static bool IsApplying; }
    public static class PendingSplit {
        public static int Source = -1, Target = -1;
        public static int Resolve(int uid) => uid == Source ? Target : -1;
    }
    public class CardTryStackToDelta {
        public required Card Card;
        public required Thing To;
        public Card? Parent;
    }
}
namespace ElinTogether.Patches { public static class RemoteCraft { public static Card? ProductReceiver; } }
