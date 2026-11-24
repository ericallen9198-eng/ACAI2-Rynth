using System.Numerics;
using ImGuiNET;
using ACAI2.Rynth.MapHunt.Core;
namespace ACAI2.Rynth.MapHunt.Plugin;
internal sealed class MapHuntPanel
{
    private bool position=true,advanced;
    private float x=260,y=120;
    private string directory="";
    private int frame=-1;
    public bool Visible=true;
    public void Load(MapHuntProfile p){Visible=p.Visible;x=p.X;y=p.Y;directory=p.RynthAiFolder;position=true;}
    public void Draw(string character,MapHuntView view,string notice,Action<string> command,Action<MapHuntPreferences> preferences,Action<bool,float,float,string> appearance)
    {
        if(!Visible||ImGui.GetCurrentContext()==IntPtr.Zero)return;
        int f=ImGui.GetFrameCount();if(frame==f)return;frame=f;
        if(position){ImGui.SetNextWindowPos(new(x,y),ImGuiCond.Always);position=false;}
        ImGui.SetNextWindowSize(new(405,310),ImGuiCond.FirstUseEver);ImGui.SetNextWindowBgAlpha(.92f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,8);
        ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(.035f,.055f,.065f,1));
        ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(.08f,.30f,.28f,1));
        bool visible=Visible;
        try
        {
            bool expanded=ImGui.Begin("MAPHUNT / "+character+"###acai2-maphunt",ref visible);
            try
            {
                var pos=ImGui.GetWindowPos();if(pos.X!=x||pos.Y!=y||visible!=Visible){x=pos.X;y=pos.Y;Visible=visible;appearance(Visible,x,y,directory);}
                if(!expanded)return;
                ImGui.TextColored(new(.38f,.9f,.77f,1),view.Phase);
                ImGui.TextWrapped(view.Detail);
                if(notice.Length>0)ImGui.TextWrapped(notice);
                ImGui.Separator();ImGui.Text("Hunting source: maphunt.nav");
                bool mesh=view.Preferences.Mesh;int close=view.Preferences.Close,far=view.Preferences.Far;
                bool change=ImGui.Checkbox("Mesh clue travel",ref mesh);
                change|=ImGui.SliderInt("Close clue",ref close,10,60,"%d yd");
                change|=ImGui.SliderInt("Far clue",ref far,60,180,"%d yd");
                if(change)preferences(new(mesh,close,far));
                ImGui.TextDisabled("Changes apply to the next clue step.");
                ImGui.BeginDisabled(view.Running&&view.State!="HOLD");
                if(ImGui.Button("Start / Resume",new(150,30)))command("start");
                ImGui.EndDisabled();
                ImGui.SameLine();if(ImGui.Button("Stop",new(85,30)))command("stop");
                if(view.Review)
                {
                    ImGui.TextWrapped("A currency move was not confirmed. Check the inventory and cache before continuing.");
                    if(ImGui.Button("Review complete"))command("review");
                }
                if(ImGui.SmallButton(advanced?"Hide advanced":"Advanced"))advanced=!advanced;
                if(advanced)
                {
                    ImGui.TextWrapped("Native Hunt mode is not available in this beta. Use a recorded route.");
                    ImGui.TextDisabled("RynthAi data folder (contains MetaFiles and NavProfiles)");
                    ImGui.BeginDisabled(view.Running);
                    bool edit=ImGui.InputText("##rynth-ai-folder",ref directory,1024);
                    ImGui.EndDisabled();
                    if(edit)appearance(Visible,x,y,directory);
                    ImGui.TextDisabled("State: "+view.State);
                    ImGui.TextWrapped("Mesh clue travel needs RynthNav and coverage here. A failed mesh path will not silently run straight.");
                }
            }
            finally{ImGui.End();}
        }
        finally{ImGui.PopStyleColor(2);ImGui.PopStyleVar();}
    }
}
