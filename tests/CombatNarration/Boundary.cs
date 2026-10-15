using System.Reflection;
using System.Text.RegularExpressions;
using ElinTogether.Models;

// Simulated game boundary; production capture, format selection and packet code
// are linked into this project. This does not replace Unity acceptance testing.
namespace MessagePack {
 public class MessagePackObjectAttribute : Attribute { }
 public class KeyAttribute(int key) : Attribute { public int Key=key; }
}
namespace HarmonyLib {
 public enum MethodType { Getter }
 [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple=true)]
 public class HarmonyPatch : Attribute {
  public HarmonyPatch() {} public HarmonyPatch(Type t, string n) {} public HarmonyPatch(Type t,string n,MethodType m) {}
 }
 public class HarmonyPrefix : Attribute { } public class HarmonyPostfix : Attribute { } public class HarmonyFinalizer : Attribute { }
 public static class AccessTools {
  public static MethodInfo Method(Type t,string n,Type[]? a=null) => (a is null ? t.GetMethod(n) : t.GetMethod(n,a))!;
  public static List<MethodInfo> GetDeclaredMethods(Type t)=>t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).ToList();
 }
}
namespace UnityEngine { public struct Color(float r,float g,float b,float a) {public float r=r,g=g,b=b,a=a;} }
public class EClass { public static Sources sources=new(); public static Card pc=null!; }
public class Sources {public LangTable langGame=new();}
public class LangTable {public Dictionary<string,Row> map=[]; public class Row {public string group="combat";} }
public class Card { public string Name=""; public bool IsPC=>ReferenceEquals(this,EClass.pc); public bool Male; public bool isSynced=true; public object? parent; public bool ShouldShowMsg=>IsPC||parent==EClass.pc||isSynced;
 public void Say(string lang,string? ref1=null,string? ref2=null) {} public void DamageHP(long d,int e,int p,AttackSource a,Card c,bool s,Thing w,Chara t,int r){} }
public class Chara:Card {public bool isDead;}
public class Thing:Card {}
public enum AttackSource {Melee}
public class AttackProcess {public void Perform() {}}
public class ActMelee {public void Attack() {}}
public class Person {
 public string him="it"; public void Set(string? s) {him="it";}
 public void Set(Card? c) {him=c is null?"it":c.IsPC?"you":c.Male?"him":"her";}
}
public static class Msg {
 public static Person thirdPerson1=new(),thirdPerson2=new(); public static bool ignoreAll,alwaysVisible;
 public static UnityEngine.Color currentColor=new(1,1,1,1); public static List<string> Output=[];
 public static void SetColor()=>currentColor=new(1,1,1,1); public static void SetColor(UnityEngine.Color c)=>currentColor=c;
 public static void NewLine()=>Output.Add("<line>"); public static string SayRaw(string text){Output.Add(text);return text;}
 public static string GetGameText(string id)=>id;
 public static bool IsThirdPerson(Card? c)=>c is null||!c.IsPC;
 public static bool IsThirdPerson(string? s)=>true;
 public static bool IsThirdPerson(int n)=>n<=1;
 public static string GetName(Card? c)=>c is null?"null":c.IsPC?"you":c.Name;
 public static string Say(string idLang)=>idLang;
 public static string Say(string idLang,string? r1,string? r2=null,string? r3=null,string? r4=null)=>idLang;
 public static string Say(string idLang,Card c1,Card c2,string? r1=null,string? r2=null)=>idLang;
 public static string Say(string idLang,Card c1,string? r1=null,string? r2=null,string? r3=null)=>idLang;
 public static string Say(string idLang,Card c1,int i,string? r1=null)=>idLang;
 public static string Say(string idLang,int i,string? r1=null,string? r2=null)=>idLang;
}
public static class GameLang {
 public static string Parse(string text,bool thirdPerson,string? v1,string? v2=null,string? v3=null,string? v4=null) {
  var values=new[]{v1,v2,v3,v4}; for(int i=0;i<4;i++)if(values[i]!=null)text=text.Replace("#"+(i+1),values[i]);
  return text.Replace("#him2",Msg.thirdPerson2.him).Replace("(s)",thirdPerson?"s":"").Replace("(es)",thirdPerson?"es":"");
 }
}
namespace ElinTogether.Net {
 public class ElinNetBase { public virtual bool IsClient=>false; public Sender Delta=new(); }
 public class ElinNetHost:ElinNetBase {}
 public class ElinNetClient:ElinNetBase {public override bool IsClient=>true;}
 public class Sender {public List<ElinDelta> Sent=[];public void AddRemote(ElinDelta d)=>Sent.Add(d);}
 public class NetSession {public static NetSession Instance=new();public ElinNetBase? Connection;}
}
namespace ElinTogether.Models {
 public class RemoteCard {public Card? Value;public Card? Find()=>Value;public static implicit operator RemoteCard?(Card? c)=>c is null?null:new(){Value=c};}
 public class ElinDelta {protected virtual void OnApply(ElinTogether.Net.ElinNetBase n){}public void Apply(ElinTogether.Net.ElinNetBase n)=>OnApply(n);}
}
namespace ElinTogether.Patches { internal static class MsgRelayContext { internal static bool IsSuppressing; } }
