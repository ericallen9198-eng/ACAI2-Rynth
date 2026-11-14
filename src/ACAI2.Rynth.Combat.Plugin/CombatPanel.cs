using System.Numerics;
using ACAI2.Rynth.Combat.Core;
using ImGuiNET;
namespace ACAI2.Rynth.Combat.Plugin;

internal sealed class CombatPanel
{
    public bool Setup,Dirty;
    private bool place=true,details;
    private int frame=-1;
    private string selected="";
    public void Reset(){place=true;Setup=false;Dirty=false;selected="";}
    private static Vector4 Accent=>new(.38f,.9f,.77f,1);
    public void Draw(string character,Appearance a,CombatView v,double now,Action<string> command)
    {
        if(ImGui.GetCurrentContext()==IntPtr.Zero)return;int f=ImGui.GetFrameCount();if(f==frame)return;frame=f;
        Vector2 screen=ImGui.GetIO().DisplaySize;if(screen.X<=0||screen.Y<=0)return;
        if(a.Visible)
        {
            if(place){ImGui.SetNextWindowPos(new(Math.Clamp(a.X,0,Math.Max(0,screen.X-340)),Math.Clamp(a.Y,0,Math.Max(0,screen.Y-200))),ImGuiCond.Always);place=false;}
            ImGui.SetNextWindowSize(new(330*a.Scale,0),ImGuiCond.Always);ImGui.SetNextWindowBgAlpha(a.Opacity);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,9);ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(.025f,.045f,.055f,1));
            var flags=ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoResize|ImGuiWindowFlags.NoCollapse;if(a.Locked)flags|=ImGuiWindowFlags.NoMove;
            ImGui.Begin("ACAI2 COMBAT###acai2-combat-meter",flags);
            try
            {
                ImGui.SetWindowFontScale(a.Scale);
                ImGui.TextUnformatted("COMBAT / "+character);ImGui.SameLine();if(ImGui.SmallButton("Setup"))Setup=!Setup;
                ImGui.Separator();ImGui.TextColored(Accent,$"{v.Dps:N0} DPS");ImGui.SameLine();ImGui.TextUnformatted($"   {v.Taken:N0} taken/s");
                ImGui.TextDisabled(v.Scope+(v.Preview?" | PREVIEW":" | 60s rolling"));
                if(v.RunScope&&v.Current?.ParserVersion!="0.1.1")ImGui.TextDisabled("Includes earlier parser totals; New session starts a clean sample.");
                var t=v.Stats;ImGui.TextUnformatted($"Damage {t.Damage:N0}   Heals {t.Healing:N0}   Kills {t.Kills}");
                if(Show(a,t,"Sneak")){ImGui.TextColored(new(.94f,.66f,1,1),$"Sneak {t.SneakPercent:N1}%  ·  {t.Sneaks}/{t.DirectHits} landed physical hits");Tip("Denominator: your landed direct melee/missile hits, including zero damage. Excludes magic, poison, acid, pets and reflected damage. Discovery occurs on the first observed sneak hit.");}
                if(t.PhysicalHits+t.Evades>0)ImGui.TextUnformatted($"Physical evade {t.EvadePercent:N1}%   Avg hit {t.PhysicalDamage/(double)Math.Max(1,t.PhysicalHits):N0}   High {t.PhysicalHigh:N0}");
                Tip("Physical includes melee and missile: chat does not reliably separate them. Zero-damage landed hits count; shield blocks are listed separately.");
                if(t.MagicHits+t.Resists>0)ImGui.TextUnformatted($"Spell resist {t.ResistPercent:N1}%   Avg spell {t.MagicDamage/(double)Math.Max(1,t.MagicHits):N0}   High {t.MagicHigh:N0}");
                foreach(string kind in new[]{"Acid","Poison","Summons","Thorns"})if(Show(a,t,kind))ImGui.TextColored(Color(a,kind),$"{kind} {t.Components.GetValueOrDefault(kind):N0}  ({100d*t.Components.GetValueOrDefault(kind)/Math.Max(1,t.Damage):N1}%)");
                if(Show(a,t,"Ward"))ImGui.TextUnformatted($"Runic Ward  absorbed {t.WardAbsorbed:N0}  ·  last ready capacity {t.WardCapacity}");
                if(t.Blocks>0)ImGui.TextUnformatted($"Shield blocks {t.Blocks}");
                if(t.Procs.ContainsKey("Kinetic"))ImGui.TextUnformatted($"Kinetic charge events {t.Procs["Kinetic"]}");
                if(Show(a,t,"Second Wind"))ImGui.TextUnformatted($"Second Wind {t.Procs.GetValueOrDefault("Second Wind")} procs (heal amount not stated)");
                if(ImGui.SmallButton(details?"Less":"Details"))details=!details;ImGui.SameLine();if(ImGui.SmallButton("Export"))command("export");ImGui.SameLine();if(ImGui.SmallButton("New session"))command("new");
                if(details)Details(v,a,command);
                if(v.Dropped>0)ImGui.TextColored(new(1,.6f,.3f,1),$"{v.Dropped} messages dropped · incomplete observation");
                var pos=ImGui.GetWindowPos();if(pos.X!=a.X||pos.Y!=a.Y){a.X=pos.X;a.Y=pos.Y;Dirty=true;}
            }
            finally{ImGui.End();ImGui.PopStyleColor();ImGui.PopStyleVar();}
        }
        if(Setup)Settings(a,v,command);
        if(a.Floating||v.Preview)Floating(a,v.Floats,screen,now);
    }
    private static bool Show(Appearance a,Totals t,string kind)=>a.Modules.TryGetValue(kind,out bool enabled)?enabled:kind=="Sneak"?t.Sneaks>0:t.Components.ContainsKey(kind)||t.Procs.ContainsKey(kind);
    private static void Tip(string text){if(ImGui.IsItemHovered()){ImGui.BeginTooltip();ImGui.PushTextWrapPos(420);ImGui.TextUnformatted(text);ImGui.PopTextWrapPos();ImGui.EndTooltip();}}
    private static Vector4 Color(Appearance a,string kind)=>ImGui.ColorConvertU32ToFloat4(a.Colors.GetValueOrDefault(kind,a.Colors["Direct"]));
    private void Details(CombatView v,Appearance a,Action<string> command)
    {
        var t=v.Stats;ImGui.Separator();
        if(ImGui.TreeNode("Damage sources"))
        {foreach(var p in t.Components.OrderByDescending(p=>p.Value))ImGui.TextUnformatted($"{p.Key}: {p.Value:N0}");if(t.UnclassifiedHits>0)ImGui.TextUnformatted($"Unclassified incoming: {t.UnclassifiedHits} hits / {t.UnclassifiedDamage:N0} damage (excluded from defense rates)");foreach(var p in t.IncomingEffects)ImGui.TextUnformatted($"Incoming {p.Key}: {p.Value} applied/refreshed");foreach(var p in t.HealingSources)ImGui.TextUnformatted($"{p.Key}: +{p.Value:N0}");foreach(var p in t.Procs)ImGui.TextUnformatted($"{p.Key}: {p.Value} notices");ImGui.TextUnformatted($"Blocks {t.Blocks} · Physical crits {t.Crits}/{t.DirectHits}");ImGui.TreePop();}
        if(ImGui.TreeNode("Mob breakdown")){MobTable(t);ImGui.TreePop();}
        if(ImGui.TreeNode("Deaths ("+t.DeathCount+")")){Deaths(t);ImGui.TreePop();}
        if(ImGui.TreeNode("Thread runs"))
        {
            bool automatic=a.AutoThread;if(ImGui.Checkbox("Use current Thread on meter",ref automatic)){a.AutoThread=automatic;Dirty=true;}
            if(v.Current!=null)
            {
                RunHeader(v.Current);
                if(ImGui.SmallButton("Mark entered"))command("entered");ImGui.SameLine();if(ImGui.SmallButton("Mark outside"))command("outside");
            }
            foreach(var run in v.History)if(ImGui.Selectable(run.Name+" · "+run.Opened.ToLocalTime().ToString("g")+"##"+run.Id,selected==run.Id))selected=selected==run.Id?"":run.Id;
            var chosen=v.History.FirstOrDefault(r=>r.Id==selected);
            if(chosen!=null){RunHeader(chosen);MobTable(chosen.Stats);Deaths(chosen.Stats);}
            ImGui.TreePop();
        }
        if(t.Truncated)ImGui.TextWrapped("A bounded mob/death limit was reached; totals continue but detail is incomplete.");
        ImGui.TextWrapped(v.Notice);
    }
    private static void RunHeader(RunRecord r)
    {
        if(r.ParserVersion!="0.1.1")ImGui.TextDisabled("Includes earlier parser totals; old aggregates are not reclassified.");
        ImGui.TextWrapped(r.Name+(r.Entered==null?" · awaiting entry":r.Cleared==null?" · in progress":" · CLEARED"));
        ImGui.TextUnformatted($"Observed inside: {TimeSpan.FromSeconds(r.Observed):hh\\:mm\\:ss}"+(r.Gaps?" · gaps":""));
        if(r.CompletionSeconds.HasValue)ImGui.TextUnformatted($"Entry to clear: {TimeSpan.FromSeconds(r.CompletionSeconds.Value):hh\\:mm\\:ss} (wall time)");
        ImGui.TextUnformatted($"Run average {r.Stats.Damage/Math.Max(1,r.Observed):N0} DPS (observed time)");
        ImGui.TextUnformatted($"Server progress: {r.KilledProgress?.ToString()??"?"}/{r.MonsterTotal?.ToString()??"?"} · attributed kills {r.Stats.Kills}");
    }
    private static void MobTable(Totals t)
    {
        ImGui.TextDisabled("Seen = sampled attackable object IDs; kills = attributed chat notices.");
        foreach(var p in t.Mobs.OrderByDescending(p=>p.Value.PhysicalDamage+p.Value.MagicDamage+p.Value.Dealt).Take(256))
        {
            var m=p.Value;if(ImGui.TreeNode(p.Key+$" · seen {m.Seen} · kills {m.Kills}"))
            {ImGui.TextUnformatted($"Dealt {m.Dealt:N0}");ImGui.TextUnformatted($"Physical: avg {m.PhysicalDamage/(double)Math.Max(1,m.PhysicalHits):N1} · high {m.PhysicalHigh} · evade {100d*m.Evades/Math.Max(1,m.Evades+m.PhysicalHits):N1}%");ImGui.TextUnformatted($"Magic: avg {m.MagicDamage/(double)Math.Max(1,m.MagicHits):N1} · high {m.MagicHigh} · resist {100d*m.Resists/Math.Max(1,m.Resists+m.MagicHits):N1}%");ImGui.TreePop();}
        }
    }
    private static void Deaths(Totals t)
    {
        if(t.Deaths.Count==0)ImGui.TextDisabled("No observed death notices.");
        foreach(var d in t.Deaths.AsEnumerable().Reverse())
        {ImGui.TextColored(new(1,.45f,.45f,1),d.Utc.ToLocalTime().ToString("T")+" · "+d.Killer);ImGui.TextWrapped(d.Message);if(d.Blow.HasValue)ImGui.TextUnformatted($"Blow {d.Blow} · HP {d.Health}/{d.Maximum} · overkill {d.Overkill}");}
    }
    private void Toggle(string label,bool value,Action<bool> set){if(ImGui.Checkbox(label,ref value)){set(value);Dirty=true;}}
    private void Slider(string label,float value,float min,float max,Action<float> set){if(ImGui.SliderFloat(label,ref value,min,max)){set(value);Dirty=true;}}
    private void Settings(Appearance a,CombatView v,Action<string> command)
    {
        ImGui.SetNextWindowSize(new(470,570),ImGuiCond.FirstUseEver);ImGui.Begin("ACAI2 Combat setup",ref Setup);
        try
        {
            ImGui.TextWrapped("Standalone display. Debuff icons remain owned by Rynth. Events discover their own meter modules.");
            Toggle("Lock meter position",a.Locked,x=>a.Locked=x);Toggle("Show meter",a.Visible,x=>a.Visible=x);Toggle("Floating combat text",a.Floating,x=>a.Floating=x);
            Toggle("Subdued effects",a.Subdued,x=>a.Subdued=x);Toggle("Bold effects and crits",a.Bold,x=>a.Bold=x);
            Slider("Meter transparency",a.Opacity,0,1,x=>a.Opacity=x);Slider("Meter scale",a.Scale,.7f,2,x=>a.Scale=x);
            if(ImGui.Button(v.Preview?"Stop preview":"Preview effects (30 seconds)"))command("preview");
            ImGui.TextDisabled("Preview is synthetic and never affects real totals or run records.");
            if(ImGui.CollapsingHeader("Floating text · advanced"))
            {
                ImGui.TextWrapped("Fixed screen anchor. Camera-based toon following needs a supported projection-frame API; no guessed world coordinates are used.");
                Slider("Anchor horizontal",a.AnchorX,.05f,.95f,x=>a.AnchorX=x);Slider("Anchor vertical",a.AnchorY,.1f,.95f,x=>a.AnchorY=x);
                Slider("Quiet hit size",a.RegularScale,.6f,1,x=>a.RegularScale=x);Slider("Quiet hit opacity",a.RegularAlpha,.25f,1,x=>a.RegularAlpha=x);Slider("Critical emphasis",a.CritScale,1.05f,1.8f,x=>a.CritScale=x);Slider("Healing height",a.HealOffset,0,300,x=>a.HealOffset=x);
                Slider("Font size",a.FontSize,12,42,x=>a.FontSize=x);Slider("Black outline",a.Outline,0,3,x=>a.Outline=x);
                Slider("Lane gap",a.Gap,40,300,x=>a.Gap=x);Slider("Arc inward",a.Curve,0,160,x=>a.Curve=x);Slider("Rise",a.Rise,30,300,x=>a.Rise=x);
                Slider("Duration",a.Lifetime,.7f,5,x=>a.Lifetime=x);Slider("Overlap",a.Overlap,0,1,x=>a.Overlap=x);Slider("Color intensity",a.Intensity,0,1,x=>a.Intensity=x);
                foreach(string key in a.Colors.Keys.ToArray())
                {Vector4 c=Color(a,key);if(ImGui.TreeNode(key+" color")){if(ImGui.ColorPicker4("##"+key,ref c,ImGuiColorEditFlags.PickerHueWheel|ImGuiColorEditFlags.AlphaBar)){a.Colors[key]=ImGui.ColorConvertFloat4ToU32(c);Dirty=true;}ImGui.TreePop();}}
            }
            if(ImGui.CollapsingHeader("Meter modules · overrides"))
            {
                ImGui.TextDisabled("Unchecked automatic discovery can be overridden per module.");
                foreach(string kind in new[]{"Sneak","Acid","Poison","Summons","Thorns","Ward","Second Wind"})
                {bool enabled=Show(a,v.Stats,kind);if(ImGui.Checkbox(kind,ref enabled)){a.Modules[kind]=enabled;Dirty=true;}ImGui.SameLine();if(ImGui.SmallButton("Auto##"+kind)){a.Modules.Remove(kind);Dirty=true;}}
            }
            if(ImGui.CollapsingHeader("RynthVision compatibility"))
            {ImGui.TextWrapped("Run these yourself to avoid duplicate numbers while keeping Rynth's debuff icons and plates:");ImGui.TextUnformatted("/rv plates numbers off");ImGui.TextUnformatted("/rv plates gains on    (keep XP/Lum)");ImGui.TextWrapped("Restore Rynth combat numbers with /rv plates numbers on. This plugin does not change those settings.");}
            if(ImGui.Button("Export compact summary"))command("export");ImGui.TextWrapped(v.Notice);
        }
        finally{ImGui.End();}
    }
    private static void Floating(Appearance a,FloatEvent[] events,Vector2 viewport,double now)
    {
        var draw=ImGui.GetForegroundDrawList();Vector2 anchor=new(viewport.X*a.AnchorX,viewport.Y*a.AnchorY);
        foreach(var e in events)
        {
            double age=now-e.Start;if(age<0||age>=a.Lifetime)continue;
            var arc=FloatStream.Arc(e,now,a);float alpha=arc.Alpha;
            string label=FloatStream.Format(e);
            if(label.Length==0)continue;
            bool regular=FloatStream.Regular(e);
            float size=a.FontSize*(e.Critical?a.CritScale:regular?a.RegularScale:1)*(1+(e.Critical?.13f:0)*(float)Math.Exp(-age*9));
            if(regular)alpha*=a.RegularAlpha;
            Vector2 p=anchor+new Vector2(arc.X,arc.Y);float width=ImGui.CalcTextSize(label).X*size/ImGui.GetFontSize();
            p.X=Math.Clamp(p.X-width/2,2,Math.Max(2,viewport.X-width-2));p.Y=Math.Clamp(p.Y,2,Math.Max(2,viewport.Y-size-2));
            Vector4 c=Color(a,e.Kind);float l=(c.X+c.Y+c.Z)/3;float saturation=(e.Critical?1:a.Intensity)*(a.Subdued?.65f:1);c.X=l+(c.X-l)*saturation;c.Y=l+(c.Y-l)*saturation;c.Z=l+(c.Z-l)*saturation;if(e.Critical){float flash=(float)Math.Exp(-age*7)*.35f;c.X+= (1-c.X)*flash;c.Y+=(1-c.Y)*flash;c.Z+=(1-c.Z)*flash;}c.W*=alpha;
            uint tint=ImGui.ColorConvertFloat4ToU32(c),black=(uint)(alpha*235)<<24;
            if((!regular||e.Kind is "Acid" or "Poison" or "Sneak")&&!a.Subdued&&a.Intensity>.05f)Art(draw,e,p,new(width,size),c,age,a.Intensity*(regular?.35f:1));
            if(a.Outline>0)for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)if(x!=0||y!=0)draw.AddText(ImGui.GetFont(),size,p+new Vector2(x,y)*a.Outline,black,label);
            draw.AddText(ImGui.GetFont(),size,p,tint,label);if(a.Bold&&!regular)draw.AddText(ImGui.GetFont(),size,p+new Vector2(.65f,0),tint,label);
        }
    }
    private static void Art(ImDrawListPtr d,FloatEvent e,Vector2 p,Vector2 text,Vector4 color,double age,float intensity)
    {
        Vector2 center=p+text*.5f;float t=(float)age;
        uint Tint(float alpha){var c=color;c.W*=Math.Clamp(alpha*intensity,0,1);return ImGui.ColorConvertFloat4ToU32(c);}
        if(e.Kind=="Acid")
        {
            for(int i=0;i<6;i++){float angle=i*1.047f+e.Id*.7f,travel=8+22*t;var q=center+new Vector2(MathF.Cos(angle)*travel,MathF.Sin(angle)*travel*.55f+8*t*t);d.AddCircleFilled(q,Math.Max(1,3-t),Tint(.5f),8);}
        }
        else if(e.Kind=="Poison")
        {
            for(int j=0;j<2;j++)for(int i=0;i<7;i++){float a=i*.4f+t*3+j*2;Vector2 q=new(p.X-10-j*7+MathF.Sin(a)*5,p.Y+text.Y-i*4-t*8);d.AddCircleFilled(q,1.3f+i*.15f,Tint(.16f+i*.035f),8);}
        }
        else if(e.Kind=="Healing")
        {
            for(int strand=0;strand<2;strand++)for(int i=0;i<6;i++){float q=i*.42f+t*3+strand*MathF.PI;Vector2 mote=center+new Vector2(MathF.Sin(q)*(18-i*1.5f),8-i*9-t*12);d.AddCircleFilled(mote,1.8f,Tint(.35f),8);}
            float r=8+(t%1)*28;d.AddCircle(center,r,Tint(.22f*(1-t%1)),24,1.2f);for(int i=0;i<3;i++){var q=center+new Vector2((i-1)*23,-10-t*15-i*9);d.AddLine(q-new Vector2(3,0),q+new Vector2(3,0),Tint(.4f),1.5f);d.AddLine(q-new Vector2(0,3),q+new Vector2(0,3),Tint(.4f),1.5f);}}
        if(e.Critical||e.Kind=="Critical"||e.Kind=="Kill")
        {
            float pop=MathF.Exp(-t*7);d.AddCircleFilled(center,5+8*pop,Tint(pop*.25f),16);
            float fade=MathF.Exp(-t*5);for(int i=0;i<8;i++){float angle=i*MathF.PI/4;Vector2 dir=new(MathF.Cos(angle),MathF.Sin(angle));d.AddLine(center+dir*(15+18*t),center+dir*(22+35*t),Tint(fade),2);}d.AddCircle(center,8+50*t,Tint(fade*.4f),32,1.5f);}
        else if(e.Kind=="Ward")
        {d.AddCircle(center,Math.Max(text.X*.55f,20),Tint(.15f),6,1.5f);}
        else if(e.Kind=="Sneak")
        {d.AddLine(p-new Vector2(10,-text.Y),p+new Vector2(text.X*.2f,-5),Tint(MathF.Exp(-t*3)*.7f),2);}
    }
}
