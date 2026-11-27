using System.Numerics;
using ACAI2.Rynth.Scrolls.Core;
using ImGuiNET;
namespace ACAI2.Rynth.Scrolls.Plugin;

internal sealed class ScrollPanel
{
    private bool position=true,reviewCheck;private int frame=-1;private Vector2? lastPosition;
    private const string Schools="All\0Creature\0Item\0Life\0War\0Void\0";
    public void Reload(){position=true;reviewCheck=false;lastPosition=null;}
    public void Render(ScrollView v,string[] unresolved,Action<UiRequest> send)
    {
        if(!v.Visible||ImGui.GetCurrentContext()==IntPtr.Zero||frame==ImGui.GetFrameCount())return;frame=ImGui.GetFrameCount();
        void Request(string command,string value="",string[]? keys=null,float x=0,float y=0)=>send(new(v.Epoch,command,value,keys,x,y));
        var accent=new Vector4(.38f,.88f,.9f,1);var purple=new Vector4(.7f,.5f,1,1);
        ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(.045f,.055f,.09f,.97f));
        ImGui.PushStyleColor(ImGuiCol.Header,new Vector4(.2f,.14f,.33f,.85f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark,accent);ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(12,10));
        try
        {
            ImGui.SetNextWindowSize(new Vector2(680,540),ImGuiCond.FirstUseEver);
            if(position){ImGui.SetNextWindowPos(new(v.X,v.Y),ImGuiCond.Always);position=false;}
            bool visible=true;bool open=ImGui.Begin("SCROLLWORK / "+v.Character+"###acai2-scrolls",ref visible);
            try
            {
                if(!visible)Request("hide");
                if(!open)return;
                var pos=ImGui.GetWindowPos();if(lastPosition!=pos){lastPosition=pos;if(Math.Abs(pos.X-v.X)>1||Math.Abs(pos.Y-v.Y)>1)Request("move",x:pos.X,y:pos.Y);}
                ImGui.TextColored(accent,"LEVEL VIII  /  SCROLL MAKER");ImGui.SameLine();ImGui.TextDisabled("v"+ScrollsPlugin.Version);
                ImGui.TextDisabled("Inventory supplies  •  Finished scrolls stay in your packs");
                int known=v.Rows.Count(r=>r.Known),missing=v.Rows.Length-known;
                ImGui.TextUnformatted($"{known} learned   /   {missing} missing   /   {v.Supplies.Sum(s=>s.Required)/4} selected");
                ImGui.Separator();
                if(ImGui.BeginTabBar("scroll-tabs"))
                {
                    if(ImGui.BeginTabItem("Spells"))
                    {
                        string[] names=["All","Creature","Item","Life","War","Void"];int school=Math.Max(0,Array.IndexOf(names,v.School));
                        ImGui.SetNextItemWidth(130);if(ImGui.Combo("School",ref school,Schools))Request("school",names[school]);ImGui.SameLine();
                        bool only=v.MissingOnly;if(ImGui.Checkbox("Missing only",ref only))Request("missing",only?"1":"0");
                        string search=v.Search;ImGui.SetNextItemWidth(260);if(ImGui.InputTextWithHint("##search","Search spells or components",ref search,96))Request("search",search);
                        var filtered=v.Rows.Where(r=>(v.School=="All"||r.Recipe.School==v.School)&&(!v.MissingOnly||!r.Known)&&(v.Search.Length==0||r.Recipe.Name.Contains(v.Search,StringComparison.OrdinalIgnoreCase)||r.Recipe.Ingredients.Any(i=>i.Name.Contains(v.Search,StringComparison.OrdinalIgnoreCase)))).ToArray();
                        ImGui.BeginDisabled(v.Running);
                        if(ImGui.SmallButton("Select missing in this view"))Request("selection",keys:v.Selected.Union(filtered.Where(r=>!r.Known&&r.InPack==0).Select(r=>r.Recipe.Key)).ToArray());
                        ImGui.SameLine();if(ImGui.SmallButton("Clear selection"))Request("selection",keys:[]);ImGui.EndDisabled();
                        if(ImGui.BeginChild("spell-list",new Vector2(0,Math.Max(140,ImGui.GetContentRegionAvail().Y-100)),ImGuiChildFlags.Borders))
                        {
                            if(ImGui.BeginTable("spell-table",3,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp))
                            {
                                ImGui.TableSetupColumn("Make",ImGuiTableColumnFlags.WidthFixed,36);ImGui.TableSetupColumn("Scroll");ImGui.TableSetupColumn("Status",ImGuiTableColumnFlags.WidthFixed,90);ImGui.TableHeadersRow();
                                foreach(var row in filtered)
                                {
                                    ImGui.PushID(row.Recipe.Key);ImGui.TableNextRow();ImGui.TableSetColumnIndex(0);
                                    bool chosen=v.Selected.Contains(row.Recipe.Key);ImGui.BeginDisabled(v.Running||row.Known||row.InPack>0);
                                    if(ImGui.Checkbox("##make",ref chosen))Request("selection",keys:chosen?v.Selected.Append(row.Recipe.Key).Distinct().ToArray():v.Selected.Where(k=>k!=row.Recipe.Key).ToArray());ImGui.EndDisabled();
                                    ImGui.TableSetColumnIndex(1);ImGui.TextUnformatted(row.Recipe.Name.Replace("Inscription of ",""));
                                    if(ImGui.IsItemHovered())ImGui.SetTooltip(row.Recipe.School+" • Spell "+row.Recipe.SpellId+"\n"+string.Join("\n",row.Recipe.Ingredients.Select(i=>i.Name)));
                                    ImGui.TableSetColumnIndex(2);ImGui.TextColored(row.Known?accent:row.InPack>0?purple:new Vector4(1,.73f,.35f,1),row.Known?"Learned":row.InPack>0?"In pack x"+row.InPack:"Missing");ImGui.PopID();
                                }
                                ImGui.EndTable();
                            }
                        }
                        ImGui.EndChild();ImGui.EndTabItem();
                    }
                    if(ImGui.BeginTabItem("Components"))
                    {
                        ImGui.TextWrapped("Combined supplies for selected missing spells. Existing finished scrolls are excluded.");
                        if(ImGui.BeginChild("supply-list",new Vector2(0,Math.Max(140,ImGui.GetContentRegionAvail().Y-100)),ImGuiChildFlags.Borders))
                        {
                            if(ImGui.BeginTable("supplies",4,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp))
                            {
                                foreach(string heading in new[]{"Component","Need","Owned","Short"})ImGui.TableSetupColumn(heading);ImGui.TableHeadersRow();
                                foreach(var s in v.Supplies)
                                {ImGui.TableNextRow();ImGui.TableSetColumnIndex(0);ImGui.TextUnformatted(s.Name);ImGui.TableSetColumnIndex(1);ImGui.TextUnformatted(s.Required.ToString());ImGui.TableSetColumnIndex(2);ImGui.TextUnformatted(s.Owned.ToString());ImGui.TableSetColumnIndex(3);ImGui.TextColored(s.Missing>0?new Vector4(1,.6f,.38f,1):accent,s.Missing.ToString());}
                                ImGui.EndTable();
                            }
                            if(v.Supplies.Length==0)ImGui.TextDisabled("Choose scrolls on the Spells tab first.");
                            if(ImGui.Button("Print component list to chat"))Request("print");
                        }
                        ImGui.EndChild();ImGui.EndTabItem();
                    }
                    if(ImGui.BeginTabItem("Activity & recipes"))
                    {
                        if(ImGui.BeginChild("activity",new Vector2(0,Math.Max(140,ImGui.GetContentRegionAvail().Y-100)),ImGuiChildFlags.Borders))
                        {
                            ImGui.TextWrapped("197 ACE-checked recipes. Crafting pauses RynthAi and requires peace mode. Remain still; normal completion restores only the macro this batch paused.");
                            ImGui.TextWrapped("Server-specific recipe changes require in-game validation. No auto-learning, buying or blind retries.");
                            if(ImGui.CollapsingHeader("Recipes awaiting verification"))foreach(string s in unresolved)ImGui.TextWrapped(s);
                            foreach(string item in v.History)ImGui.TextWrapped(item);
                        }
                        ImGui.EndChild();ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
                ImGui.Separator();
                ImGui.TextWrapped(v.Status);
                if(v.Running)ImGui.ProgressBar(v.Total>0?(float)v.Completed/v.Total:0,new Vector2(-1,12),$"{v.Completed} / {v.Total}");
                ImGui.BeginDisabled(v.Running);if(ImGui.Button("Rescan"))Request("scan");ImGui.SameLine();
                ImGui.BeginDisabled(v.Review||v.Supplies.Length==0||v.Supplies.Any(s=>s.Missing>0));if(ImGui.Button("Make selected scrolls"))Request("craft");ImGui.EndDisabled();ImGui.EndDisabled();
                if(v.Running){ImGui.SameLine();if(ImGui.Button("Stop"))Request("stop");}
                if(v.Review)
                {
                    ImGui.Checkbox("I checked my inventory and the interrupted craft",ref reviewCheck);
                    ImGui.BeginDisabled(!reviewCheck);if(ImGui.Button("Clear reviewed checkpoint")){Request("review");reviewCheck=false;}ImGui.EndDisabled();
                }
            }
            finally{ImGui.End();}
        }
        finally{ImGui.PopStyleVar();ImGui.PopStyleColor(3);}
    }
}
