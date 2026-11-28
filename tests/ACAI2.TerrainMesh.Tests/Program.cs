using ACAI2.Rynth.Terrain.Plugin;
var a=new Ground(30000,40000,92);var b=new Ground(30100,40000,92);
int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
void Refused(Action action,string name){try{action();}catch(InvalidDataException){Check(true,name);return;}throw new Exception("Accepted "+name);}
Ground[] Direct(Ground from,Ground to)=>[from,to];
Ground[] Make(int v=0)=>MeshPlan.Build(a,b,v,Direct,(_,_)=>true,[]);
Check(Make().Length==2,"complete checked mesh path accepted");
for(int v=1;v<=4;v++)
{
 var p=Make(v);Check(p.Length==3&&Math.Abs(p[1].Y-a.Y)==(v<=2?10:18)&&p[^1]==b,"bounded mesh side approach "+v);
}
Refused(()=>MeshPlan.Build(a,b,0,(_,_)=>[],(_,_)=>true,[]),"no path never becomes straight movement");
Refused(()=>MeshPlan.Build(a,b,0,(_,_)=>[a,new(b.X-20,b.Y,b.Z)],(_,_)=>true,[]),"short partial endpoint refused");
Refused(()=>MeshPlan.Build(a,b,0,(_,_)=>[a,new(b.X,b.Y,20)],(_,_)=>true,[]),"lower floor at same coordinates refused");
Refused(()=>MeshPlan.Build(a,b,0,(_,_)=>[new(a.X,a.Y,20),b],(_,_)=>true,[]),"wrong starting layer refused");
Refused(()=>MeshPlan.Build(a,b,0,Direct,(_,_)=>false,[]),"blocked mesh segment refused");
Refused(()=>MeshPlan.Build(a,b,0,Direct,(_,_)=>true,[new(a.X+50,a.Y+14,a.Z)]),"entire segment keeps fifteen yards from portal anchors");
Check(MeshPlan.Build(a,b,0,Direct,(_,_)=>true,[new(a.X+50,a.Y+16,a.Z)]).Length==2,"outside portal exclusion accepted");
Refused(()=>MeshPlan.Build(a,b,0,(_,_)=>[a,new(double.NaN,1,1),b],(_,_)=>true,[]),"invalid corner refused");
Refused(()=>MeshPlan.Build(a,b,5,Direct,(_,_)=>true,[]),"unbounded side approaches refused");
Refused(()=>MeshPlan.Build(a,new(a.X+601,a.Y,a.Z),0,Direct,(_,_)=>true,[]),"overlong request refused");
Refused(()=>MeshPlan.Build(new(a.X,a.Y,120),new(b.X,b.Y,64),0,Direct,(_,_)=>true,[new(a.X+50,a.Y,92)]),"sloping segment crossing a portal height band refused");
Check(MeshPlan.Serialize(Make()).Split('\n')[0]=="uTank2 NAV 1.2","native route header");
File.WriteAllText(args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"terrain-mesh-test.nav"),MeshPlan.Serialize(Make(1)));
var biggest=Enumerable.Range(0,256).Select(i=>new Ground(30000.12345+i,40000.98765+i,92.12345)).ToArray();
using(var g=System.Text.Json.JsonDocument.Parse(MeshPlan.Geometry(biggest,123,456)))
 Check(g.RootElement.GetProperty("points").GetArrayLength()==256&&g.RootElement.GetProperty("request").GetInt64()==123&&MeshPlan.Geometry(biggest,123,456).Length<12000,"maximum geometry record stays bounded and generation-tagged");
Refused(()=>MeshPlan.Geometry(biggest.Concat(new[]{a}).ToArray(),1,1),"geometry refuses oversized history");

Console.WriteLine($"PASS {checks} mesh policy checks; fake path provider only (not live navigation).");
