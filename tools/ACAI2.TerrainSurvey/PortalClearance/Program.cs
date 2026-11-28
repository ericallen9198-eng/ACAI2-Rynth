using System.Text.Json.Nodes;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using RynthNav.Routing;
var mesh=new DtNavMesh();mesh.Init(new DtNavMeshParams{orig=new RcVec3f(0,0,0),tileWidth=192,tileHeight=192,maxTiles=1024,maxPolys=1<<16},6);
foreach(var f in Directory.GetFiles(args[0],"nav_*.tile")){using var br=new BinaryReader(File.OpenRead(f));var md=new DtMeshDataReader().Read(br,6);if(mesh.AddTile(md,0,0,out _).Failed())throw new Exception("Cannot add tile");}
var q=new DtNavMeshQuery(mesh);var plain=new DtQueryDefaultFilter();
const double cx=6449.813011,cy=35292.236633,radius=12;
var avoid=new PortalAvoid();avoid.Set(new[]{new PortalAvoid.Spot(cx,cy,radius+1,"Known camp portal staging")},Array.Empty<PortalAvoid.Spot>());
var filter=new AvoidFilter(avoid);
RcVec3f From(JsonNode p)=>new((float)(p["east"]!.GetValue<double>()*240+24468),(float)p["z"]!.GetValue<double>(),(float)(p["north"]!.GetValue<double>()*240+24468));
JsonObject To(RcVec3f p)=>new(){["east"]=((double)p.X-24468)/240,["north"]=((double)p.Z-24468)/240,["z"]=(double)p.Y};
double Dist(RcVec3f a,RcVec3f b)=>PortalAvoid.SegmentDistance(cx,cy,a.X,a.Z,b.X,b.Z);
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
var input=Path.Combine(args[1],"hub-275-approaches.json");var data=JsonNode.Parse(File.ReadAllText(input))!.AsArray();int changed=0;
foreach(var h in data){var a=h!["corners"]!.AsArray();var pts=a.Select(p=>From(p!)).ToArray();int last=-1;for(int i=1;i<pts.Length;i++)if(Dist(pts[i-1],pts[i])<radius)last=i;if(last<0)continue;while(last<pts.Length-1&&Math.Sqrt(Math.Pow(pts[last].X-cx,2)+Math.Pow(pts[last].Z-cy,2))<25)last++;
 var replacement=Plan(pts[0],pts[last]);var result=new JsonArray();foreach(var p in replacement)result.Add(To(p));for(int i=last+1;i<pts.Length;i++)result.Add(a[i]!.DeepClone());h["corners"]=result;
 double length=0;var all=result.Select(p=>From(p!)).ToArray();for(int i=1;i<all.Length;i++){if(Dist(all[i-1],all[i])<radius)throw new Exception("Remaining portal crossing");length+=Math.Sqrt(Math.Pow(all[i].X-all[i-1].X,2)+Math.Pow(all[i].Z-all[i-1].Z,2));}h["length"]=length;changed++;
 Console.WriteLine($"Replanned hub approach {h["approach"]}: {replacement.Length} portal-clear corners");
}
File.WriteAllText(input,data.ToJsonString(new(){WriteIndented=true}));Console.WriteLine($"PASS {changed} hub approaches replanned around 12-yard portal staging exclusion; new segments raycast checked.");
