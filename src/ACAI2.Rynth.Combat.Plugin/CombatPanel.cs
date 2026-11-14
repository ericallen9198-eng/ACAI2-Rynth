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
    public void ResetPlacement()=>place=true;
    public void Reset(){place=true;Setup=false;Dirty=false;selected="";}
    private static Vector4 Accent=>new(.38f,.9f,.77f,1);
    public void Draw(string character,Appearance a,CombatView v,double now,Action<string> command,IntPtr floatFont,IntPtr boldFont)
    {
        if(ImGui.GetCurrentContext()==IntPtr.Zero)return;int f=ImGui.GetFrameCount();if(f==frame)return;frame=f;
        Vector2 screen=ImGui.GetIO().DisplaySize;if(screen.X<=0||screen.Y<=0)return;
        if(a.Visible&&a.MeterMode==MeterMode.Normal)
        {
            if(place){ImGui.SetNextWindowPos(new(Math.Clamp(a.X,0,Math.Max(0,screen.X-340)),Math.Clamp(a.Y,0,Math.Max(0,screen.Y-200))),ImGuiCond.Always);place=false;}
            ImGui.SetNextWindowSize(new(310*a.Scale,0),ImGuiCond.Always);ImGui.SetNextWindowBgAlpha(a.Opacity);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,7);ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(4,2)*a.Scale);ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(7,5)*a.Scale);ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(.025f,.045f,.055f,1));
            var flags=ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoResize|ImGuiWindowFlags.NoCollapse;if(a.Locked)flags|=ImGuiWindowFlags.NoMove;
            ImGui.Begin("ACAI2 COMBAT###acai2-combat-meter",flags);
            try
            {
                ImGui.SetWindowFontScale(a.Scale);
                if(a.CustomLettering)
                {
                    var header=ImGui.GetCursorScreenPos()-new Vector2(5,3);var tint=Accent;tint.W=a.Opacity;
                    ImpactArt.Plate(header,new Vector2(ImGui.GetWindowSize().X-ImGui.GetStyle().WindowPadding.X*2+10,57*a.Scale),tint,.5,(points,color)=>Polygon(ImGui.GetWindowDrawList(),points,color));
                }
                ImGui.TextUnformatted("COMBAT / "+character);ImGui.SameLine();if(ImGui.SmallButton("Setup"))Setup=!Setup;
                ImGui.Separator();
                if(a.CustomLettering)
                {
                    var origin=ImGui.GetCursorScreenPos();string dps=v.Dps.ToString("N0",System.Globalization.CultureInfo.InvariantCulture);float numberSize=30*a.Scale;
                    ImpactLettering.Draw(dps,origin,numberSize,Accent,a.Bold,(points,tint)=>Polygon(ImGui.GetWindowDrawList(),points,tint));
                    ImGui.Dummy(new Vector2(ImpactLettering.Width(dps,numberSize)+5,numberSize));ImGui.SameLine();ImGui.TextUnformatted($"DPS  ·  {v.Taken:N0} in/s");
                }
                else{ImGui.TextColored(Accent,$"{v.Dps:N0} DPS");ImGui.SameLine();ImGui.TextUnformatted($"   {v.Taken:N0} taken/s");}
                ImGui.TextDisabled(v.Preview?"EFFECT PREVIEW · real pack DPS":v.Pack.HasCombat?$"Pack {v.Pack.Number} · {(v.Pack.Active?"ACTIVE":"FINAL")} · {v.Pack.Seconds:N1}s":"Waiting for the next pack");
                if(v.RunScope&&v.Current?.ParserVersion!=CombatEngine.ParserVersion)ImGui.TextDisabled("Includes earlier run data");
                var t=v.Stats;ImGui.TextUnformatted($"{(v.RunScope?"Thread":"Session")} · Dmg {t.Damage:N0}  Heal {t.Healing:N0}  Kills {t.Kills}");
                if(Show(a,t,"Sneak")){ImGui.TextColored(new(.94f,.66f,1,1),$"Sneak {t.SneakPercent:N1}%  ·  {t.Sneaks}/{t.DirectHits} landed physical hits");Tip("Denominator: your landed direct melee/missile hits, including zero damage. Excludes magic, poison, acid, pets and reflected damage. Discovery occurs on the first observed sneak hit.");}
                if(t.PhysicalHits+t.Evades>0)ImGui.TextUnformatted($"Physical evade {t.EvadePercent:N1}%   Avg hit {t.PhysicalDamage/(double)Math.Max(1,t.PhysicalHits):N0}   High {t.PhysicalHigh:N0}");
                Tip("Physical includes melee and missile: chat does not reliably separate them. Zero-damage landed hits count; shield blocks are listed separately.");
                if(t.MagicHits+t.Resists>0)ImGui.TextUnformatted($"Spell resist {t.ResistPercent:N1}%   Avg spell {t.MagicDamage/(double)Math.Max(1,t.MagicHits):N0}   High {t.MagicHigh:N0}");
                foreach(string kind in new[]{"Acid","Poison","Summons","Thorns"})if(Show(a,t,kind))ImGui.TextColored(Color(a,kind),$"{kind} {t.Components.GetValueOrDefault(kind):N0}  ({100d*t.Components.GetValueOrDefault(kind)/Math.Max(1,t.Damage):N1}%)");
                if(Show(a,t,"Ward"))ImGui.TextUnformatted($"Runic Ward  absorbed {t.WardAbsorbed:N0}  ·  last ready capacity {t.WardCapacity}");
                if(t.Blocks>0)ImGui.TextUnformatted($"Shield blocks {t.Blocks}");
                if(t.KineticReady+t.KineticReleases+t.KineticExpired>0)ImGui.TextUnformatted($"Kinetic · {t.KineticReleases} releases");
                if(Show(a,t,"Reckless"))LiveReckless(v.Reckless);
                if(Show(a,t,"Second Wind"))ImGui.TextUnformatted($"Second Wind {t.Procs.GetValueOrDefault("Second Wind")} procs (heal amount not stated)");
                if(ImGui.SmallButton(details?"Less":"Details"))details=!details;ImGui.SameLine();if(ImGui.SmallButton("Export"))command("export");ImGui.SameLine();if(ImGui.SmallButton("New session"))command("new");
                if(details)Details(v,a,command);
                if(v.Dropped>0)ImGui.TextColored(new(1,.6f,.3f,1),$"{v.Dropped} messages dropped · incomplete observation");
                var pos=ImGui.GetWindowPos();if(pos.X!=a.X||pos.Y!=a.Y){a.X=pos.X;a.Y=pos.Y;Dirty=true;}
            }
            finally{ImGui.End();ImGui.PopStyleColor();ImGui.PopStyleVar(3);}
        }
        if(a.Visible&&a.MeterMode==MeterMode.Compact)Compact(a,v,screen);
        if(a.Visible&&a.MeterMode==MeterMode.UltraCompact)Ultra(a,v,screen);
        if(Setup)Settings(a,v,command);
        if(a.Floating||v.Preview)Floating(a,v.Floats,screen,now,floatFont,boldFont);
    }
    private static string Digits(CombatView v)=>v.Dps.ToString("N0",System.Globalization.CultureInfo.InvariantCulture);
    private static void DrawDigits(ImDrawListPtr draw,string text,Vector2 origin,float size,Appearance a)
        =>ImpactLettering.Draw(text,Pixel(origin),size,Accent,a.Bold,(points,color)=>Polygon(draw,points,color));
    private void Compact(Appearance a,CombatView v,Vector2 screen)
    {
        string text=Digits(v);float size=a.DigitsSize*a.Scale;float width=ImpactLettering.Width(text,size)+14;
        ImGui.SetNextWindowPos(new(Math.Clamp(a.X,0,Math.Max(0,screen.X-width)),Math.Clamp(a.Y,0,Math.Max(0,screen.Y-size-12))),ImGuiCond.Always);
        ImGui.SetNextWindowSize(new(ImpactLettering.Width(text,size)+14,size+10),ImGuiCond.Always);ImGui.SetNextWindowBgAlpha(a.Opacity);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(7,5));
        var flags=ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoResize|ImGuiWindowFlags.NoCollapse|ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoMove;
        ImGui.Begin("ACAI2 COMBAT###acai2-combat-meter",flags);
        try{DrawDigits(ImGui.GetWindowDrawList(),text,ImGui.GetCursorScreenPos(),size,a);ImGui.InvisibleButton("##compact-digits",new(ImpactLettering.Width(text,size),size));Tip($"Pack {v.Pack.Number}: {(v.Pack.Active?"active":"final")} · {v.Pack.Seconds:N1}s. /acai2combat setup");if(!a.Locked&&ImGui.IsItemActive()&&ImGui.IsMouseDragging(ImGuiMouseButton.Left)){var p=Vector2.Clamp(new Vector2(a.X,a.Y)+ImGui.GetIO().MouseDelta,Vector2.Zero,Vector2.Max(Vector2.Zero,screen-new Vector2(width,size+10)));a.X=p.X;a.Y=p.Y;Dirty=true;}}
        finally{ImGui.End();ImGui.PopStyleVar();}
    }
    private void Ultra(Appearance a,CombatView v,Vector2 screen)
    {
        string text=Digits(v);float size=a.DigitsSize*a.Scale;Vector2 bounds=new(ImpactLettering.Width(text,size),size);
        Vector2 p=new(Math.Clamp(a.DigitsX,0,Math.Max(0,screen.X-bounds.X)),Math.Clamp(a.DigitsY,0,Math.Max(0,screen.Y-bounds.Y)));
        // Locked: no ImGui window, widgets or capture request. This is the floating-text layer.
        // Unlocked: reuse the existing meter's window ID solely for its transparent drag target.
        if(!a.DigitsLocked)
        {
            ImGui.SetNextWindowPos(p,ImGuiCond.Always);ImGui.SetNextWindowSize(bounds+new Vector2(8),ImGuiCond.Always);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,Vector2.Zero);ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize,0);
            ImGui.Begin("ACAI2 COMBAT###acai2-combat-meter",ImGuiWindowFlags.NoBackground|ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoSavedSettings);
            try
            {
                ImGui.InvisibleButton("##drag-dps-digits",bounds+new Vector2(8));
                if(ImGui.IsItemActive()&&ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {p+=ImGui.GetIO().MouseDelta;p=Vector2.Clamp(p,Vector2.Zero,Vector2.Max(Vector2.Zero,screen-bounds));a.DigitsX=p.X;a.DigitsY=p.Y;Dirty=true;}
                if(ImGui.IsItemHovered()){ImGui.GetForegroundDrawList().AddRect(p-Vector2.One,p+bounds+Vector2.One,0x9968bbaa);Tip("Drag DPS digits. /acai2combat lock enables click-through.");}
            }
            finally{ImGui.End();ImGui.PopStyleVar(2);}
        }
        DrawDigits(ImGui.GetForegroundDrawList(),text,p,size,a);
    }
    private static void LiveReckless(RecklessView r)
    {
        string power=r.On?(r.PowerPercent.HasValue?$" · power {r.PowerPercent:N0}%":" · power unavailable"):"";
        ImGui.TextUnformatted("Reckless "+(r.On?"ON":"OFF")+power);
        Tip("ON = an outgoing direct physical Recklessness-tagged hit in the last 4 seconds. OFF = no recent tag, not proof the skill is disabled. Power is the last validated pre-hit requested-power sample (at most 500ms old), not the momentary charging animation. 10–90% is the stated Recklessness range.");
    }
    private static bool Show(Appearance a,Totals t,string kind)=>a.Modules.TryGetValue(kind,out bool enabled)?enabled:kind=="Sneak"?t.Sneaks>0:kind=="Reckless"?t.RecklessOutgoing.Count>0:t.Components.ContainsKey(kind)||t.Procs.ContainsKey(kind);
    private static void Tip(string text){if(ImGui.IsItemHovered()){ImGui.BeginTooltip();ImGui.PushTextWrapPos(420);ImGui.TextUnformatted(text);ImGui.PopTextWrapPos();ImGui.EndTooltip();}}
    private static Vector4 Color(Appearance a,string kind)=>ImGui.ColorConvertU32ToFloat4(a.Colors.GetValueOrDefault(kind,a.Colors["Direct"]));
    private void Details(CombatView v,Appearance a,Action<string> command)
    {
        var t=v.Stats;ImGui.Separator();
        if(ImGui.TreeNode("Damage sources"))
        {if(t.RecklessOutgoing.Count>0)RecklessLine("OUT",t.RecklessOutgoing,t.DirectHits,t.RecklessOutgoingPercent);ImGui.TextUnformatted($"Kinetic: {t.KineticReleases} releases · {t.KineticReady} ready · {t.KineticExpired} expired");ImGui.TextUnformatted($"Kinetic repeated state notices suppressed this login: {v.KineticDuplicates}");if(t.Procs.TryGetValue("Kinetic",out int legacyKinetic))ImGui.TextDisabled($"Legacy Kinetic notices {legacyKinetic} (not a release count)");if(t.PeriodicTicks>0)ImGui.TextUnformatted($"Periodic incoming: {t.PeriodicTicks} ticks / {t.PeriodicTaken:N0} damage (excluded from direct spell resist rate)");foreach(var p in t.Components.OrderByDescending(p=>p.Value))ImGui.TextUnformatted($"{p.Key}: {p.Value:N0}");if(t.UnclassifiedHits>0)ImGui.TextUnformatted($"Unclassified incoming: {t.UnclassifiedHits} hits / {t.UnclassifiedDamage:N0} damage (excluded from defense rates)");foreach(var p in t.IncomingEffects)ImGui.TextUnformatted($"Incoming {p.Key}: {p.Value} applied/refreshed");foreach(var p in t.HealingSources)ImGui.TextUnformatted($"{p.Key}: +{p.Value:N0}");foreach(var p in t.Procs)ImGui.TextUnformatted($"{p.Key}: {p.Value} notices");ImGui.TextUnformatted($"Blocks {t.Blocks} · Physical crits {t.Crits}/{t.DirectHits}");ImGui.TreePop();}
        if(ImGui.TreeNode("Big hits · "+t.HighestObserved+" highest observed")){BigHits(t);ImGui.TreePop();}
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
            if(chosen!=null)
            {
                RunHeader(chosen);
                if(chosen.Stats.RecklessOutgoing.Count>0)RecklessLine("OUT",chosen.Stats.RecklessOutgoing,chosen.Stats.DirectHits,chosen.Stats.RecklessOutgoingPercent);
                if(ImGui.TreeNode("Big hits##"+chosen.Id)){BigHits(chosen.Stats);ImGui.TreePop();}
                MobTable(chosen.Stats);Deaths(chosen.Stats);
            }
            ImGui.TreePop();
        }
        if(t.Truncated)ImGui.TextWrapped("A bounded mob/death limit was reached; totals continue but detail is incomplete.");
        ImGui.TextWrapped(v.Notice);
    }
    private static void RunHeader(RunRecord r)
    {
        if(r.ParserVersion!=CombatEngine.ParserVersion)ImGui.TextDisabled("Includes earlier parser totals; old aggregates are not reclassified.");
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
    private static void RecklessLine(string direction,TaggedHits hits,int total,double percent)
    {
        ImGui.TextUnformatted($"Reckless {direction} {percent:N1}% · avg {hits.Average:N1} · H {hits.High} · L {hits.Low?.ToString()??"-"}");
        Tip($"Reckless-tagged landed physical hits / all landed physical hits in this direction: {hits.Count}/{total}. Includes zeros and tagged crits; excludes spells, poison, acid and summons. Marker evidence does not assign the skill to a particular participant or infer its multiplier.");
    }
    private static void Context(HitContext? context)
    {
        if(context==null){ImGui.TextDisabled("No historical at-hit effect snapshot was recorded.");return;}
        ImGui.TextUnformatted("Imperil: "+(context.Imperiled==true?"PRESENT":context.Imperiled==false?"not observed in complete sample":"UNKNOWN"));
        ImGui.TextWrapped("Snapshot vulns: "+(context.Vulnerabilities.Length>0?string.Join(", ",context.Vulnerabilities):!context.Available||context.UnknownSpellIds.Length>0||context.ChangesSinceSample.Length>0?"UNKNOWN / no classified vuln observed":"none observed in complete sample"));
        ImGui.TextWrapped(context.Status+(context.AgeMilliseconds.HasValue?$" · sample age {context.AgeMilliseconds:N0}ms":""));
        if(context.UnknownSpellIds.Length>0)ImGui.TextWrapped("Unclassified spell IDs: "+string.Join(", ",context.UnknownSpellIds));
        if(context.ChangesSinceSample.Length>0)ImGui.TextWrapped("Changes after sample: "+string.Join(", ",context.ChangesSinceSample));
    }
    private static void BigHits(Totals t)
    {
        var historical=t.Deaths.Where(d=>d.Blow.HasValue&&!t.BigHits.Any(h=>h.KillingBlow&&h.Damage==d.Blow&&Math.Abs((h.Utc-d.Utc).TotalSeconds)<=1)).Select(d=>new IncomingHit(d.Utc,d.Killer=="Unspecified killer"?DeathSource.Parse(d.Message):d.Killer,d.Blow!.Value,"Unknown","Unknown","",false,false,true,d.Health,d.Overkill,d.Context??HitContext.Unknown("Historical death: at-hit effects were not recorded"),"Historical server death notice; attack type unconfirmed"));
        var hits=t.BigHits.Concat(historical).OrderByDescending(h=>h.Damage).Take(24).ToArray();
        if(hits.Length==0)ImGui.TextDisabled("No qualifying hit snapshots. Older run files cannot reconstruct past debuffs.");
        foreach(var h in hits)
        {
            if(!ImGui.TreeNode($"{h.Damage:N0} · {h.Attacker}"+(h.KillingBlow?" · LETHAL":"")+"##"+h.Utc.Ticks))continue;
            ImGui.TextUnformatted(h.Utc.ToLocalTime().ToString("T")+" · "+h.Delivery+" · "+h.Element);
            if(h.Spell.Length>0)ImGui.TextWrapped("Spell: "+h.Spell);
            ImGui.TextUnformatted("Crit: "+(h.Critical?"yes":"not marked"));
            if(h.HealthBefore.HasValue)ImGui.TextUnformatted($"Health before {h.HealthBefore} · overkill {h.Overkill}");
            ImGui.TextWrapped(h.Evidence);Context(h.Context);ImGui.TreePop();
        }
    }
    private static void Deaths(Totals t)
    {
        if(t.Deaths.Count==0)ImGui.TextDisabled("No observed death notices.");
        foreach(var d in t.Deaths.AsEnumerable().Reverse())
        {ImGui.TextColored(new(1,.45f,.45f,1),d.Utc.ToLocalTime().ToString("T")+" · "+(d.Killer=="Unspecified killer"?DeathSource.Parse(d.Message):d.Killer));ImGui.TextWrapped(d.Message);if(d.Blow.HasValue)ImGui.TextUnformatted($"Blow {d.Blow} · HP {d.Health}/{d.Maximum} · overkill {d.Overkill}");Context(d.Context);}
    }
    private void Toggle(string label,bool value,Action<bool> set){if(ImGui.Checkbox(label,ref value)){set(value);Dirty=true;}}
    private void Slider(string label,float value,float min,float max,Action<float> set){if(ImGui.SliderFloat(label,ref value,min,max)){set(value);Dirty=true;}}
    private void Settings(Appearance a,CombatView v,Action<string> command)
    {
        ImGui.SetNextWindowSize(new(470,570),ImGuiCond.FirstUseEver);ImGui.Begin("ACAI2 Combat setup",ref Setup);
        try
        {
            ImGui.TextWrapped("Standalone display. Debuff icons remain owned by Rynth. Events discover their own meter modules.");
            int mode=(int)a.MeterMode;if(ImGui.Combo("Display mode",ref mode,"Normal\0Compact\0Ultra-compact\0")){a.MeterMode=(MeterMode)mode;place=true;Dirty=true;}
            Slider("Pack idle cutoff (seconds)",a.PackIdleSeconds,2,12,x=>a.PackIdleSeconds=x);
            ImGui.TextDisabled("All modes share pack DPS; a quiet gap freezes it and the next pack resets it.");
            Slider("Compact / ultra digit size",a.DigitsSize,18,90,x=>a.DigitsSize=x);
            Toggle("Lock ultra digits (click-through)",a.DigitsLocked,x=>a.DigitsLocked=x);
            if(a.MeterMode==MeterMode.UltraCompact)ImGui.TextWrapped("Unlock to drag the digits. Locked digits use only the existing floating draw layer. /acai2combat unlock or setup restores editing.");
            Toggle("Lock meter position",a.Locked,x=>a.Locked=x);Toggle("Show meter",a.Visible,x=>a.Visible=x);Toggle("Floating combat text",a.Floating,x=>a.Floating=x);
            Toggle("Custom combat lettering",a.CustomLettering,x=>a.CustomLettering=x);
            Toggle("Subdued effects",a.Subdued,x=>a.Subdued=x);Toggle("Bold effects and crits",a.Bold,x=>a.Bold=x);
            Slider("Meter transparency",a.Opacity,0,1,x=>a.Opacity=x);Slider("Meter scale",a.Scale,.7f,2,x=>a.Scale=x);
            if(ImGui.Button("Apply impact layout")){a.CustomLettering=true;a.FontSize=23;a.Gap=185;a.Curve=45;a.Overlap=0;a.Lifetime=2.2f;a.CritScale=1.5f;a.RegularScale=.8f;a.RegularAlpha=.8f;Dirty=true;}
            ImGui.TextDisabled("Layout preset changes floating size/spacing; keeps your colors and meter position.");
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
            int threshold=a.BigHitMinimum;if(ImGui.InputInt("Big hit minimum",ref threshold)){a.BigHitMinimum=Math.Clamp(threshold,1,10000);Dirty=true;}
            ImGui.TextDisabled("Keep 24 largest hits per run; killing blows always qualify for ranking.");
            if(ImGui.CollapsingHeader("Meter modules · overrides"))
            {
                ImGui.TextDisabled("Unchecked automatic discovery can be overridden per module.");
                foreach(string kind in new[]{"Sneak","Acid","Poison","Summons","Thorns","Ward","Second Wind","Reckless"})
                {bool enabled=Show(a,v.Stats,kind);if(ImGui.Checkbox(kind,ref enabled)){a.Modules[kind]=enabled;Dirty=true;}ImGui.SameLine();if(ImGui.SmallButton("Auto##"+kind)){a.Modules.Remove(kind);Dirty=true;}}
            }
            if(ImGui.CollapsingHeader("RynthVision compatibility"))
            {ImGui.TextWrapped("Run these yourself to avoid duplicate numbers while keeping Rynth's debuff icons and plates:");ImGui.TextUnformatted("/rv plates numbers off");ImGui.TextUnformatted("/rv plates gains on    (keep XP/Lum)");ImGui.TextWrapped("Restore Rynth combat numbers with /rv plates numbers on. This plugin does not change those settings.");}
            if(ImGui.Button("Export compact summary"))command("export");ImGui.TextWrapped(v.Notice);
        }
        finally{ImGui.End();}
    }
    private static Vector2 Pixel(Vector2 p)=>new(MathF.Round(p.X),MathF.Round(p.Y));
    private static void Lettering(ImDrawListPtr draw,string text,Vector2 p,float size,Vector4 color,float outline)
    {
        p=Pixel(p);size=MathF.Round(size);
        uint black=ImGui.ColorConvertFloat4ToU32(new Vector4(0,0,0,color.W*.94f));
        int edge=(int)MathF.Round(outline);
        if(edge>0)for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)if(x!=0||y!=0)
            draw.AddText(ImGui.GetFont(),size,p+new Vector2(x*edge,y*edge),black,text);
        // One fill only: offset duplicate fills blur the glyph edges.
        draw.AddText(ImGui.GetFont(),size,p,ImGui.ColorConvertFloat4ToU32(color),text);
    }
    private static void Polygon(ImDrawListPtr draw,Vector2[] points,Vector4 color)
    {
        // Dear ImGui's AA convex fill expects clockwise points in screen coordinates.
        float area=0;for(int i=0;i<points.Length;i++){var b=points[(i+1)%points.Length];area+=points[i].X*b.Y-b.X*points[i].Y;}
        if(area<0)for(int i=points.Length-1;i>=0;i--)draw.PathLineTo(points[i]);
        else foreach(var q in points)draw.PathLineTo(q);
        draw.PathFillConvex(ImGui.ColorConvertFloat4ToU32(color));
    }
    private static void Floating(Appearance a,FloatEvent[] events,Vector2 viewport,double now,IntPtr floatFont,IntPtr boldFont)
    {
        var draw=ImGui.GetForegroundDrawList();Vector2 anchor=new(viewport.X*a.AnchorX,viewport.Y*a.AnchorY);
        var layout=new FloatLayout(viewport,anchor);
        // New important notices claim space first. Quiet events yield visually when the screen is full;
        // the underlying combat counters and event compression are never changed by layout.
        foreach(var e in events.OrderByDescending(e=>e.Kind=="KineticReady"?3:e.Critical||e.Kind is "Critical" or "KineticRelease"?2:e.Kind=="Healing"||e.Kind=="KineticReady"||e.Kind=="Imperil"||e.Kind.StartsWith("Vuln")?1:0).ThenByDescending(e=>e.Id))
        {
            double age=now-e.Start;if(age<0||age>=a.Lifetime)continue;
            var arc=FloatStream.Arc(e,now,a);float alpha=arc.Alpha;
            string label=FloatStream.Format(e);if(label.Length==0)continue;
            bool regular=FloatStream.Regular(e),impact=e.Critical||e.Kind is "Critical" or "KineticRelease",effect=e.Kind=="KineticReady"||e.Kind=="Imperil"||e.Kind.StartsWith("Vuln",StringComparison.Ordinal);
            bool numeric=FloatStream.HasNumber(e)&&a.CustomLettering;
            float size=MathF.Round(a.FontSize*(impact?a.CritScale*(a.CustomLettering?1.3f:1):regular?a.RegularScale:1)*(impact?ImpactLettering.Punch(age):1));
            if(effect)size=MathF.Round(a.FontSize*1.08f);
            if(regular)alpha*=a.RegularAlpha;
            // Prefer the larger baked font for labels; never enlarge the small bold atlas for crits.
            IntPtr chosen=floatFont!=IntPtr.Zero?floatFont:boldFont;
            bool pushed=chosen!=IntPtr.Zero;if(pushed)ImGui.PushFont(new ImFontPtr(chosen));
            try
            {
                string headline=label,caption="";
                if(numeric||impact&&FloatStream.HasNumber(e))
                {
                    headline=ImpactLettering.Number(e);
                    caption=ImpactLettering.Caption(e)+(e.Count>1?" ×"+e.Count:"");
                }
                if(a.CustomLettering){headline=headline.ToUpperInvariant();caption=caption.ToUpperInvariant();}
                float Width(string text,float pixels)=>ImGui.CalcTextSize(text).X*pixels/ImGui.GetFontSize();
                float captionSize=MathF.Round(Math.Max(12,Math.Min(18,size*.52f)));
                bool vectorHeadline=a.CustomLettering&&ImpactLettering.Supports(headline);
                bool vectorCaption=a.CustomLettering&&ImpactLettering.Supports(caption);
                float headlineWidth=vectorHeadline?ImpactLettering.Width(headline,size):Width(headline,size);
                float captionWidth=vectorCaption?ImpactLettering.Width(caption,captionSize):Width(caption,captionSize);
                float width=Math.Max(headlineWidth,captionWidth);
                float height=size+(caption.Length>0?captionSize+3:0);
                Vector2 padding=effect?new(13,7):impact?new(32,8):Vector2.Zero;
                Vector2 bounds=new(width+padding.X*2,height+padding.Y*2);
                Vector2 p=anchor+new Vector2(arc.X,arc.Y);
                // Hold crits briefly, then let them drift. Entry is an outward punch, never screen shake.
                if(impact){p.X+=(e.Incoming?-1:1)*14*MathF.Exp(-(float)age*9);p.Y+=a.Rise*Math.Min((float)age,.20f)/a.Lifetime;}
                if(!layout.TryPlace(p,bounds,e.Kind=="Healing"?0:e.Incoming?-1:1,out p))continue;
                Vector4 c=Color(a,e.Kind);
                if(impact&&e.Incoming&&e.Kind=="Taken")c=new(1,.32f,.29f,c.W);
                float l=(c.X+c.Y+c.Z)/3,saturation=(impact||effect?1:a.Intensity)*(a.Subdued?.65f:1);
                c.X=l+(c.X-l)*saturation;c.Y=l+(c.Y-l)*saturation;c.Z=l+(c.Z-l)*saturation;c.W*=alpha;
                if(effect)ImpactArt.Plate(p,bounds,c,age,(points,tint)=>Polygon(draw,points,tint));
                Vector2 textP=p+padding;
                if(!a.Subdued&&a.Intensity>.05f&&(!regular||e.Kind is "Acid" or "Poison" or "Sneak"))Art(draw,e,textP,new(width,height),c,age,a.Intensity*(regular?.85f:1));
                if(vectorHeadline)ImpactLettering.Draw(headline,textP+new Vector2((width-headlineWidth)/2,0),size,c,a.Bold,(points,tint)=>Polygon(draw,points,tint));
                else Lettering(draw,headline,textP+new Vector2((width-headlineWidth)/2,0),size,c,a.Outline);
                if(caption.Length>0)
                {
                    Vector2 captionP=textP+new Vector2((width-captionWidth)/2,size+3);
                    if(vectorCaption)ImpactLettering.Draw(caption,captionP,captionSize,c,false,(points,tint)=>Polygon(draw,points,tint));
                    else Lettering(draw,caption,captionP,captionSize,c,a.Outline);
                }
            }
            finally{if(pushed)ImGui.PopFont();}
        }
    }
    private static void Art(ImDrawListPtr d,FloatEvent e,Vector2 p,Vector2 text,Vector4 color,double age,float intensity)
    {
        float t=(float)age;Vector2 center=p+text*.5f;
        uint Tint(float alpha){var c=color;c.W*=Math.Clamp(alpha*intensity,0,1);return ImGui.ColorConvertFloat4ToU32(c);}
        // Decoration stays outside the lettering's bounding box.
        if(e.Kind=="Healing")
        {
            float fade=MathF.Exp(-t*1.3f),r=12+28*Math.Min(t,1);
            Vector2 halo=new(center.X,p.Y+text.Y+10);
            d.AddEllipse(halo,new(r,r*.26f),Tint(fade*.8f),0,24,1.6f);
            d.AddEllipse(halo+new Vector2(0,3),new(r*.72f,r*.15f),Tint(fade*.4f),0,24,1);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<6;i++)
            {
                float phase=t*3+i*.8f;Vector2 q=new(center.X+side*(text.X*.5f+11+MathF.Sin(phase)*6),p.Y+text.Y-i*8-t*13);
                d.AddCircleFilled(Pixel(q),2,Tint(fade*.7f),8);
                if(i is 1 or 4){d.AddLine(q-new Vector2(3,0),q+new Vector2(3,0),Tint(fade*.5f),1.3f);d.AddLine(q-new Vector2(0,3),q+new Vector2(0,3),Tint(fade*.5f),1.3f);}
            }
        }
        else if(e.Kind=="Acid")
        {
            float fade=MathF.Exp(-t*.9f);
            // Thin local mist sits behind/below the lettering, not over the character.
            for(int i=0;i<3;i++)
            {
                Vector2 fog=new(p.X+text.X*(.18f+i*.32f)+MathF.Sin(t*2+i)*4,p.Y+text.Y+6);
                d.AddEllipseFilled(fog,new(Math.Max(10,text.X*.26f),6+i),Tint(fade*.10f),0,20);
            }
            for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)
            {
                Vector2 q=new(center.X+side*(text.X*.5f+7+i*4),p.Y+i*7+t*17);
                d.AddLine(q-new Vector2(0,5),q+new Vector2(0,2),Tint(fade*.65f),1.4f);
                d.AddTriangleFilled(q-new Vector2(0,5),q+new Vector2(-2,1),q+new Vector2(2,1),Tint(fade*.7f));
                d.AddCircleFilled(q+new Vector2(0,1),2,Tint(fade*.7f),8);
            }
        }
        else if(e.Kind=="Poison")
        {
            float fade=MathF.Exp(-t*.8f);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<6;i++)
            {
                float phase=i*.65f+t*3;Vector2 q=new(center.X+side*(text.X*.5f+9+MathF.Sin(phase)*5),p.Y+text.Y-i*6-t*9);
                d.AddCircleFilled(Pixel(q),1.6f+i*.13f,Tint(fade*.6f),8);
                Vector2 trail=q+new Vector2(side*MathF.Cos(phase)*3,5);d.AddLine(q,trail,Tint(fade*.35f),1.4f);
            }
        }
        else if(e.Kind=="KineticReady")
        {
            ImpactArt.Charge(p,text,color,age,intensity,(from,to,tint,width)=>d.AddLine(Pixel(from),Pixel(to),ImGui.ColorConvertFloat4ToU32(tint),width));
        }
        if(e.Critical||e.Kind is "Critical" or "Kill" or "KineticRelease")
        {
            ImpactArt.Burst(p,text,color,age,intensity,(from,to,tint,thickness)=>d.AddLine(Pixel(from),Pixel(to),ImGui.ColorConvertFloat4ToU32(tint),thickness),(points,tint)=>Polygon(d,points,tint));
        }
        else if(e.Kind=="Ward")
        {d.AddLine(p+new Vector2(-5,text.Y+4),p+new Vector2(text.X+5,text.Y+4),Tint(.35f),1);}
        else if(e.Kind=="Imperil"||e.Kind.StartsWith("Vuln",StringComparison.Ordinal))
        {
            d.AddLine(p+new Vector2(-7,2),p+new Vector2(-7,text.Y-2),Tint(.7f),2);
            d.AddLine(p+new Vector2(-7,text.Y-2),p+new Vector2(-2,text.Y-2),Tint(.7f),2);
        }
        else if(e.Kind=="Sneak")
        {d.AddLine(p+new Vector2(-7,text.Y),p+new Vector2(-2,0),Tint(MathF.Exp(-t*3)*.7f),2);}
    }
}
