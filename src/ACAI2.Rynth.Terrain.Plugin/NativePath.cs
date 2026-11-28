using RynthCore.Plugin.Shared;
using RynthCore.PluginSdk;
namespace ACAI2.Rynth.Terrain.Plugin;
internal sealed unsafe class NativePath(RynthCoreHost host)
{
    private RynthNavPathApiV1* Table()
    {
        var t=(RynthNavPathApiV1*)host.GetPluginInterface("RynthNav",RynthNavPathApiV1.InterfaceName,2);
        if(t==null||t->Version<2||t->StructSize<sizeof(RynthNavPathApiV1)||t->FindPathEx==null||t->Raycast==null)
            throw new InvalidOperationException("Enable a compatible stock RynthNav");
        return t;
    }
    public Ground[] Find(Ground a,Ground b)
    {
        double* points=stackalloc double[384];double* info=stackalloc double[RynthNavPathApiV1.InfoLength];
        for(int i=0;i<RynthNavPathApiV1.InfoLength;i++)info[i]=double.NaN;
        int count=0;var t=Table();
        int flags=RynthNavPathApiV1.PathAvoidPortals|RynthNavPathApiV1.PathWallClearance|RynthNavPathApiV1.PathQuiet;
        int result=t->FindPathEx(a.X,a.Y,a.Z,b.X,b.Y,b.Z,flags,25,points,128,&count,info);
        if(result!=RynthNavPathApiV1.ResultOk||count is <2 or >128||info[1]!=0)
            throw new InvalidDataException($"No complete mesh path (result {result}, corners {count}; partial/straight fallback refused)");
        var path=new Ground[count];
        for(int i=0;i<count;i++)path[i]=new(points[3*i],points[3*i+1],points[3*i+2]);
        return path;
    }
    public bool Clear(Ground a,Ground b)=>Table()->Raycast(a.X,a.Y,a.Z,b.X,b.Y,b.Z)==RynthNavPathApiV1.RayClear;
}
