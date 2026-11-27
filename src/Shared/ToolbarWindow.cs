using System.Runtime.InteropServices;
using System.Text.Json;
namespace ACAI2.Rynth.Ui;

// Source-linked into each app: no extra DLL or engine patch. This is the public
// RynthPluginGetSectionsJson windows schema, not a replacement native manifest.
internal sealed class ToolbarWindow
{
    public string Title{get;}
    public string Surface{get;}
    public string Json{get;}
    public IntPtr Manifest{get;}
    public ToolbarWindow(string id,string title,string icon,int width,int height)
    {
        Title=title;Surface="window:"+id;
        Json=JsonSerializer.Serialize(new{version=1,windows=new[]{new{id,title,surface=Surface,icon,width,height,minWidth=320,minHeight=240,bar=true}}});
        Manifest=Marshal.StringToCoTaskMemUTF8(Json); // Stable for the loaded DLL's lifetime, like Name/Version exports.
    }
    public bool Accepts(string? surface)=>surface==Surface||surface==Surface+":popout";
    public int Draw(string? surface,float width,float height,Action draw)
    {
        if(!Accepts(surface)||!float.IsFinite(width)||!float.IsFinite(height)||width<=0||height<=0)return 0;
        try{draw();return 1;}catch{return -2;} // Never let an exception cross an unmanaged export.
    }
}
internal sealed class ToolbarVisibility(ToolbarWindow window)
{
    private bool? requested;
    public void Request(bool open)=>requested=open;
    public void Reset()=>requested=null;
    // Ask=-1, Close=0, Open=1. Pending commands retry until registration, but
    // an old saved Visible=true must never reopen a panel the player closed.
    public void Sync(Func<string,int,int> panel,Action<bool> changed)
    {
        int state=panel(window.Title,requested.HasValue?(requested.Value?1:0):-1);
        if(state<0)return;requested=null;changed(state==1);
    }
}
