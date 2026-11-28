using System.Text.Json.Nodes;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using RynthNav.Routing;
var mesh=new DtNavMesh();mesh.Init(new DtNavMeshParams{orig=new RcVec3f(0,0,0),tileWidth=192,tileHeight=192,maxTiles=1024,maxPolys=1<<16},6);
foreach(var f in Directory.GetFiles(args[0],"nav_*.tile")){using var br=new BinaryReader(File.OpenRead(f));var md=new DtMeshDataReader().Read(br,6);if(mesh.AddTile(md,0,0,out _).Failed())throw new Exception("Cannot add tile");}
var q=new DtNavMeshQuery(mesh);var plain=new DtQueryDefaultFilter();
const double radius=15;
var zoneData=JsonNode.Parse(File.ReadAllText(Path.Combine(args[1],"camp-portals.json")))!["links"]!.AsArray();
var spots=zoneData.Select(l=>l!["nearby_position"]!).Select(p=>(X:p["east"]!.GetValue<double>()*240+24468,Y:p["north"]!.GetValue<double>()*240+24468)).Distinct().ToArray();
var avoid=new PortalAvoid();avoid.Set(spots.Select(p=>new PortalAvoid.Spot(p.X,p.Y,radius+1,"Known portal neighborhood")),Array.Empty<PortalAvoid.Spot>());
var filter=new AvoidFilter(avoid);
RcVec3f From(JsonNode p)=>new((float)(p["east"]!.GetValue<double>()*240+24468),(float)p["z"]!.GetValue<double>(),(float)(p["north"]!.GetValue<double>()*240+24468));
JsonObject To(RcVec3f p)=>new(){["east"]=((double)p.X-24468)/240,["north"]=((double)p.Z-24468)/240,["z"]=(double)p.Y};
double Dist(RcVec3f a,RcVec3f b)=>spots.Min(p=>PortalAvoid.SegmentDistance(p.X,p.Y,a.X,a.Z,b.X,b.Z));
bool Clear(RcVec3f a,RcVec3f b){q.FindNearestPoly(a,new RcVec3f(2.5f,4,2.5f),plain,out long ar,out var at,out _);if(ar==0)return false;var ps=new long[512];var st=q.Raycast(ar,at,b,plain,out float t,out _,ps,out _,ps.Length);return !st.Failed()&&t>=.999f&&Dist(a,b)>=radius;}
RcVec3f[] Plan(RcVec3f a,RcVec3f b){
 q.FindNearestPoly(a,new RcVec3f(2.5f,4,2.5f),plain,out long ar,out var ap,out _);q.FindNearestPoly(b,new RcVec3f(2.5f,4,2.5f),plain,out long br,out var bp,out _);
 if(ar==0||br==0)throw new Exception("Endpoint missing mesh");filter.AllowA=ar;filter.AllowB=br;
 var path=new long[8192];var st=q.FindPath(ar,br,ap,bp,filter,path,out int n,path.Length);if(st.Failed()||n==0||path[n-1]!=br)throw new Exception("No complete portal-clear corridor");
 var corners=new DtStraightPath[8192];st=q.FindStraightPath(ap,bp,path,n,corners,out int count,corners.Length,2);if(st.Failed())throw new Exception("No corners");var pts=corners.Take(count).Select(p=>p.pos).ToArray();
 // Resolve shared-edge nearest-polygon ambiguity without changing corridor endpoints.
 for(int pass=0;pass<6;pass++)for(int i=1;i<pts.Length-1;i++)if(!Clear(pts[i-1],pts[i])||!Clear(pts[i],pts[i+1])){
  var old=pts[i];bool found=false;
  foreach(float d in new[]{.125f,.25f,.5f}){for(int k=0;k<16;k++){
   double angle=k*Math.PI/8;var candidate=new RcVec3f(old.X+(float)Math.Cos(angle)*d,old.Y,old.Z+(float)Math.Sin(angle)*d);
   q.FindNearestPoly(candidate,new RcVec3f(.5f,4,.5f),plain,out long cr,out var at,out _);if(cr==0||Math.Abs(at.Y-old.Y)>4)continue;
   if(Clear(pts[i-1],at)&&Clear(at,pts[i+1])){pts[i]=at;found=true;break;}
  }if(found)break;}
 }
 for(int i=1;i<pts.Length;i++)if(!Clear(pts[i-1],pts[i]))throw new Exception("Portal-clear path failed independent segment check "+i);
 return pts;
}

