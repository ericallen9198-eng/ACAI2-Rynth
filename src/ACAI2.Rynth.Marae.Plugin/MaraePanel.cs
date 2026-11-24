using System.Numerics;
using ImGuiNET;
using ACAI2.Rynth.Marae.Core;
namespace ACAI2.Rynth.Marae.Plugin;
internal sealed record MaraeView(string Character,string Phase,string Detail,bool Running,bool Review,MaraeOptions Options,string Folder,bool Visible,float X,float Y);
internal sealed record PanelRequest(string Command,MaraeOptions? Options=null,string? Folder=null,bool Visible=true,float X=0,float Y=0);
internal sealed class MaraePanel
{
    private int frame=-1;private bool placed,advanced;private float x,y;private string folder="";
    public void Reset(){placed=false;}
    public void Draw(MaraeView v,Action<PanelRequest> send)
    {
        if(!v.Visible||ImGui.GetCurrentContext()==IntPtr.Zero||frame==ImGui.GetFrameCount())return;frame=ImGui.GetFrameCount();
        if(!placed){x=v.X;y=v.Y;folder=v.Folder;ImGui.SetNextWindowPos(new(x,y),ImGuiCond.Always);placed=true;}
        ImGui.SetNextWindowSize(new(450,380),ImGuiCond.FirstUseEver);ImGui.SetNextWindowBgAlpha(.94f);
        bool visible=v.Visible;ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,8);
        ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(.035f,.055f,.065f,1));ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(.08f,.30f,.28f,1));
        try
        {
            bool expanded=ImGui.Begin("MARAE LASSEL / "+v.Character+"###acai2-marae",ref visible);
            try
            {
                var pos=ImGui.GetWindowPos();if(x!=pos.X||y!=pos.Y||visible!=v.Visible){x=pos.X;y=pos.Y;send(new("appearance",Visible:visible,X:x,Y:y));}
                if(!expanded)return;
                ImGui.TextColored(new(.38f,.9f,.77f,1),v.Phase);ImGui.TextWrapped(v.Detail);ImGui.Separator();
                var p=v.Options;var areas=p.Areas.ToArray();bool changed=false;
                ImGui.BeginDisabled(v.Running);
                if(ImGui.BeginTable("areas",3,ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Area",ImGuiTableColumnFlags.WidthStretch,2);ImGui.TableSetupColumn("Use maps");ImGui.TableSetupColumn("Hunt only");ImGui.TableHeadersRow();
                    for(int i=0;i<3;i++)
                    {
                        var a=areas[i];bool enabled=a.Enabled,maps=a.Maps,hunt=a.HuntOnly;ImGui.TableNextRow();ImGui.TableSetColumnIndex(0);
                        changed|=ImGui.Checkbox(a.Level+(i==0?" Lowlands":i==1?" High Country":" Plateau"),ref enabled);ImGui.TableSetColumnIndex(1);
                        changed|=ImGui.Checkbox("##maps"+a.Level,ref maps);ImGui.TableSetColumnIndex(2);changed|=ImGui.Checkbox("##hunt"+a.Level,ref hunt);
                        areas[i]=a with {Enabled=enabled,Maps=maps,HuntOnly=hunt};
                    }
                    ImGui.EndTable();
                }
                ImGui.TextWrapped("Hunt only skips NPC tasks and repeats the route. Maps are optional in either mode.");
                bool mesh=p.Mesh,cap=p.CampLimit,fallback=p.Fallback;
                changed|=ImGui.Checkbox("Mesh map travel (RynthNav)",ref mesh);changed|=ImGui.Checkbox("2-hour task limit per camp",ref cap);
                changed|=ImGui.Checkbox("Fallback to 180 after tasks",ref fallback);ImGui.EndDisabled();
                int close=p.Close,far=p.Far;
                if(mesh){changed|=ImGui.SliderInt("Close clue",ref close,10,60,"%d yd");changed|=ImGui.SliderInt("Far clue",ref far,60,180,"%d yd");ImGui.TextDisabled("Travel distances apply to the next clue step.");}
                if(changed)send(new("options",new(areas,mesh,close,far,cap,fallback)));
                ImGui.BeginDisabled(v.Running||v.Review);if(ImGui.Button("Start / Resume"))send(new("start"));ImGui.SameLine();if(ImGui.Button("New run"))send(new("new"));ImGui.EndDisabled();
                ImGui.SameLine();if(ImGui.Button("Stop"))send(new("stop"));
                if(v.Review){ImGui.TextWrapped("An item move was not confirmed. Check your inventory/cache before resuming.");if(ImGui.Button("I reviewed the pickup — resume"))send(new("review"));}
                if(ImGui.SmallButton(advanced?"Hide advanced":"Advanced"))advanced=!advanced;
                if(advanced){ImGui.BeginDisabled(v.Running);if(ImGui.InputText("RynthAi folder",ref folder,1024))send(new("folder",Folder:folder));ImGui.EndDisabled();ImGui.TextWrapped("Uses the normal RynthAi DLL. Stop and disable autostart for the old MaraeLassel Lua script before using this app.");}
            }
            finally{ImGui.End();}
        }
        finally{ImGui.PopStyleColor(2);ImGui.PopStyleVar();}
    }
}
