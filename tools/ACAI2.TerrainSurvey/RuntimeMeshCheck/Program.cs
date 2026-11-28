using System.Text.Json;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using RynthNav.Routing;
using RynthCore.Plugin.Shared;
using ACAI2.Rynth.Terrain.Plugin;
var mesh=new DtNavMesh();mesh.Init(new DtNavMeshParams{orig=new RcVec3f(0,0,0),tileWidth=192,tileHeight=192,maxTiles=1024,maxPolys=1<<16},6);
foreach(var file in Directory.GetFiles(args[0],"nav_*.tile")){using var br=new BinaryReader(File.OpenRead(file));var md=new DtMeshDataReader().Read(br,6);if(mesh.AddTile(md,0,0,out _).Failed())throw new Exception("Cannot load official tile");}
var q=new DtNavMeshQuery(mesh);
using var doc=JsonDocument.Parse(File.ReadAllText(args[1]));
using var pd=JsonDocument.Parse(File.ReadAllText(args[2]));
var portals=pd.RootElement.GetProperty("links").EnumerateArray().Select(l=>Read(l.GetProperty("nearby_position"))).Distinct().ToArray();
var circles=new PortalAvoid();circles.Set(portals.Select(p=>new PortalAvoid.Spot(p.X,p.Y,16,"Camp portal")),[]);var filter=new AvoidFilter(circles);
Ground Read(JsonElement p)=>new(p.GetProperty("east").GetDouble()*240+24468,p.GetProperty("north").GetDouble()*240+24468,p.GetProperty("z").GetDouble());
Ground[] Find(Ground a,Ground b)
{
 var r=new PathExResult();int result=NavPathQuery.FindPathEx(q,a.X,a.Y,a.Z,b.X,b.Y,b.Z,11,128,filter,1,r);
 if(result!=RynthNavPathApiV1.ResultOk||r.Partial)throw new InvalidDataException(r.Note);
 return r.Corners.Select(p=>new Ground(p.X,p.Y,p.Z)).ToArray();
}
bool Clear(Ground a,Ground b)=>NavPathQuery.Raycast(q,a.X,a.Y,a.Z,b.X,b.Y,b.Z,out _)==1;
int passed=0;var report=new List<object>();
foreach(int z in new[]{275,180,225})
{
 var p=doc.RootElement.GetProperty("corridors").GetProperty("Hub_"+z+"_01").EnumerateArray().Select(Read).ToArray();
 int index=1;double distance=p[0].Flat(p[index]);while(index<p.Length-1&&distance<40){distance+=p[index].Flat(p[index+1]);index++;}
 for(int variant=0;variant<=4;variant++)try
 {
  var route=MeshPlan.Build(p[0],p[index],variant,Find,Clear,portals);passed++;report.Add(new{zone=z,variant,accepted=true,corners=route.Length});
  Console.WriteLine($"PASS {z}, variant {variant}: {route.Length} corners from genuine native path query and raycasts");
 }catch(InvalidDataException ex){report.Add(new{zone=z,variant,accepted=false,reason=ex.Message});Console.WriteLine($"REFUSED {z}, variant {variant}: {ex.Message}");if(variant==0)throw;}
}
File.WriteAllText(args[3],JsonSerializer.Serialize(new{passed,cases=report,scope="First survey mesh step at each camp; official 60 camp tiles; no live game"},new JsonSerializerOptions{WriteIndented=true}));
