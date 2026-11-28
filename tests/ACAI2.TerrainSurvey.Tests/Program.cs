using MoonSharp.Interpreter;
using System.Text.Json;
var script=File.ReadAllText(args[0]);var index=File.ReadAllText(Path.Combine(Path.GetDirectoryName(args[0])!,"probe-index.json"));
Script New(){
 var vm=new Script(CoreModules.Preset_Complete);
 vm.Globals["indexText"]=index;vm.Globals["scriptText"]=script;
 vm.Globals["encodeNative"]=(Func<DynValue,string>)(v=>JsonSerializer.Serialize(ToObject(v)));
 vm.Globals["decodeNative"]=(Func<string,DynValue>)(s=>FromElement(vm,JsonDocument.Parse(s).RootElement));
 vm.DoString(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"harness.lua")));
 vm.DoString(script);return vm;
}
object? ToObject(DynValue v)=>v.Type switch{DataType.Boolean=>v.Boolean,DataType.Number=>v.Number,DataType.String=>v.String,DataType.Table=>v.Table.Pairs.ToDictionary(p=>p.Key.CastToString(),p=>ToObject(p.Value)),_=>null};
DynValue FromElement(Script vm,JsonElement j)=>j.ValueKind switch{
 JsonValueKind.Object=>DynValue.NewTable(ObjectTable(vm,j)),JsonValueKind.Array=>DynValue.NewTable(ArrayTable(vm,j)),JsonValueKind.String=>DynValue.NewString(j.GetString()),JsonValueKind.Number=>DynValue.NewNumber(j.GetDouble()),JsonValueKind.True=>DynValue.True,JsonValueKind.False=>DynValue.False,_=>DynValue.Nil};
Table ObjectTable(Script vm,JsonElement j){var t=new Table(vm);foreach(var p in j.EnumerateObject())t.Set(p.Name,FromElement(vm,p.Value));return t;}
Table ArrayTable(Script vm,JsonElement j){var t=new Table(vm);foreach(var p in j.EnumerateArray())t.Append(FromElement(vm,p));return t;}
var tests=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"scenarios.lua"));
int count=0;
foreach(var scenario in tests.Split("-- SCENARIO ").Skip(1)){
 var name=scenario[..scenario.IndexOf('\n')].Trim();var code=scenario[(scenario.IndexOf('\n')+1)..];
 try{var vm=New();vm.DoString(code);Console.WriteLine("PASS "+name);count++;}catch(InterpreterException ex){throw new Exception(name+": "+ex.DecoratedMessage,ex);}
}
Console.WriteLine($"PASS {count} TerrainSurvey scenarios using genuine MoonSharp; fake game and local filesystem only.");
