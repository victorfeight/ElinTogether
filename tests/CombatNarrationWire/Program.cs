using System.Collections;
using System.Reflection;

// Validate the staged DLL, real Harmony target lookup and the actual EMP wire
// serializer without starting Unity or changing the installed package.
var build=Path.GetFullPath(args[0]);var game=args[1];
var dirs=new[]{build,Path.Combine(game,"Elin_Data/Managed"),Path.Combine(game,"BepInEx/core"),Path.Combine(game,"Package/_ModdingKit"),Path.GetFullPath(Path.Combine(game,"../../workshop/content/2135150/3400020753"))};
AppDomain.CurrentDomain.AssemblyResolve+=(_,e)=>{
 var name=new AssemblyName(e.Name).Name+".dll";
 var file=dirs.Select(d=>Path.Combine(d,name)).FirstOrDefault(File.Exists);
 return file is null?null:Assembly.LoadFrom(file);
};
var asm=Assembly.LoadFrom(Path.Combine(build,"ElinTogether.dll"));
int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
foreach(var name in new[]{"CombatNarrationScopePatch","CombatNarrationMessagePatch","CombatNarrationSpeakerPatch"}) {
 var type=asm.GetType("ElinTogether.Patches."+name,true)!;
 var targets=((IEnumerable)type.GetMethod("TargetMethods",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!).Cast<MethodBase>().ToArray();
 Check(targets.Length==(name.Contains("Message")?6:3)&&targets.All(t=>t!=null),name+" targets resolve against installed stable");
 foreach(var target in targets) Console.WriteLine("  "+target.DeclaringType!.Name+"."+target);
 if(name.Contains("Speaker"))Check(targets.All(t=>t.GetParameters()[0].Name=="lang"),"Card.Say Harmony argument name matches stable");
}
var textType=asm.GetType("ElinTogether.Models.CombatText",true)!;
var deltaType=asm.GetType("ElinTogether.Models.CombatNarrationDelta",true)!;
var baseType=asm.GetType("ElinTogether.Models.ElinDelta",true)!;
var text=Activator.CreateInstance(textType)!;
void Set(object o,string p,object? v)=>o.GetType().GetProperty(p)!.SetValue(o,v);
Set(text,"Template","#1 #3 #2 and");Set(text,"Fallback","Farris smashes a grudge and");
Set(text,"Kind",Enum.Parse(textType.GetNestedType("Format")!,"Cards"));
Set(text,"Refs",new string?[]{"smash(es)",null});Set(text,"R",0.25f);Set(text,"A",1f);
var remote=Activator.CreateInstance(asm.GetType("ElinTogether.Models.RemoteCard",true)!)!;
Set(remote,"Uid",123);Set(text,"First",remote);Set(text,"Speaker",remote);
var texts=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(textType))!;texts.Add(text);
var newline=Activator.CreateInstance(textType)!;Set(newline,"NewLine",true);texts.Add(newline);
var packet=Activator.CreateInstance(deltaType)!;Set(packet,"Text",texts);
var deltas=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(baseType))!;deltas.Add(packet);
var envelopeType=asm.GetType("ElinTogether.Models.WorldStateDeltaList",true)!;
var envelope=Activator.CreateInstance(envelopeType)!;Set(envelope,"DeltaList",deltas);
var serializerType=asm.GetType("ElinTogether.Net.Steam.SteamNetSerializer",true)!;var serializer=Activator.CreateInstance(serializerType,true)!;
var bytes=(byte[])serializerType.GetMethod("Serialize")!.MakeGenericMethod(envelopeType).Invoke(serializer,[envelope])!;
var result=serializerType.GetMethod("Deserialize")!.Invoke(serializer,[bytes.Skip(4).ToArray(),envelopeType])!;
var returned=((IEnumerable)envelopeType.GetProperty("DeltaList")!.GetValue(result)!).Cast<object>().Single();
Check(returned.GetType()==deltaType,"real EMP envelope round-trips the new union member");
var returnedText=((IEnumerable)deltaType.GetProperty("Text")!.GetValue(returned)!).Cast<object>().ToArray();
Check(returnedText.Length==2&&(string)textType.GetProperty("Template")!.GetValue(returnedText[0])! == "#1 #3 #2 and","native selected template and fragment order survive serialization");
Check(((string?[])textType.GetProperty("Refs")!.GetValue(returnedText[0])!)[1]==null,"optional native arguments survive serialization");
Check((bool)textType.GetProperty("NewLine")!.GetValue(returnedText[1])!,"line boundary survives serialization");
Check((float)textType.GetProperty("R")!.GetValue(returnedText[0])! == 0.25f,"fragment color survives serialization");
var returnedActor=textType.GetProperty("First")!.GetValue(returnedText[0])!;
Check((int)returnedActor.GetType().GetProperty("Uid")!.GetValue(returnedActor)! == 123,"actor identity survives serialization");
var version=asm.GetType("ElinTogether.Common.BuildVersionIntegrity",true)!.GetField("APIVersionLatest")!.GetRawConstantValue();
Check(Convert.ToInt32(version)==55,"protocol bumped to prevent old clients joining this presentation contract");
Console.WriteLine($"{checks} real-assembly checks passed; {bytes.Length} bytes for test envelope.");
