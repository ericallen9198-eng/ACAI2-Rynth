using System.Numerics;
using ACAI2.Rynth.Core;
using ImGuiNET;
using RynthCore.PluginSdk;

namespace ACAI2.Rynth.Plugin;

// Called only inside the engine's active ImGui frame. The engine resolves cimgui
// to its own module for managed plugins; do not create an independent context.
internal sealed class CombatPanel
{
    public CombatAppearance Appearance = new();
    public bool Setup;
    public bool Preview;
    public bool Dirty;
    public bool ResetStatistics;
    public string Notice = "";
    private readonly CombatDisplay preview = new();
    private double nextPreview;
    private bool positionPending=true;
    public void Reset(CombatAppearance appearance) {Appearance=appearance;positionPending=true;Setup=false;Preview=false;Dirty=false;ResetStatistics=false;Notice="";}
    public void Draw(RynthCoreHost host,string character,CombatReading live,double now)
    {
        if(ImGui.GetCurrentContext()==IntPtr.Zero) return;
        var a=Appearance;
        Vector2 viewport=ImGui.GetIO().DisplaySize;
        if(viewport.X<=0||viewport.Y<=0)return;
        if(a.Visible)
        {
            if(positionPending || a.X>viewport.X-60 || a.Y>viewport.Y-40)
            {ImGui.SetNextWindowPos(new(Math.Clamp(a.X,0,Math.Max(0,viewport.X-280*a.Scale)),Math.Clamp(a.Y,0,Math.Max(0,viewport.Y-150*a.Scale))),ImGuiCond.Always);positionPending=false;}
            ImGui.SetNextWindowBgAlpha(a.Opacity);
            ImGui.SetNextWindowSize(new(280*a.Scale,0),ImGuiCond.Always);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,7);
            ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(.035f,.055f,.065f,1));
            var flags=ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoResize|ImGuiWindowFlags.NoCollapse|ImGuiWindowFlags.AlwaysAutoResize;
            if(a.Locked)flags|=ImGuiWindowFlags.NoMove;
            bool expanded=ImGui.Begin("ACAI2 Combat###acai2-meter",flags);
            try
            {
                if(expanded)
                {
                    ImGui.SetWindowFontScale(a.Scale);
                    ImGui.TextUnformatted("COMBAT / "+character);ImGui.SameLine();
                    if(ImGui.SmallButton("Setup"))Setup=!Setup;
                    ImGui.Separator();
                    ImGui.TextColored(new(.38f,.9f,.77f,1),$"{live.Dps:N0} DPS");
                    ImGui.SameLine(); ImGui.TextUnformatted($"   {live.Taken:N0} taken/s");
                    ImGui.TextDisabled("60s rolling | Live | Not recording");
                    if(ImGui.TreeNode("Breakdown"))
                    {
                        foreach(var p in live.Components.OrderByDescending(x=>x.Value))ImGui.TextUnformatted($"{p.Key}  {p.Value:N0}  {(live.Damage>0?p.Value*100.0/live.Damage:0):N1}%");
                        ImGui.TextUnformatted($"Healing {live.Healing:N0}");
                        if(live.Sneaks>0)ImGui.TextUnformatted($"Sneak {live.Sneaks} | {live.Sneaks*100.0/Math.Max(1,live.DirectHits):N1}% | avg {live.SneakDamage/(double)live.Sneaks:N0}");
                        ImGui.TreePop();
                    }
                    Vector2 pos=ImGui.GetWindowPos();
                    if(pos.X!=a.X||pos.Y!=a.Y){a.X=pos.X;a.Y=pos.Y;Dirty=true;}
                }
            }
            finally {ImGui.End();ImGui.PopStyleColor();ImGui.PopStyleVar();}
        }
        if(Setup)DrawSetup(host);
        if(Preview && now>=nextPreview)
        {
            nextPreview=now+.65;
            preview.Observe("Your poison deals 1,221 additional points of damage to Dread Plaguefang!",character,now);
            preview.Observe("Sneak Attack! You graze Dread Plaguefang for 69 points of bludgeoning damage!",character,now);
        }
        if(!a.Floating && !Preview)return;
        var reading=Preview?preview.Read(now,a.Lifetime):live;
        Vector2 anchor=new(viewport.X*a.AnchorX,viewport.Y*a.AnchorY);
        // Fixed-screen mode only until camera frame-cell offsets can be obtained
        // through the SDK. Raw object coordinates are not projection coordinates.
        var draw=ImGui.GetForegroundDrawList();
        foreach(var line in reading.Text)
        {
            bool left=line.Kind is "Healing" or "Poison";
            var arc=CombatDisplay.Arc(now-line.Start,a.Lifetime,left,a.LaneGap,a.Curvature,a.Rise);
            string label=$"{line.Amount:N0} {line.Kind.ToUpperInvariant()}"+(line.Count>1?$" · {line.Count} ticks":"");
            float size=a.TextSize*(line.Kind=="Critical"?1.1f:1);
            Vector2 pos=anchor+new Vector2(arc.X,arc.Y-line.Slot*24);
            // Center each lane's text around its own anchor.
            float width=ImGui.CalcTextSize(label).X*size/ImGui.GetFontSize();pos.X-=width*.5f;
            if(pos.X<0||pos.X+width>viewport.X||pos.Y<0||pos.Y>viewport.Y)continue;
            uint tint=a.Colors.GetValueOrDefault(line.Kind,a.Colors["Direct"]);
            uint alpha=(uint)(((tint>>24)&255)*arc.Alpha);
            uint color=(tint&0xffffff)|(alpha<<24),black=(uint)(255*arc.Alpha)<<24;
            if(a.Outline>0)
                for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)if(dx!=0||dy!=0)
                    draw.AddText(ImGui.GetFont(),size,pos+new Vector2(dx,dy)*a.Outline,black,label);
            draw.AddText(ImGui.GetFont(),size,pos,color,label);
        }
    }
    private void Toggle(string label,bool current,Action<bool> set)
    {if(ImGui.Checkbox(label,ref current)){set(current);Dirty=true;}}
    private void Slider(string label,float current,float min,float max,Action<float> set)
    {if(ImGui.SliderFloat(label,ref current,min,max)){set(current);Dirty=true;}}
    private void DrawSetup(RynthCoreHost host)
    {
        ImGui.SetNextWindowSize(new(430,540),ImGuiCond.FirstUseEver);
        bool open=Setup;
        bool body=ImGui.Begin("ACAI2 Appearance",ref open);
        try
        {
            Setup=open;if(!body)return;
            var a=Appearance;
            Toggle("Show meter",a.Visible,v=>a.Visible=v);
            Toggle("Lock layout",a.Locked,v=>a.Locked=v);
            Toggle("ACAI2 proc text",a.Floating,v=>a.Floating=v);
            ImGui.TextWrapped("Optional: Rynth Vision already provides general combat text. Keep one source for overlapping crit/heal numbers.");
            ImGui.TextDisabled("Character anchoring: pending coordinate validation");
            ImGui.Checkbox("Preview sample combat text",ref Preview);
            if(Preview)ImGui.TextColored(new(1,.8f,.3f,1),"SAMPLE TEXT · separate from live totals");
            Slider("Panel opacity",a.Opacity,0,1,v=>a.Opacity=v);
            Slider("Meter scale",a.Scale,.7f,2,v=>a.Scale=v);
            if(ImGui.Button("New live session"))ResetStatistics=true;
            ImGui.Separator();
            ImGui.TextUnformatted("Power and attack height: managed by Rynth");
            if(ImGui.Button("Open Rynth Settings"))
            {
                int result=host.SetPanelOpen("Settings",RynthPanelOpen.Open);
                Notice=result<0?"Settings panel unavailable; open it from Rynth's menu.":"Combat: Melee/Missile Power and Attack Height.";
            }
            ImGui.TextWrapped("Use Rynth's saved melee/missile power and height settings. ACAI2 does not create another attack controller.");
            ImGui.TextDisabled("Thread height learning: not connected yet.");
            if(ImGui.CollapsingHeader("Advanced appearance"))
            {
                Slider("Font size",a.TextSize,14,40,v=>a.TextSize=v);
                Slider("Black outline",a.Outline,0,2,v=>a.Outline=v);
                Slider("Inward curvature",a.Curvature,0,100,v=>a.Curvature=v);
                Slider("Lane gap",a.LaneGap,50,250,v=>a.LaneGap=v);
                Slider("Rise distance",a.Rise,30,250,v=>a.Rise=v);
                Slider("Lifetime",a.Lifetime,1,5,v=>a.Lifetime=v);
                Slider("Fixed anchor X",a.AnchorX,.05f,.95f,v=>a.AnchorX=v);
                Slider("Fixed anchor Y",a.AnchorY,.1f,.9f,v=>a.AnchorY=v);
                foreach(string key in a.Colors.Keys.ToArray())
                {
                    Vector4 color=ImGui.ColorConvertU32ToFloat4(a.Colors[key]);
                    if(ImGui.ColorEdit4(key,ref color,ImGuiColorEditFlags.AlphaBar|ImGuiColorEditFlags.DisplayHex|ImGuiColorEditFlags.PickerHueWheel))
                    {a.Colors[key]=ImGui.ColorConvertFloat4ToU32(color);Dirty=true;}
                }
                ImGui.TextWrapped("Uses the engine font atlas. Custom font loading and weight selection are not enabled yet.");
            }
            if(ImGui.Button("Reset appearance")){Reset(new());Setup=true;Dirty=true;}
            if(Notice.Length>0)ImGui.TextWrapped(Notice);
        }
        finally {ImGui.End();}
    }
}
