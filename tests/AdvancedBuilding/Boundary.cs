namespace HarmonyLib {
    [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch(Type type, string method) : Attribute { }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPostfix : Attribute { }
}
namespace ElinTogether.Net {
    public class ElinNetClient { }
    public class NetSession { public static NetSession Instance = new(); public object? Connection; }
}
public class ActionMode { }
public class AM_CreateArea : ActionMode { }
public class AM_ExpandArea : ActionMode { }
public class AM_EditArea : ActionMode { }
public class AM_RemoveDesignation : ActionMode { }
public class AM_Deconstruct : ActionMode { }
public class AM_Copy : ActionMode { }
public class AM_Blueprint : AM_Copy { }
public class AM_Designation<T> : ActionMode { }
public class AM_Build : AM_Designation<object> { }
public class CustomBuild : AM_Build { }
public class BaseTileSelector { public ActionMode mode = new(); public object? start; public void TryProcessTiles() { } }
public class UndoManager { public void Perform() { } }
public class BaseArea {
    public class Interaction { public Action? action; }
    public void ListInteractions() { }
}
public class Msg { public static int Messages; public static void Say(string text) { Messages++; } }
