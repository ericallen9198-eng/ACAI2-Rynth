using ACAI2.Rynth.Marae.Core;
using RynthCore.PluginSdk;
namespace ACAI2.Rynth.Marae.Plugin;
internal static unsafe class NativeMesh
{
    // Genuine RynthNav.Path v2 append-only ABI (RynthSuite/Plugins/Shared/RynthNavPathApi.cs, MIT).
    public static RoutePoint[] Find(RynthCoreHost host,double ns,double ew)
    {
        if(!host.TryGetPlayerPose(out uint cell,out float x,out float y,out float z,out _,out _,out _,out _)||(cell&0xffff)>=0x100)throw new InvalidOperationException("Mesh map travel needs an outdoor player pose.");
        IntPtr table=host.GetPluginInterface("RynthNav","RynthNav.Path",2);
        if(table==IntPtr.Zero)throw new InvalidOperationException("Enable RynthNav for mesh map travel.");
        uint* h=(uint*)table;
        if(h[0]<2||h[1]<8+3*IntPtr.Size)throw new InvalidOperationException("Unsupported RynthNav path interface.");
        IntPtr entry=*(IntPtr*)((byte*)table+8+IntPtr.Size);
        if(entry==IntPtr.Zero)throw new InvalidOperationException("Mesh path planner unavailable.");
        var find=(delegate* unmanaged[Cdecl]<double,double,double,double,double,double,int,int,double*,int,int*,double*,int>)entry;
        double* points=stackalloc double[384];double* info=stackalloc double[6];int count=0;
        double sx=(cell>>24)*192d+x,sy=((cell>>16)&255)*192d+y;
        // Avoid portals + wall clearance + quiet. Deliberately do not allow partial paths.
        int result=find(sx,sy,z,ew*240+24468,ns*240+24468,z,11,25,points,128,&count,info);
        if(result!=1||count<2||count>128||info[1]!=0)throw new InvalidOperationException("No complete mesh path to this clue step.");
        var route=new List<RoutePoint>(count-1);
        for(int i=1;i<count;i++)
        {
            double px=points[i*3],py=points[i*3+1],pz=points[i*3+2];
            if(!double.IsFinite(px)||!double.IsFinite(py)||!double.IsFinite(pz))throw new InvalidDataException("Invalid mesh corner.");
            route.Add(new("Point",(py-24468)/240,(px-24468)/240,pz/240));
        }
        if(Math.Sqrt(Math.Pow((route[^1].NS-ns)*240,2)+Math.Pow((route[^1].EW-ew)*240,2))>2)throw new InvalidDataException("Mesh path did not reach its requested goal.");
        return route.ToArray();
    }
}
