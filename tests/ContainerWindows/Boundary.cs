using Newtonsoft.Json;
public class Card{public int uid;public bool isChara,isDestroyed;public object trait=new();public Window.SaveData? c_windowSaveData;}
public class TraitChestMerchant{}
public enum ContainerSharedType{Personal,Shared}
public class Window{
 public string idWindow="LayerInventoryFloat0";public SaveData saveData=new();public Button buttonShared=new(),buttonSort=new();
 public class SaveData{
  public int[] ints=new int[20];public HashSet<int> cats=[];public string filter="";public string[]? _filterStrs;
  public int autodump,priority,flag,category,sortMode;public ContainerSharedType sharedType;
  public bool excludeDump,excludeCraft,compress,advDistribution,noRotten,onlyRottable,alwaysSort,sort_ascending,open,useBG,noRightClickClose,fixedPos,shiftToShowMenu;
 }
}
public class Button{public Click onClick=new();public Image image=new();public Tooltip tooltip=new();}
public class Click{public List<Action> Actions=[];public void AddListener(Action a)=>Actions.Add(a);public void Invoke(){foreach(var a in Actions.ToArray())a();}}
public class Image{public object? sprite;}public class Tooltip{public string lang="";}
public class InvOwner{public Card Container=new();}
public class UIInventory{public InvOwner owner=new();public Window window=new();public int Refreshes;public void RefreshMenu(){}public void RefreshWindow()=>Refreshes++;}
public class LayerInventory{public static List<LayerInventory> listInv=[];public List<UIInventory> invs=[];}
public class EMono{public static Core core=new();public static UI ui=new();}
public class Core{public Refs refs=new();}public class Refs{public Icons icons=new();}public class Icons{public object shared=new(),personal=new();}
public class UI{public ContextMenu contextMenu=new();}public class ContextMenu{public bool currentMenu;public static bool operator !(ContextMenu menu)=>false;}
public static class IO{public static T DeepCopy<T>(T x)=>JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(x))!;}
namespace ElinTogether.Models{
 public class ElinDelta{public static bool IsRemoteStateLanding;protected virtual void OnApply(ElinTogether.Net.ElinNetBase net){}public void Apply(ElinTogether.Net.ElinNetBase net)=>OnApply(net);}
 public class RemoteCard{public Card? Card;public Card? Find()=>Card;public static implicit operator RemoteCard(Card c)=>new(){Card=c};}
 public class LZ4Bytes{string json="";public static LZ4Bytes Create<T>(T value)=>new(){json=JsonConvert.SerializeObject(value)};public T? Decompress<T>()=>JsonConvert.DeserializeObject<T>(json);}
}
namespace ElinTogether.Net{
 public class ElinNetBase{public Queue Delta=new();}public class ElinNetHost:ElinNetBase{}public class ElinNetClient:ElinNetBase{}
 public class Queue{public List<ElinTogether.Models.ElinDelta> Items=[];public void AddRemote(ElinTogether.Models.ElinDelta d)=>Items.Add(d);}
 public class NetSession{public static NetSession Instance=new();public ElinNetBase? Connection;}
}
namespace ElinTogether.Patches{public static class CoroutineHelper{public static void Deferred(Action a,Func<bool> ready){if(ready())a();}}}
namespace MessagePack{public class MessagePackObjectAttribute:Attribute{}public class KeyAttribute(int n):Attribute{}}
namespace HarmonyLib{public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string s){}}public class HarmonyPostfix:Attribute{}}
