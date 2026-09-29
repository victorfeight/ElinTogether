using Newtonsoft.Json;
public static class GameIOContext { public static JsonSerializerSettings Settings = new() { TypeNameHandling = TypeNameHandling.Auto }; }
public static class EmpLog { public static void Debug(string s, params object[] a) {} }
public class Chara { public int uid=1; public List<Condition> conditions=[]; public AI ai=new(); public int Dirty, Refreshes; public void SetDirtySpeed()=>Dirty++; public void Refresh()=>Refreshes++; }
public class AI { public int Cancels; public void Cancel()=>Cancels++; }
public class Container { public Chara? Parent; public void SetParent(Chara? owner=null)=>Parent=owner; }
[JsonObject(MemberSerialization.OptIn)]
public class Condition {
    [JsonProperty] public int id;
    [JsonProperty] public int[] _ints = new int[5];
    public int refVal => _ints[2]; public int refVal2 => _ints[3];
    public Chara owner=null!; public int phase; public Container? elements;
    public int Effects, Removed, Binds, PhaseChanges;
    public virtual bool CancelAI=>false; public virtual bool ShouldRefresh=>false;
    public virtual Container? GetElementContainer()=>elements;
    public virtual void SetOwner(Chara c,bool onDeserialize=false) {owner=c; phase=_ints[0]/10; elements?.SetParent(); elements=new(); elements.SetParent(c); Binds++;}
    public void PlayEffect()=>Effects++;
    public void Kill() { Removed++; owner.conditions.Remove(this); elements?.SetParent(); }
    public void OnChangePhase(int from,int to)=>PhaseChanges++;
}
public class ConTransmute : Condition { public int Changes; public void Change()=>Changes++; }
public class SpecialCondition : Condition {
    [JsonProperty] private int extra;
    public void SetExtra(int value)=>extra=value; public int Extra=>extra;
    public override bool CancelAI=>true; public override bool ShouldRefresh=>true;
    public override void SetOwner(Chara c,bool onDeserialize=false) { if(!onDeserialize)extra=0; base.SetOwner(c,onDeserialize); }
}
namespace ElinTogether.Models {
 public class LZ4Bytes {
    public string Data="";
    public static LZ4Bytes Create<T>(T value)=>new(){Data=JsonConvert.SerializeObject(value,typeof(T),GameIOContext.Settings)};
    public T Decompress<T>()=>JsonConvert.DeserializeObject<T>(Data,GameIOContext.Settings)!;
 }
}