int changed=0,newSegments=0;double minimum=double.MaxValue;
JsonArray Fix(JsonArray array,string label){
 var pts=array.Select(p=>From(p!)).ToArray();int first=-1,last=-1;
 for(int i=1;i<pts.Length;i++)if(Dist(pts[i-1],pts[i])<radius){if(first<0)first=i-1;last=i;}
 if(first<0)return array;
 while(first>0&&Dist(pts[first],pts[first])<28)first--;
 while(last<pts.Length-1&&Dist(pts[last],pts[last])<28)last++;
 var repl=Plan(pts[first],pts[last]);var result=new JsonArray();
 for(int i=0;i<first;i++)result.Add(array[i]!.DeepClone());foreach(var p in repl)result.Add(To(p));for(int i=last+1;i<pts.Length;i++)result.Add(array[i]!.DeepClone());
 var output=result.Select(p=>From(p!)).ToArray();for(int i=1;i<output.Length;i++)if(Dist(output[i-1],output[i])<radius)throw new Exception("Remaining portal crossing: "+label);
 changed++;newSegments+=repl.Length-1;Console.WriteLine($"Replanned {label}: {repl.Length} checked corners");return result;
}
double Length(JsonArray a){var p=a.Select(x=>From(x!)).ToArray();return Enumerable.Range(1,p.Length-1).Sum(i=>Math.Sqrt(Math.Pow(p[i].X-p[i-1].X,2)+Math.Pow(p[i].Z-p[i-1].Z,2)));}
var outputs=new Dictionary<string,JsonNode>();
foreach(int z in new[]{180,225,275}){
 var hp=Path.Combine(args[1],$"hub-{z}-approaches.json");var hubs=JsonNode.Parse(File.ReadAllText(hp))!.AsArray();int i=0;
 foreach(var h in hubs){h!["corners"]=Fix(h["corners"]!.AsArray(),$"hub {z}/{++i}").DeepClone();h["length"]=Length(h["corners"]!.AsArray());}outputs[hp]=hubs;
 var rp=Path.Combine(args[1],$"route-{z}.json");var route=JsonNode.Parse(File.ReadAllText(rp))!;i=0;
 foreach(var leg in route["legs"]!.AsArray()){leg!["corners"]=Fix(leg["corners"]!.AsArray(),$"leg {z}/{++i}").DeepClone();leg["length"]=Length(leg["corners"]!.AsArray());}
 route["returnCorners"]=Fix(route["returnCorners"]!.AsArray(),$"return {z}").DeepClone();var all=new JsonArray();
 foreach(var l in route["legs"]!.AsArray())foreach(var p in l!["corners"]!.AsArray())all.Add(p!.DeepClone());foreach(var p in route["returnCorners"]!.AsArray())all.Add(p!.DeepClone());route["corners"]=all;route["length"]=Length(all);outputs[rp]=route;
}
foreach(var (file,node)in outputs)File.WriteAllText(file,node.ToJsonString(new(){WriteIndented=true}));
foreach(var node in outputs.Values){IEnumerable<JsonArray> arrays=node is JsonArray hubs?hubs.Select(h=>h!["corners"]!.AsArray()):new[]{node["corners"]!.AsArray()};foreach(var ps in arrays){var points=ps.Select(p=>From(p!)).ToArray();for(int i=1;i<points.Length;i++)minimum=Math.Min(minimum,Dist(points[i-1],points[i]));}}
var report=new{radius_yards=radius,nearby_positions=spots.Length,paths_replanned=changed,new_segments_raycast_checked=newSegments,minimum_planned_clearance_yards=minimum,exact_portal_centers=false};
File.WriteAllText(Path.Combine(args[1],"portal-clearance-check.json"),System.Text.Json.JsonSerializer.Serialize(report,new System.Text.Json.JsonSerializerOptions(){WriteIndented=true}));
Console.WriteLine($"PASS {changed} paths, {newSegments} checked replacement segments, minimum planned clearance {minimum:F2} yards.");
