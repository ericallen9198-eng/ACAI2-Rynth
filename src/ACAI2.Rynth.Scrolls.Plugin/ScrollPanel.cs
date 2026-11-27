using System.Numerics;
using ACAI2.Rynth.Scrolls.Core;
using ImGuiNET;
namespace ACAI2.Rynth.Scrolls.Plugin;

internal sealed class ScrollPanel
{
    private bool position=true,reviewCheck,tabsPlaced;private string activeTab="";private int frame=-1;private IntPtr lastContext;private Vector2? lastPosition;
    private const string Schools="All\0Creature\0Item\0Life\0War\0Void\0";
    // ImGui.NET's flagged string overload requires a close-button pointer.
    // A null pointer keeps these fixed navigation tabs non-closeable.
    private static unsafe bool BeginTab(string name,ImGuiTabItemFlags flags)
    {
        Span<byte> label=stackalloc byte[128];int n=System.Text.Encoding.UTF8.GetBytes(name,label);label[n]=0;
        fixed(byte* p=label)return ImGuiNative.igBeginTabItem(p,null,flags)!=0;
    }
    public void Reload(){position=true;reviewCheck=false;lastPosition=null;tabsPlaced=false;activeTab="";}
    public void Render(ScrollView v,string[] unresolved,Action<UiRequest> send,bool hosted=false)
    {
        if((!hosted&&!v.Visible)||ImGui.GetCurrentContext()==IntPtr.Zero||(lastContext==ImGui.GetCurrentContext()&&frame==ImGui.GetFrameCount()))return;frame=ImGui.GetFrameCount();lastContext=ImGui.GetCurrentContext();
        void Request(string command,string value="",string[]? keys=null,float x=0,float y=0)=>send(new(v.Epoch,command,value,keys,x,y));
        var accent=new Vector4(.38f,.88f,.9f,1);var purple=new Vector4(.7f,.5f,1,1);
        ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(.045f,.055f,.09f,.97f));
        ImGui.PushStyleColor(ImGuiCol.Header,new Vector4(.2f,.14f,.33f,.85f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark,accent);ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(12,10));
        try
        {
            if(!hosted)ImGui.SetNextWindowSize(new Vector2(680,540),ImGuiCond.FirstUseEver);
            if(!hosted&&position){ImGui.SetNextWindowPos(new(v.X,v.Y),ImGuiCond.Always);position=false;}
            bool visible=true;bool open=hosted||ImGui.Begin("SCROLLWORK / "+v.Character+"###acai2-scrolls",ref visible);
            try
            {
                if(!hosted&&!visible)Request("hide");
                if(!open)return;
                var pos=ImGui.GetWindowPos();if(!hosted&&lastPosition!=pos){lastPosition=pos;if(Math.Abs(pos.X-v.X)>1||Math.Abs(pos.Y-v.Y)>1)Request("move",x:pos.X,y:pos.Y);}
                ImGui.TextColored(accent,"LEVEL VIII  /  SCROLL MAKER");ImGui.SameLine();ImGui.TextDisabled("v"+ScrollsPlugin.Version+" / "+v.Character);
                ImGui.TextDisabled("Inventory supplies  •  Finished scrolls stay in your packs");
                int known=v.Rows.Count(r=>r.Known),missing=v.Rows.Length-known;
                ImGui.TextUnformatted(v.Rows.Length==0?"Waiting for learned-spell snapshot…":$"{known} learned   /   {missing} missing   /   {v.Supplies.Sum(s=>s.Required)/4} selected total");
                ImGui.Separator();
                float footerSpace=v.Review?160:v.Running?125:105;
                if(ImGui.BeginTabBar("scroll-tabs"))
                {
                    bool Tab(string name)
                    {
                        bool opened=BeginTab(name,!tabsPlaced&&v.Tab==name?ImGuiTabItemFlags.SetSelected:ImGuiTabItemFlags.None);
                        if(opened&&activeTab!=name){activeTab=name;Request("tab",name);}return opened;
                    }
                    foreach(var list in new[]{SpellList.Self,SpellList.Others,SpellList.Equipment})
                    {
                        if(!Tab(list.ToString()))continue;
                        string[] names=["All","Creature","Item","Life","War","Void"];int school=Math.Max(0,Array.IndexOf(names,list==SpellList.Equipment?"Item":v.School));
                        ImGui.BeginDisabled(list==SpellList.Equipment);ImGui.SetNextItemWidth(130);if(ImGui.Combo("School",ref school,Schools))Request("school",names[school]);ImGui.EndDisabled();ImGui.SameLine();
                        bool only=v.MissingOnly;if(ImGui.Checkbox("Missing only",ref only))Request("missing",only?"1":"0");
                        if(list==SpellList.Self)
                        {
                            ImGui.SameLine();bool essentials=v.EssentialsOnly;if(ImGui.Checkbox("Essentials only",ref essentials))Request("essentials",essentials?"1":"0");
                            int count=v.Rows.Count(r=>r.Essential);
                            ImGui.TextColored(purple,$"{count} essential unlearned self skill buff(s)");
                            if(v.SkillsAvailable<SpellRecommendations.Skills.Length)ImGui.TextDisabled($"Skill snapshot: {v.SkillsAvailable}/{SpellRecommendations.Skills.Length} available; unread skills stay unknown.");
                            if(ImGui.IsItemHovered())ImGui.SetTooltip("Essentials match a trained/specialized skill directly. This is relevance, not a casting-skill check.");
                        }
                        else ImGui.TextDisabled(list==SpellList.Others?"Spells targeting others: attacks, debuffs and other-target buffs/heals.":"Spells targeting equipment: banes, lures, armor and item dispels.");
                        string search=v.Search;ImGui.SetNextItemWidth(260);if(ImGui.InputTextWithHint("##search","Search spells, skills or components",ref search,96))Request("search",search);
                        var filtered=SpellRecommendations.Filter(v.Rows,list,names[school],v.Search,v.MissingOnly,list==SpellList.Self&&v.EssentialsOnly);
                        ImGui.BeginDisabled(v.Running);
                        if(ImGui.SmallButton("Select missing in this view"))Request("add",keys:SpellRecommendations.SelectMissing(filtered,[]));
                        if(list==SpellList.Self)
                        {ImGui.SameLine();if(ImGui.SmallButton("Select missing essentials"))Request("add",keys:SpellRecommendations.SelectMissing(filtered.Where(r=>r.Essential),[]));}
                        ImGui.SameLine();if(ImGui.SmallButton("Clear all selections"))Request("selection",keys:[]);ImGui.EndDisabled();
                        if(ImGui.BeginChild("spell-list-"+list,new Vector2(0,Math.Max(100,ImGui.GetContentRegionAvail().Y-footerSpace)),ImGuiChildFlags.Borders))
                        {
                            if(filtered.Length==0)ImGui.TextDisabled(v.Rows.Length==0?"Waiting for spellbook scan…":"No spells match these filters.");
                            if(ImGui.BeginTable("spell-table-"+list,3,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp))
                            {
                                ImGui.TableSetupColumn("Make",ImGuiTableColumnFlags.WidthFixed,36);ImGui.TableSetupColumn("Scroll");ImGui.TableSetupColumn("Status",ImGuiTableColumnFlags.WidthFixed,90);ImGui.TableHeadersRow();
                                foreach(var row in filtered)
                                {
                                    ImGui.PushID(row.Recipe.Key);ImGui.TableNextRow();ImGui.TableSetColumnIndex(0);
                                    bool chosen=v.Selected.Contains(row.Recipe.Key);ImGui.BeginDisabled(v.Running||row.Known||row.InPack>0);
                                    if(ImGui.Checkbox("##make",ref chosen))Request(chosen?"choose":"unchoose",row.Recipe.Key);ImGui.EndDisabled();
                                    ImGui.TableSetColumnIndex(1);
                                    if(row.Essential)ImGui.TextColored(purple,"ESSENTIAL  "+row.Recipe.Name.Replace("Inscription of ",""));
                                    else ImGui.TextUnformatted(row.Recipe.Name.Replace("Inscription of ",""));
                                    if(ImGui.IsItemHovered())ImGui.SetTooltip(row.Recipe.School+" • Spell "+row.Recipe.SpellId+(row.Skill==null?"":"\n"+row.Skill.Skill.Name+": "+row.Skill.TrainingName+" — relevance only, not castability")+"\n"+string.Join("\n",row.Recipe.Ingredients.Select(i=>i.Name)));
                                    ImGui.TableSetColumnIndex(2);ImGui.TextColored(row.Known?accent:row.InPack>0?purple:new Vector4(1,.73f,.35f,1),row.Known?"Learned":row.InPack>0?"In pack x"+row.InPack:"Missing");ImGui.PopID();
                                }
                                ImGui.EndTable();
                            }
                            if(filtered.Length==0)ImGui.TextDisabled(v.Rows.Length==0?"Waiting for spellbook scan.":"No spells match this list and its filters.");
                        }
                        ImGui.EndChild();ImGui.EndTabItem();
                    }
                    if(Tab("Components"))
                    {
                        ImGui.TextWrapped("Combined supplies for selected missing spells across all lists. Existing finished scrolls are excluded.");
                        if(ImGui.BeginChild("supply-list",new Vector2(0,Math.Max(140,ImGui.GetContentRegionAvail().Y-footerSpace)),ImGuiChildFlags.Borders))
                        {
                            if(ImGui.BeginTable("supplies",4,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp))
                            {
                                foreach(string heading in new[]{"Component","Need","Owned","Short"})ImGui.TableSetupColumn(heading);ImGui.TableHeadersRow();
                                foreach(var s in v.Supplies)
                                {ImGui.TableNextRow();ImGui.TableSetColumnIndex(0);ImGui.TextUnformatted(s.Name);ImGui.TableSetColumnIndex(1);ImGui.TextUnformatted(s.Required.ToString());ImGui.TableSetColumnIndex(2);ImGui.TextUnformatted(s.Owned.ToString());ImGui.TableSetColumnIndex(3);ImGui.TextColored(s.Missing>0?new Vector4(1,.6f,.38f,1):accent,s.Missing.ToString());}
                                ImGui.EndTable();
                            }
                            if(v.Supplies.Length==0)ImGui.TextDisabled("Choose scrolls on Self, Others or Equipment first.");
                            if(ImGui.Button("Print component list to chat"))Request("print");
                        }
                        ImGui.EndChild();ImGui.EndTabItem();
                    }
                    if(Tab("Activity & recipes"))
                    {
                        if(ImGui.BeginChild("activity",new Vector2(0,Math.Max(140,ImGui.GetContentRegionAvail().Y-footerSpace)),ImGuiChildFlags.Borders))
                        {
                            ImGui.TextWrapped("197 ACE-checked recipes. Crafting pauses RynthAi and requires peace mode. Remain still; normal completion restores only the macro this batch paused.");
                            ImGui.TextWrapped("Server-specific recipe changes require in-game validation. No auto-learning, buying or blind retries.");
                            if(ImGui.CollapsingHeader("Recipes awaiting verification"))foreach(string s in unresolved)ImGui.TextWrapped(s);
                            foreach(string item in v.History)ImGui.TextWrapped(item);
                        }
                        ImGui.EndChild();ImGui.EndTabItem();
                    }
                    tabsPlaced=true;ImGui.EndTabBar();
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
            finally{if(!hosted)ImGui.End();}
        }
        finally{ImGui.PopStyleVar();ImGui.PopStyleColor(3);}
    }
}
