using System.Numerics;
using System.Text.Json;
using ACAI2.Rynth.Threads.Core;
using ImGuiNET;
namespace ACAI2.Rynth.Threads.Plugin;

internal sealed class ThreadAppearance
{
    public int Schema {get;set;}=1;
    public PrepPlan Plan {get;set;}=new();
    public bool Visible {get;set;}=true;
    public bool Locked {get;set;}
    public float Opacity {get;set;}=.85f;
    public float X {get;set;}=360;
    public float Y {get;set;}=100;
}
internal sealed class ThreadPanel
{
    private ThreadAppearance a=new();
    private PrepPlan plan=>a.Plan;
    private bool position=true,settings;
    private int frame=-1;
    private string goal="lum";
    public bool Visible {get=>a.Visible;set{a.Visible=value;Dirty=true;}}
    public bool Dirty;
    public void Load(string path)
    {
        a=File.Exists(path)?JsonSerializer.Deserialize<ThreadAppearance>(File.ReadAllText(path))??new():new();
        a.Plan??=new();
        if(a.Schema!=1)throw new InvalidDataException("Unsupported Thread UI settings schema");
        a.Opacity=Math.Clamp(float.IsFinite(a.Opacity)?a.Opacity:.85f,.15f,1);
        if(!float.IsFinite(a.X)||a.X<0)a.X=360;if(!float.IsFinite(a.Y)||a.Y<0)a.Y=100;position=true;
    }
    public void Save(string path)
    {string temporary=path+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(a));File.Move(temporary,path,true);}
    public void Draw(string character,ThreadView view,PrepMachine machine,string notice,Action<PrepPlan> start,Action stop,Action<string> command)
    {
        if(!a.Visible||ImGui.GetCurrentContext()==IntPtr.Zero)return;
        int current=ImGui.GetFrameCount();if(frame==current)return;frame=current;
        if(position){ImGui.SetNextWindowPos(new(a.X,a.Y),ImGuiCond.Always);position=false;}
        ImGui.SetNextWindowSize(new(440,420),ImGuiCond.FirstUseEver);ImGui.SetNextWindowBgAlpha(a.Opacity);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,8);
        ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(.035f,.055f,.065f,1));
        ImGui.PushStyleColor(ImGuiCol.Header,new Vector4(.08f,.3f,.28f,1));
        bool visible=a.Visible;
        try
        {
            bool expanded=ImGui.Begin("THREADS / "+character+"###acai2-threads",ref visible,a.Locked?ImGuiWindowFlags.NoMove:ImGuiWindowFlags.None);
            try
            {
                if(visible!=a.Visible){a.Visible=visible;Dirty=true;}
                if(!expanded)return;
                var pos=ImGui.GetWindowPos();if(pos.X!=a.X||pos.Y!=a.Y){a.X=pos.X;a.Y=pos.Y;Dirty=true;}
                ImGui.TextColored(new(.38f,.9f,.77f,1),"Thread journal + gem preparation");
                ImGui.SameLine();if(ImGui.SmallButton("Settings"))settings=!settings;
                if(settings)
                {
                    bool locked=a.Locked;if(ImGui.Checkbox("Lock position",ref locked)){a.Locked=locked;Dirty=true;}
                    float opacity=a.Opacity;if(ImGui.SliderFloat("Background",ref opacity,.15f,1)){a.Opacity=opacity;Dirty=true;}
                    if(ImGui.Button("Export local records"))command("export");
                }
                ImGui.TextDisabled(view.Runs+" runs | "+view.Presses+" presses | local records only");
                if(ImGui.BeginTabBar("threads-tabs"))
                {
                    if(ImGui.BeginTabItem("Current Run"))
                    {
                        foreach(string line in view.Current)ImGui.TextWrapped(line);
                        if(ImGui.Button("Mark entered"))command("entered");ImGui.SameLine();if(ImGui.Button("Mark outside"))command("outside");
                        if(ImGui.Button("Link latest completed press"))command("link");
                        ImGui.TextDisabled("Manual links are labeled; no gem identity is guessed.");
                        if(ImGui.Button("Easy"))command("rate easy");ImGui.SameLine();if(ImGui.Button("Tough"))command("rate tough");ImGui.SameLine();if(ImGui.Button("Nightmare"))command("rate nightmare");
                        if(ImGui.CollapsingHeader("History"))
                            foreach(string row in view.History){int split=row.IndexOf('|');if(split>0&&ImGui.Selectable(row[(split+1)..]+"##"+row[..split]))command("run "+row[..split]);}
                        if(ImGui.Button("Show live run"))command("run ");
                        ImGui.EndTabItem();
                    }
                    if(ImGui.BeginTabItem("Gem Maker"))
                    {
                        ImGui.TextWrapped("Start pauses RynthAi automatically. Stay in the local station area in peace mode. The client may approach the intended nearby vendor or press.");
                        var data=machine.Data;
                        ImGui.TextColored(new(.38f,.9f,.77f,1),data.State+" | "+data.Done+" / "+data.Needed);
                        ImGui.TextWrapped(data.Notice);
                        if(notice.Length>0)ImGui.TextWrapped(notice);
                        if(machine.Running)
                        {
                            ImGui.TextUnformatted("Dose "+data.Dose+" | fragment "+data.Fragment.ToString("X8"));
                            if(ImGui.Button("STOP BATCH"))stop();
                        }
                        else
                        {
                            if(ImGui.BeginCombo("Blank level",plan.Level.ToString())){foreach(int level in new[]{300,325,350,375})if(ImGui.Selectable(level.ToString(),plan.Level==level)){plan.Level=level;Dirty=true;}ImGui.EndCombo();}
                            int target=plan.Target;if(ImGui.InputInt("Gems",ref target)){plan.Target=Math.Clamp(target,1,50);Dirty=true;}
                            bool topup=plan.TopUp;if(ImGui.Checkbox("Top up usable stock",ref topup)){plan.TopUp=topup;Dirty=true;}
                            bool buy=plan.BuyMissing;if(ImGui.Checkbox("Buy missing supplies",ref buy)){plan.BuyMissing=buy;Dirty=true;}
                            int cap=plan.Budget;if(ImGui.InputInt("Purchase cap (per quoted currency)",ref cap)){plan.Budget=Math.Clamp(cap,0,10000000);Dirty=true;}
                            string value=plan.Scarab;if(ImGui.InputText("Scarab",ref value,100)){plan.Scarab=value;Dirty=true;}
                            value=plan.Herb;if(ImGui.InputText("Herb",ref value,100)){plan.Herb=value;Dirty=true;}
                            value=plan.Powder;if(ImGui.InputText("Powder",ref value,100)){plan.Powder=value;Dirty=true;}
                            value=plan.Reagent;if(ImGui.InputText("Reagent (optional)",ref value,100)){plan.Reagent=value;Dirty=true;}
                            value=plan.Talisman;if(ImGui.InputText("Talisman (optional)",ref value,100)){plan.Talisman=value;Dirty=true;}
                            value=plan.Taper;if(ImGui.InputText("Taper",ref value,100)){plan.Taper=value;Dirty=true;}
                            int count=plan.TaperCount;if(ImGui.SliderInt("Taper doses",ref count,0,4)){plan.TaperCount=count;Dirty=true;}
                            ImGui.TextWrapped("Recipe: "+string.Join(", ",plan.Doses()));
                            ImGui.TextDisabled("Keeps every result. No automatic gem destruction.");
                            if(ImGui.Button("START BATCH"))start(plan);
                        }
                        ImGui.EndTabItem();
                    }
                    if(ImGui.BeginTabItem("Research"))
                    {
                        if(ImGui.BeginCombo("Goal",goal))
                        {
                            foreach(string g in new[]{"lum","xp","loot","easy","hard","salvage Amethyst","salvage Mahogany"})if(ImGui.Selectable(g,g==goal)){goal=g;command("goal "+g);}
                            ImGui.EndCombo();
                        }
                        foreach(string line in view.Goals)ImGui.TextWrapped(line);
                        ImGui.Separator();
                        foreach(string line in view.Research)ImGui.TextWrapped(line);
                        ImGui.TextDisabled("Observed associations; no guaranteed recipe or farming rate.");
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
                ImGui.Separator();ImGui.TextWrapped(view.Notice);if(notice.Length>0)ImGui.TextWrapped(notice);
            }
            finally{ImGui.End();}
        }
        finally{ImGui.PopStyleColor(2);ImGui.PopStyleVar();}
    }
}
